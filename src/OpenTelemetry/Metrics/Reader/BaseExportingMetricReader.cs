// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// MetricReader 实现类，在 <see cref="MetricReader.Collect(int)"/> 时将度量导出到配置的 MetricExporter。
/// </summary>
public class BaseExportingMetricReader : MetricReader
{
    /// <summary>
    /// 获取度量读取器使用的导出器。
    /// </summary>
    protected readonly BaseExporter<Metric> exporter; // 导出器实例

    // 支持的导出模式，默认为 Push 和 Pull 模式
    private readonly ExportModes supportedExportModes = ExportModes.Push | ExportModes.Pull;
    // 导出调用消息
    private readonly string exportCalledMessage;
    // 导出成功消息
    private readonly string exportSucceededMessage;
    // 导出失败消息
    private readonly string exportFailedMessage;
    // 是否已释放
    private bool disposed;

    /// <summary>
    /// 构造函数，初始化 BaseExportingMetricReader 实例。
    /// </summary>
    /// <param name="exporter">用于导出度量的导出器实例。</param>
    public BaseExportingMetricReader(BaseExporter<Metric> exporter)
    {
        // 检查导出器是否为 null
        Guard.ThrowIfNull(exporter);

        // 将导出器实例赋值给类的成员变量
        this.exporter = exporter;

        // 获取导出器的类型
        var exporterType = exporter.GetType();
        // 获取导出器类型上的 ExportModesAttribute 特性
        var attributes = exporterType.GetCustomAttributes(typeof(ExportModesAttribute), true);
        if (attributes.Length > 0)
        {
            // 如果存在 ExportModesAttribute 特性，则获取其支持的导出模式
            var attr = (ExportModesAttribute)attributes[attributes.Length - 1];
            this.supportedExportModes = attr.Supported;
        }

        // 如果导出器实现了 IPullMetricExporter 接口
        if (exporter is IPullMetricExporter pullExporter)
        {
            // 如果支持 Push 模式，则将 Collect 方法赋值给 pullExporter.Collect
            if (this.supportedExportModes.HasFlag(ExportModes.Push))
            {
                pullExporter.Collect = this.Collect;
            }
            else
            {
                // 否则，使用 PullMetricScope 包装 Collect 方法
                pullExporter.Collect = (timeoutMilliseconds) =>
                {
                    using (PullMetricScope.Begin())
                    {
                        return this.Collect(timeoutMilliseconds);
                    }
                };
            }
        }

        // 初始化导出调用、成功和失败的消息
        this.exportCalledMessage = $"{nameof(BaseExportingMetricReader)} 调用 {this.Exporter}.{nameof(this.Exporter.Export)} 方法。";
        this.exportSucceededMessage = $"{this.Exporter}.{nameof(this.Exporter.Export)} 成功。";
        this.exportFailedMessage = $"{this.Exporter}.{nameof(this.Exporter.Export)} 失败。";
    }

    internal BaseExporter<Metric> Exporter => this.exporter; // 获取导出器实例

    /// <summary>
    /// 获取支持的 <see cref="ExportModes"/>。
    /// </summary>
    protected ExportModes SupportedExportModes => this.supportedExportModes; // 获取支持的导出模式

    /// <summary>
    /// 设置父提供者。
    /// </summary>
    /// <param name="parentProvider">父提供者实例。</param>
    internal override void SetParentProvider(BaseProvider parentProvider)
    {
        base.SetParentProvider(parentProvider);
        this.exporter.ParentProvider = parentProvider;
    }

    /// <summary>
    /// 处理度量数据。
    /// </summary>
    /// <param name="metrics">度量数据批次。</param>
    /// <param name="timeoutMilliseconds">超时时间（毫秒）。</param>
    /// <returns>处理成功返回 true，否则返回 false。</returns>
    internal override bool ProcessMetrics(in Batch<Metric> metrics, int timeoutMilliseconds)
    {
        // TODO: 这里是否需要考虑超时？
        try
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderEvent(this.exportCalledMessage);
            var result = this.exporter.Export(metrics);
            if (result == ExportResult.Success)
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent(this.exportSucceededMessage);
                return true;
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent(this.exportFailedMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.ProcessMetrics), ex);
            return false;
        }
    }

    /// <summary>
    /// 收集度量数据。
    /// </summary>
    /// <param name="timeoutMilliseconds">超时时间（毫秒）。</param>
    /// <returns>收集成功返回 true，否则返回 false。</returns>
    protected override bool OnCollect(int timeoutMilliseconds)
    {
        if (this.supportedExportModes.HasFlag(ExportModes.Push))
        {
            return base.OnCollect(timeoutMilliseconds);
        }

        if (this.supportedExportModes.HasFlag(ExportModes.Pull) && PullMetricScope.IsPullAllowed)
        {
            return base.OnCollect(timeoutMilliseconds);
        }

        // TODO: 添加一些错误日志
        return false;
    }

    /// <summary>
    /// 关闭度量读取器。
    /// </summary>
    /// <param name="timeoutMilliseconds">超时时间（毫秒）。</param>
    /// <returns>关闭成功返回 true，否则返回 false。</returns>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var result = true;

        if (timeoutMilliseconds == Timeout.Infinite)
        {
            result = this.Collect(Timeout.Infinite) && result;
            result = this.exporter.Shutdown(Timeout.Infinite) && result;
        }
        else
        {
            var sw = Stopwatch.StartNew();
            result = this.Collect(timeoutMilliseconds) && result;
            var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;
            result = this.exporter.Shutdown((int)Math.Max(timeout, 0)) && result;
        }

        return result;
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    /// <param name="disposing">是否释放托管资源。</param>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                try
                {
                    if (this.exporter is IPullMetricExporter pullExporter)
                    {
                        pullExporter.Collect = null;
                    }

                    this.exporter.Dispose();
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.Dispose), ex);
                }
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }
}
