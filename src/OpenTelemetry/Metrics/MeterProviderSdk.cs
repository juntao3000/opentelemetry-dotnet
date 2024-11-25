// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics;

internal sealed class MeterProviderSdk : MeterProvider
{
    // 配置键：示例过滤器
    internal const string ExemplarFilterConfigKey = "OTEL_METRICS_EXEMPLAR_FILTER";
    // 配置键：直方图的示例过滤器
    internal const string ExemplarFilterHistogramsConfigKey = "OTEL_DOTNET_EXPERIMENTAL_METRICS_EXEMPLAR_FILTER_HISTOGRAMS";

    // 服务提供者
    internal readonly IServiceProvider ServiceProvider;
    // 拥有的服务提供者（可选）
    internal readonly IDisposable? OwnedServiceProvider;
    // 关闭计数
    internal int ShutdownCount;
    // 是否已释放
    internal bool Disposed;
    // 示例过滤器类型（可选）
    internal ExemplarFilterType? ExemplarFilter;
    // 直方图的示例过滤器类型（可选）
    internal ExemplarFilterType? ExemplarFilterForHistograms;
    // 收集可观察仪器时的回调（可选）
    internal Action? OnCollectObservableInstruments;

    // 仪器列表
    private readonly List<object> instrumentations = new();
    // 视图配置列表
    private readonly List<Func<Instrument, MetricStreamConfiguration?>> viewConfigs;
    // 收集锁
    private readonly Lock collectLock = new();
    // 仪表监听器
    private readonly MeterListener listener;
    // 度量读取器（可选）
    private readonly MetricReader? reader;
    // 复合度量读取器（可选）
    private readonly CompositeMetricReader? compositeMetricReader;
    // 是否应监听仪器的函数
    private readonly Func<Instrument, bool> shouldListenTo = instrument => false;

    internal MeterProviderSdk(
        IServiceProvider serviceProvider,
        bool ownsServiceProvider)
    {
        // 确保 serviceProvider 不为空
        Debug.Assert(serviceProvider != null, "serviceProvider was null");

        // 获取 MeterProviderBuilderSdk 实例并注册当前提供程序
        var state = serviceProvider!.GetRequiredService<MeterProviderBuilderSdk>();
        state.RegisterProvider(this);

        // 设置 ServiceProvider
        this.ServiceProvider = serviceProvider!;

        // 如果拥有服务提供者，则将其设置为可释放的
        if (ownsServiceProvider)
        {
            this.OwnedServiceProvider = serviceProvider as IDisposable;
            Debug.Assert(this.OwnedServiceProvider != null, "serviceProvider was not IDisposable");
        }

        // 记录构建 MeterProvider 的事件
        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent("Building MeterProvider.");

        // 获取并配置所有 IConfigureMeterProviderBuilder 实例
        var configureProviderBuilders = serviceProvider!.GetServices<IConfigureMeterProviderBuilder>();
        foreach (var configureProviderBuilder in configureProviderBuilders)
        {
            configureProviderBuilder.ConfigureBuilder(serviceProvider!, state);
        }

        // 设置示例过滤器
        this.ExemplarFilter = state.ExemplarFilter;

        // 应用规范配置键
        this.ApplySpecificationConfigurationKeys(serviceProvider!.GetRequiredService<IConfiguration>());

        // 初始化 StringBuilder 用于记录添加的导出器和仪器工厂
        StringBuilder exportersAdded = new StringBuilder();
        StringBuilder instrumentationFactoriesAdded = new StringBuilder();

        // 构建资源
        var resourceBuilder = state.ResourceBuilder ?? ResourceBuilder.CreateDefault();
        resourceBuilder.ServiceProvider = serviceProvider;
        this.Resource = resourceBuilder.Build();

        // 设置视图配置
        this.viewConfigs = state.ViewConfigs;

        // 记录 MeterProvider 配置
        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent(
            $"MeterProvider configuration: {{MetricLimit={state.MetricLimit}, CardinalityLimit={state.CardinalityLimit}, ExemplarFilter={this.ExemplarFilter}, ExemplarFilterForHistograms={this.ExemplarFilterForHistograms}}}.");

        // 配置度量读取器
        foreach (var reader in state.Readers)
        {
            Guard.ThrowIfNull(reader);

            reader.SetParentProvider(this);

            reader.ApplyParentProviderSettings(
                state.MetricLimit,
                state.CardinalityLimit,
                this.ExemplarFilter,
                this.ExemplarFilterForHistograms);

            if (this.reader == null)
            {
                this.reader = reader;
            }
            else if (this.reader is CompositeMetricReader compositeReader)
            {
                compositeReader.AddReader(reader);
            }
            else
            {
                this.reader = new CompositeMetricReader(new[] { this.reader, reader });
            }

            // 记录导出器信息
            if (reader is PeriodicExportingMetricReader periodicExportingMetricReader)
            {
                exportersAdded.Append(periodicExportingMetricReader.Exporter);
                exportersAdded.Append(" (Paired with PeriodicExportingMetricReader exporting at ");
                exportersAdded.Append(periodicExportingMetricReader.ExportIntervalMilliseconds);
                exportersAdded.Append(" milliseconds intervals.)");
                exportersAdded.Append(';');
            }
            else if (reader is BaseExportingMetricReader baseExportingMetricReader)
            {
                exportersAdded.Append(baseExportingMetricReader.Exporter);
                exportersAdded.Append(" (Paired with a MetricReader requiring manual trigger to export.)");
                exportersAdded.Append(';');
            }
        }

        // 如果有导出器被添加，记录事件
        if (exportersAdded.Length != 0)
        {
            exportersAdded.Remove(exportersAdded.Length - 1, 1);
            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Exporters added = \"{exportersAdded}\".");
        }

        this.compositeMetricReader = this.reader as CompositeMetricReader;

        // 配置仪器
        if (state.Instrumentation.Any())
        {
            foreach (var instrumentation in state.Instrumentation)
            {
                if (instrumentation.Instance is not null)
                {
                    this.instrumentations.Add(instrumentation.Instance);
                }

                instrumentationFactoriesAdded.Append(instrumentation.Name);
                instrumentationFactoriesAdded.Append(';');
            }
        }

        // 如果有仪器工厂被添加，记录事件
        if (instrumentationFactoriesAdded.Length != 0)
        {
            instrumentationFactoriesAdded.Remove(instrumentationFactoriesAdded.Length - 1, 1);
            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Instrumentations added = \"{instrumentationFactoriesAdded}\".");
        }

        // 设置监听器
        if (state.MeterSources.Any(s => WildcardHelper.ContainsWildcard(s)))
        {
            var regex = WildcardHelper.GetWildcardRegex(state.MeterSources);
            this.shouldListenTo = instrument => regex.IsMatch(instrument.Meter.Name);
        }
        else if (state.MeterSources.Any())
        {
            var meterSourcesToSubscribe = new HashSet<string>(state.MeterSources, StringComparer.OrdinalIgnoreCase);
            this.shouldListenTo = instrument =>
            {
                return meterSourcesToSubscribe.Contains(instrument.Meter.Name);
            };
        }

        // 记录监听的仪表
        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Listening to following meters = \"{string.Join(";", state.MeterSources)}\".");

        // 初始化 MeterListener
        this.listener = new MeterListener();
        var viewConfigCount = this.viewConfigs.Count;

        // 记录视图配置数量
        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Number of views configured = {viewConfigCount}.");

        // 设置仪器发布回调
        this.listener.InstrumentPublished = (instrument, listener) =>
        {
            object? state = this.InstrumentPublished(instrument, listeningIsManagedExternally: false);
            if (state != null)
            {
                listener.EnableMeasurementEvents(instrument, state);
            }
        };

        // 设置测量事件回调
        this.listener.SetMeasurementEventCallback<double>(MeasurementRecordedDouble);
        this.listener.SetMeasurementEventCallback<float>(static (instrument, value, tags, state) => MeasurementRecordedDouble(instrument, value, tags, state));
        this.listener.SetMeasurementEventCallback<long>(MeasurementRecordedLong);
        this.listener.SetMeasurementEventCallback<int>(static (instrument, value, tags, state) => MeasurementRecordedLong(instrument, value, tags, state));
        this.listener.SetMeasurementEventCallback<short>(static (instrument, value, tags, state) => MeasurementRecordedLong(instrument, value, tags, state));
        this.listener.SetMeasurementEventCallback<byte>(static (instrument, value, tags, state) => MeasurementRecordedLong(instrument, value, tags, state));

        // 设置测量完成回调
        this.listener.MeasurementsCompleted = MeasurementsCompleted;

        // 启动监听器
        this.listener.Start();

        // 记录 MeterProvider 构建成功事件
        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent("MeterProvider built successfully.");
    }

    /// <summary>
    /// 获取 MeterProvider 的资源。
    /// </summary>
    internal Resource Resource { get; }

    /// <summary>
    /// 获取 MeterProvider 的仪器列表。
    /// </summary>
    internal List<object> Instrumentations => this.instrumentations;

    /// <summary>
    /// 获取 MeterProvider 的度量读取器。
    /// </summary>
    internal MetricReader? Reader => this.reader;

    /// <summary>
    /// 获取 MeterProvider 的视图配置数量。
    /// </summary>
    internal int ViewCount => this.viewConfigs.Count;

    // 处理完成的测量
    internal static void MeasurementsCompleted(Instrument instrument, object? state)
    {
        // 检查状态是否为 MetricState 类型
        if (state is not MetricState metricState)
        {
            // todo: 记录日志
            return;
        }

        // 完成测量
        metricState.CompleteMeasurement();
    }

    // 记录长整型值的测量
    internal static void MeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        // 检查状态是否为 MetricState 类型
        if (state is not MetricState metricState)
        {
            // 如果状态不是 MetricState 类型，记录测量丢失事件
            OpenTelemetrySdkEventSource.Log.MeasurementDropped(instrument?.Name ?? "UnknownInstrument", "SDK internal error occurred.", "Contact SDK owners.");
            return;
        }

        // 记录长整型值的测量
        metricState.RecordMeasurementLong(value, tags);
    }

    // 记录双精度值的测量
    internal static void MeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        // 检查状态是否为 MetricState 类型
        if (state is not MetricState metricState)
        {
            // 如果状态不是 MetricState 类型，记录测量丢失事件
            OpenTelemetrySdkEventSource.Log.MeasurementDropped(instrument?.Name ?? "UnknownInstrument", "SDK internal error occurred.", "Contact SDK owners.");
            return;
        }

        // 记录双精度值的测量
        metricState.RecordMeasurementDouble(value, tags);
    }

    /// <summary>
    /// 处理发布的仪器
    /// </summary>
    internal object? InstrumentPublished(Instrument instrument, bool listeningIsManagedExternally)
    {
        // 检查是否应使用 SDK 配置监听仪器
        var listenToInstrumentUsingSdkConfiguration = this.shouldListenTo(instrument);

        // 如果外部管理监听并且 SDK 配置也监听该仪器，则忽略外部订阅
        if (listeningIsManagedExternally && listenToInstrumentUsingSdkConfiguration)
        {
            OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                instrument.Name,
                instrument.Meter.Name,
                "仪器属于一个仪表，该仪表已通过外部和提供程序上的订阅启用。外部订阅将被忽略，以支持提供程序订阅。",
                "通过编程调用将仪表添加到 SDK（直接调用 AddMeter 或间接通过 'AddInstrumentation' 扩展助手）总是优先于外部注册。当使用外部管理（通常是 IMetricsBuilder 或 IMetricsListener）时，删除对 SDK 的编程调用以允许动态添加和删除注册。");
            return null;
        }
        // 如果既没有外部管理监听也没有 SDK 配置监听该仪器，则忽略该仪器
        else if (!listenToInstrumentUsingSdkConfiguration && !listeningIsManagedExternally)
        {
            OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                instrument.Name, instrument.Meter.Name, "仪器属于一个未被提供程序订阅的仪表。", "使用 AddMeter 将仪表添加到提供程序中。");
            return null;
        }

        object? state = null;
        // 获取视图配置数量
        var viewConfigCount = this.viewConfigs.Count;

        try
        {
            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Started publishing Instrument = \"{instrument.Name}\" of Meter = \"{instrument.Meter.Name}\".");

            // 如果没有视图配置
            if (viewConfigCount <= 0)
            {
                // 检查仪器名称是否有效
                if (!MeterProviderBuilderSdk.IsValidInstrumentName(instrument.Name))
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                        instrument.Name,
                        instrument.Meter.Name,
                        "Instrument name is invalid.",
                        "The name must comply with the OpenTelemetry specification");
                    return null;
                }

                // 如果有度量读取器
                if (this.reader != null)
                {
                    // 添加没有视图的度量
                    var metrics = this.reader.AddMetricWithNoViews(instrument);
                    if (metrics.Count == 1)
                    {
                        state = MetricState.BuildForSingleMetric(metrics[0]);
                    }
                    else if (metrics.Count > 0)
                    {
                        state = MetricState.BuildForMetricList(metrics);
                    }
                }
            }
            else
            {
                // 创建具有初始容量的列表，以避免内部数组调整大小/复制
                var metricStreamConfigs = new List<MetricStreamConfiguration?>(viewConfigCount);
                for (var i = 0; i < viewConfigCount; ++i)
                {
                    var viewConfig = this.viewConfigs[i];
                    MetricStreamConfiguration? metricStreamConfig = null;

                    try
                    {
                        // 获取视图配置
                        metricStreamConfig = viewConfig(instrument);

                        // SDK 提供一些静态的 MetricStreamConfigurations
                        // 例如，Drop 配置。静态 ViewId 不应更改
                        if (metricStreamConfig != null && !metricStreamConfig.ViewId.HasValue)
                        {
                            metricStreamConfig.ViewId = i;
                        }

                        // 检查是否为直方图配置且仪器类型不为直方图
                        if (metricStreamConfig is HistogramConfiguration
                            && instrument.GetType().GetGenericTypeDefinition() != typeof(Histogram<>))
                        {
                            metricStreamConfig = null;

                            OpenTelemetrySdkEventSource.Log.MetricViewIgnored(
                                instrument.Name,
                                instrument.Meter.Name,
                                "The current SDK does not allow aggregating non-Histogram instruments as Histograms.",
                                "Fix the view configuration.");
                        }
                    }
                    catch (Exception ex)
                    {
                        OpenTelemetrySdkEventSource.Log.MetricViewIgnored(instrument.Name, instrument.Meter.Name, ex.Message, "Fix the view configuration.");
                    }

                    if (metricStreamConfig != null)
                    {
                        metricStreamConfigs.Add(metricStreamConfig);
                    }
                }

                // 如果没有视图匹配，添加 null 以应用默认值
                if (metricStreamConfigs.Count == 0)
                {
                    metricStreamConfigs.Add(null);
                }

                // 如果有度量读取器
                if (this.reader != null)
                {
                    // 添加具有视图的度量
                    var metrics = this.reader.AddMetricWithViews(instrument, metricStreamConfigs);
                    if (metrics.Count == 1)
                    {
                        state = MetricState.BuildForSingleMetric(metrics[0]);
                    }
                    else if (metrics.Count > 0)
                    {
                        state = MetricState.BuildForMetricList(metrics);
                    }
                }
            }

            // 如果状态不为空，记录事件并返回状态
            if (state != null)
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Measurements for Instrument = \"{instrument.Name}\" of Meter = \"{instrument.Meter.Name}\" will be processed and aggregated by the SDK.");
                return state;
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Measurements for Instrument = \"{instrument.Name}\" of Meter = \"{instrument.Meter.Name}\" will be dropped by the SDK.");
                return null;
            }
        }
#if DEBUG
        catch (Exception ex)
        {
            throw new InvalidOperationException("SDK internal error occurred.", ex);
        }
#else
        catch (Exception)
        {
            OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(instrument.Name, instrument.Meter.Name, "SDK internal error occurred.", "Contact SDK owners.");
            return null;
        }
#endif
    }

    /// <summary>
    /// 收集可观察仪器的函数
    /// </summary>
    internal void CollectObservableInstruments()
    {
        // 锁定 collectLock 以确保线程安全
        lock (this.collectLock)
        {
            // 记录所有可观察的仪器
            try
            {
                // 调用 listener 的 RecordObservableInstruments 方法记录可观察仪器
                this.listener.RecordObservableInstruments();

                // 如果 OnCollectObservableInstruments 不为空，则调用它
                this.OnCollectObservableInstruments?.Invoke();
            }
            catch (Exception exception)
            {
                // TODO:
                // 看起来我们无法找到哪个仪器的回调抛出了异常。
                // 记录 MetricObserverCallbackException 事件
                OpenTelemetrySdkEventSource.Log.MetricObserverCallbackException(exception);
            }
        }
    }

    /// <summary>
    /// 由 <c>ForceFlush</c> 调用。此函数应阻塞当前线程直到刷新完成或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或<c>Timeout.Infinite</c>表示无限等待。
    /// </param>
    /// <returns>
    /// 返回<c>true</c>表示刷新成功；否则返回<c>false</c>。
    /// </returns>
    /// <remarks>
    /// 此函数在第一次调用 <c>ForceFlush</c> 的线程上同步调用。此函数不应抛出异常。
    /// </remarks>
    internal bool OnForceFlush(int timeoutMilliseconds)
    {
        // 记录 MeterProviderSdkEvent 事件，指示调用了 OnForceFlush 方法，并传递了 timeoutMilliseconds 参数。
        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"{nameof(MeterProviderSdk)}.{nameof(this.OnForceFlush)} called with {nameof(timeoutMilliseconds)} = {timeoutMilliseconds}.");

        // 调用 reader 的 Collect 方法，传递 timeoutMilliseconds 参数，并返回其结果。
        // 如果 reader 为 null，则返回 true。
        return this.reader?.Collect(timeoutMilliseconds) ?? true;
    }

    /// <summary>
    /// 由 <c>Shutdown</c> 调用。此函数应阻塞当前线程直到关闭完成或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或<c>Timeout.Infinite</c>表示无限等待。
    /// </param>
    /// <returns>
    /// 返回<c>true</c>表示关闭成功；否则返回<c>false</c>。
    /// </returns>
    /// <remarks>
    /// 此函数在第一次调用 <c>Shutdown</c> 的线程上同步调用。此函数不应抛出异常。
    /// </remarks>
    internal bool OnShutdown(int timeoutMilliseconds)
    {
        // 记录 MeterProviderSdkEvent 事件，指示调用了 OnShutdown 方法，并传递了 timeoutMilliseconds 参数。
        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"{nameof(MeterProviderSdk)}.{nameof(this.OnShutdown)} called with {nameof(timeoutMilliseconds)} = {timeoutMilliseconds}.");

        // 调用 reader 的 Shutdown 方法，传递 timeoutMilliseconds 参数，并返回其结果。
        // 如果 reader 为 null，则返回 true。
        return this.reader?.Shutdown(timeoutMilliseconds) ?? true;
    }

    /// <summary>
    /// 释放 MeterProviderSdk 类的资源。
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        // 记录 MeterProviderSdkEvent 事件，指示调用了 Dispose 方法。
        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"{nameof(MeterProviderSdk)}.{nameof(this.Dispose)} started.");

        // 检查是否已释放资源。
        if (!this.Disposed)
        {
            // 如果正在释放托管资源。
            if (disposing)
            {
                // 如果仪器列表不为空。
                if (this.instrumentations != null)
                {
                    // 释放每个仪器的资源。
                    foreach (var item in this.instrumentations)
                    {
                        (item as IDisposable)?.Dispose();
                    }

                    // 清空仪器列表。
                    this.instrumentations.Clear();
                }

                // 等待最多 5 秒的宽限期以关闭度量读取器。
                this.reader?.Shutdown(5000);
                // 释放度量读取器的资源。
                this.reader?.Dispose();
                // 释放复合度量读取器的资源。
                this.compositeMetricReader?.Dispose();

                // 释放监听器的资源。
                this.listener?.Dispose();

                // 释放拥有的服务提供者的资源。
                this.OwnedServiceProvider?.Dispose();
            }

            // 标记资源已释放。
            this.Disposed = true;
            // 记录 ProviderDisposed 事件，指示 MeterProvider 已释放。
            OpenTelemetrySdkEventSource.Log.ProviderDisposed(nameof(MeterProvider));
        }

        // 调用基类的 Dispose 方法。
        base.Dispose(disposing);
    }

    // 应用规范配置键
    private void ApplySpecificationConfigurationKeys(IConfiguration configuration)
    {
        // 检查是否有编程设置的示例过滤器值
        var hasProgrammaticExemplarFilterValue = this.ExemplarFilter.HasValue;

        // 尝试从配置中获取示例过滤器配置值
        if (configuration.TryGetStringValue(ExemplarFilterConfigKey, out var configValue))
        {
            // 如果有编程设置的示例过滤器值，则忽略配置中的值
            if (hasProgrammaticExemplarFilterValue)
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent(
                    $"示例过滤器配置值 '{configValue}' 已被忽略，因为已通过编程设置了值 '{this.ExemplarFilter}'。");
                return;
            }

            // 尝试解析配置值为示例过滤器类型
            if (!TryParseExemplarFilterFromConfigurationValue(configValue, out var exemplarFilter))
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"找到了示例过滤器配置，但值 '{configValue}' 无效，将被忽略。");
                return;
            }

            // 设置示例过滤器
            this.ExemplarFilter = exemplarFilter;

            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"示例过滤器从配置中设置为 '{exemplarFilter}'。");
        }

        // 尝试从配置中获取直方图的示例过滤器配置值
        if (configuration.TryGetStringValue(ExemplarFilterHistogramsConfigKey, out configValue))
        {
            // 如果有编程设置的示例过滤器值，则忽略配置中的值
            if (hasProgrammaticExemplarFilterValue)
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent(
                    $"直方图的示例过滤器配置值 '{configValue}' 已被忽略，因为已通过编程设置了值 '{this.ExemplarFilter}'。");
                return;
            }

            // 尝试解析配置值为示例过滤器类型
            if (!TryParseExemplarFilterFromConfigurationValue(configValue, out var exemplarFilter))
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"找到了直方图的示例过滤器配置，但值 '{configValue}' 无效，将被忽略。");
                return;
            }

            // 设置直方图的示例过滤器
            this.ExemplarFilterForHistograms = exemplarFilter;

            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"直方图的示例过滤器从配置中设置为 '{exemplarFilter}'。");
        }

        // 尝试解析配置值为示例过滤器类型
        static bool TryParseExemplarFilterFromConfigurationValue(string? configValue, out ExemplarFilterType? exemplarFilter)
        {
            // 检查配置值是否为 "always_off"
            if (string.Equals("always_off", configValue, StringComparison.OrdinalIgnoreCase))
            {
                exemplarFilter = ExemplarFilterType.AlwaysOff;
                return true;
            }

            // 检查配置值是否为 "always_on"
            if (string.Equals("always_on", configValue, StringComparison.OrdinalIgnoreCase))
            {
                exemplarFilter = ExemplarFilterType.AlwaysOn;
                return true;
            }

            // 检查配置值是否为 "trace_based"
            if (string.Equals("trace_based", configValue, StringComparison.OrdinalIgnoreCase))
            {
                exemplarFilter = ExemplarFilterType.TraceBased;
                return true;
            }

            // 如果配置值无效，则返回 null
            exemplarFilter = null;
            return false;
        }
    }
}
