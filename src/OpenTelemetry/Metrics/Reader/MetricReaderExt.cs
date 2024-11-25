// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// MetricReader 基类。
/// </summary>
public abstract partial class MetricReader
{
    // 存储度量流名称的集合，忽略大小写
    private readonly HashSet<string> metricStreamNames = new(StringComparer.OrdinalIgnoreCase);
    // 并发字典，用于存储度量流标识和度量对象的映射关系
    private readonly ConcurrentDictionary<MetricStreamIdentity, Metric?> instrumentIdentityToMetric = new();
    // 锁对象，用于度量创建时的同步
    private readonly Lock instrumentCreationLock = new();
    // 度量限制
    private int metricLimit;
    // 基数限制
    private int cardinalityLimit;
    // 度量数组
    private Metric?[]? metrics;
    // 当前批次的度量数组
    private Metric[]? metricsCurrentBatch;
    // 度量索引
    private int metricIndex = -1;
    // 示例过滤器
    private ExemplarFilterType? exemplarFilter;
    // 直方图的示例过滤器
    private ExemplarFilterType? exemplarFilterForHistograms;

    /// <summary>
    /// 停用度量。
    /// </summary>
    /// <param name="metric">要停用的度量。</param>
    internal static void DeactivateMetric(Metric metric)
    {
        if (metric.Active)
        {
            // TODO: 这将在下次收集/导出期间导致度量从存储数组中删除。如果这种情况经常发生，我们将耗尽存储空间。
            // 是否更好地设置度量的结束时间并保留它，以便可以重新激活？
            metric.Active = false;

            OpenTelemetrySdkEventSource.Log.MetricInstrumentDeactivated(
                metric.Name,
                metric.MeterName);
        }
    }

    /// <summary>
    /// 获取聚合时间间隔。
    /// </summary>
    /// <param name="instrumentType">仪器类型。</param>
    /// <returns>聚合时间间隔。</returns>
    internal AggregationTemporality GetAggregationTemporality(Type instrumentType)
    {
        return this.temporalityFunc(instrumentType);
    }

    /// <summary>
    /// 添加没有视图的度量。
    /// </summary>
    /// <param name="instrument">仪器。</param>
    /// <returns>度量列表。</returns>
    internal virtual List<Metric> AddMetricWithNoViews(Instrument instrument)
    {
        Debug.Assert(instrument != null, "instrument was null");
        Debug.Assert(this.metrics != null, "this.metrics was null");

        var metricStreamIdentity = new MetricStreamIdentity(instrument!, metricStreamConfiguration: null);

        var exemplarFilter = metricStreamIdentity.IsHistogram
            ? this.exemplarFilterForHistograms ?? this.exemplarFilter
            : this.exemplarFilter;

        lock (this.instrumentCreationLock)
        {
            if (this.TryGetExistingMetric(in metricStreamIdentity, out var existingMetric))
            {
                return new() { existingMetric };
            }

            var index = ++this.metricIndex;
            if (index >= this.metricLimit)
            {
                OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricStreamIdentity.InstrumentName, metricStreamIdentity.MeterName, "Maximum allowed Metric streams for the provider exceeded.", "Use MeterProviderBuilder.AddView to drop unused instruments. Or use MeterProviderBuilder.SetMaxMetricStreams to configure MeterProvider to allow higher limit.");
                return new();
            }
            else
            {
                Metric? metric = null;
                try
                {
                    metric = new Metric(
                        metricStreamIdentity,
                        this.GetAggregationTemporality(metricStreamIdentity.InstrumentType),
                        this.cardinalityLimit,
                        exemplarFilter);
                }
                catch (NotSupportedException nse)
                {
                    // TODO: 即使没有监听，这也会分配字符串。可以通过单独的事件来改进。
                    // 此外，消息可以指出支持哪些仪器和类型（例如：int、long等）。
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricStreamIdentity.InstrumentName, metricStreamIdentity.MeterName, "Unsupported instrument. Details: " + nse.Message, "Switch to a supported instrument type.");
                    return new();
                }

                this.instrumentIdentityToMetric[metricStreamIdentity] = metric;
                this.metrics![index] = metric;

                this.CreateOrUpdateMetricStreamRegistration(in metricStreamIdentity);

                return new() { metric };
            }
        }
    }

    /// <summary>
    /// 添加具有视图的度量。
    /// </summary>
    /// <param name="instrument">仪器。</param>
    /// <param name="metricStreamConfigs">度量流配置列表。</param>
    /// <returns>度量列表。</returns>
    internal virtual List<Metric> AddMetricWithViews(Instrument instrument, List<MetricStreamConfiguration?> metricStreamConfigs)
    {
        Debug.Assert(instrument != null, "instrument was null");
        Debug.Assert(metricStreamConfigs != null, "metricStreamConfigs was null");
        Debug.Assert(this.metrics != null, "this.metrics was null");

        var maxCountMetricsToBeCreated = metricStreamConfigs!.Count;

        // 创建初始容量为最大度量计数的列表。由于重复/最大限制，我们可能不会全部使用它们，并且该内存在 Meter 释放之前是浪费的。
        // TODO: 重新审视是否需要执行 metrics.TrimExcess()
        var metrics = new List<Metric>(maxCountMetricsToBeCreated);
        lock (this.instrumentCreationLock)
        {
            for (int i = 0; i < maxCountMetricsToBeCreated; i++)
            {
                var metricStreamConfig = metricStreamConfigs[i];
                var metricStreamIdentity = new MetricStreamIdentity(instrument!, metricStreamConfig);

                var exemplarFilter = metricStreamIdentity.IsHistogram
                    ? this.exemplarFilterForHistograms ?? this.exemplarFilter
                    : this.exemplarFilter;

                if (!MeterProviderBuilderSdk.IsValidInstrumentName(metricStreamIdentity.InstrumentName))
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                        metricStreamIdentity.InstrumentName,
                        metricStreamIdentity.MeterName,
                        "Metric name is invalid.",
                        "The name must comply with the OpenTelemetry specification.");

                    continue;
                }

                if (this.TryGetExistingMetric(in metricStreamIdentity, out var existingMetric))
                {
                    metrics.Add(existingMetric);
                    continue;
                }

                if (metricStreamConfig == MetricStreamConfiguration.Drop)
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricStreamIdentity.InstrumentName, metricStreamIdentity.MeterName, "View configuration asks to drop this instrument.", "Modify view configuration to allow this instrument, if desired.");
                    continue;
                }

                var index = ++this.metricIndex;
                if (index >= this.metricLimit)
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricStreamIdentity.InstrumentName, metricStreamIdentity.MeterName, "Maximum allowed Metric streams for the provider exceeded.", "Use MeterProviderBuilder.AddView to drop unused instruments. Or use MeterProviderBuilder.SetMaxMetricStreams to configure MeterProvider to allow higher limit.");
                }
                else
                {
                    Metric metric = new(
                        metricStreamIdentity,
                        this.GetAggregationTemporality(metricStreamIdentity.InstrumentType),
                        metricStreamConfig?.CardinalityLimit ?? this.cardinalityLimit,
                        exemplarFilter,
                        metricStreamConfig?.ExemplarReservoirFactory);

                    this.instrumentIdentityToMetric[metricStreamIdentity] = metric;
                    this.metrics![index] = metric;
                    metrics.Add(metric);

                    this.CreateOrUpdateMetricStreamRegistration(in metricStreamIdentity);
                }
            }

            return metrics;
        }
    }

    /// <summary>
    /// 应用父提供程序设置。
    /// </summary>
    /// <param name="metricLimit">度量限制。</param>
    /// <param name="cardinalityLimit">基数限制。</param>
    /// <param name="exemplarFilter">示例过滤器。</param>
    /// <param name="exemplarFilterForHistograms">直方图的示例过滤器。</param>
    internal void ApplyParentProviderSettings(
        int metricLimit,
        int cardinalityLimit,
        ExemplarFilterType? exemplarFilter,
        ExemplarFilterType? exemplarFilterForHistograms)
    {
        this.metricLimit = metricLimit;
        this.metrics = new Metric[metricLimit];
        this.metricsCurrentBatch = new Metric[metricLimit];
        this.cardinalityLimit = cardinalityLimit;
        this.exemplarFilter = exemplarFilter;
        this.exemplarFilterForHistograms = exemplarFilterForHistograms;
    }

    /// <summary>
    /// 尝试获取现有度量。
    /// </summary>
    /// <param name="metricStreamIdentity">度量流标识。</param>
    /// <param name="existingMetric">现有度量。</param>
    /// <returns>如果找到现有度量，则返回 true；否则返回 false。</returns>
    private bool TryGetExistingMetric(in MetricStreamIdentity metricStreamIdentity, [NotNullWhen(true)] out Metric? existingMetric)
        => this.instrumentIdentityToMetric.TryGetValue(metricStreamIdentity, out existingMetric)
            && existingMetric != null && existingMetric.Active;

    /// <summary>
    /// 创建或更新度量流注册。
    /// </summary>
    /// <param name="metricStreamIdentity">度量流标识。</param>
    private void CreateOrUpdateMetricStreamRegistration(in MetricStreamIdentity metricStreamIdentity)
    {
        if (!this.metricStreamNames.Add(metricStreamIdentity.MetricStreamName))
        {
            // TODO: 如果度量被停用然后重新激活，我们会记录与重复度量相同的警告。
            OpenTelemetrySdkEventSource.Log.DuplicateMetricInstrument(
                metricStreamIdentity.InstrumentName,
                metricStreamIdentity.MeterName,
                "Metric instrument has the same name as an existing one but differs by description, unit, or instrument type. Measurements from this instrument will still be exported but may result in conflicts.",
                "Either change the name of the instrument or use MeterProviderBuilder.AddView to resolve the conflict.");
        }
    }

    /// <summary>
    /// 获取度量批次。
    /// </summary>
    /// <returns>度量批次。</returns>
    private Batch<Metric> GetMetricsBatch()
    {
        //Debug.Assert(this.metrics != null, "this.metrics was null");
        //Debug.Assert(this.metricsCurrentBatch != null, "this.metricsCurrentBatch was null");

        try
        {
            var indexSnapshot = Math.Min(this.metricIndex, this.metricLimit - 1);
            var target = indexSnapshot + 1;
            int metricCountCurrentBatch = 0;
            for (int i = 0; i < target; i++)
            {
                ref var metric = ref this.metrics![i];
                if (metric != null)
                {
                    int metricPointSize = metric.Snapshot();

                    if (metricPointSize > 0)
                    {
                        this.metricsCurrentBatch![metricCountCurrentBatch++] = metric;
                    }

                    if (!metric.Active)
                    {
                        this.RemoveMetric(ref metric);
                    }
                }
            }

            return (metricCountCurrentBatch > 0) ? new Batch<Metric>(this.metricsCurrentBatch!, metricCountCurrentBatch) : default;
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.GetMetricsBatch), ex);
            return default;
        }
    }

    /// <summary>
    /// 移除度量。
    /// </summary>
    /// <param name="metric">要移除的度量。</param>
    private void RemoveMetric(ref Metric? metric)
    {
        Debug.Assert(metric != null, "metric was null");

        // TODO: 这个逻辑会移除度量。如果同一个度量再次发布，我们将为其创建一个新的度量。
        // 如果这种情况经常发生，我们将耗尽存储空间。相反，我们是否应该保留度量并设置新的开始时间+重置其数据，如果它回来？

        OpenTelemetrySdkEventSource.Log.MetricInstrumentRemoved(metric!.Name, metric.MeterName);

        // 注意：这里使用 TryUpdate 而不是 TryRemove，因为存在竞争条件。如果度量在同一个收集周期内被停用然后重新激活，
        // instrumentIdentityToMetric[metric.InstrumentIdentity] 可能已经指向新的激活度量，而不是旧的停用度量。
        this.instrumentIdentityToMetric.TryUpdate(metric.InstrumentIdentity, null, metric);

        // 注意：metric 是数组存储的引用，因此这会清除数组中的度量。
        metric = null;
    }
}
