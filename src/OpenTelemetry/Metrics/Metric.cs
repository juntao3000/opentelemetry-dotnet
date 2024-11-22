// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 表示一个度量流，可以包含多个度量点。
/// </summary>
public sealed class Metric
{
    // 默认的指数直方图最大桶数
    internal const int DefaultExponentialHistogramMaxBuckets = 160;

    // 默认的指数直方图最大比例
    internal const int DefaultExponentialHistogramMaxScale = 20;

    // 默认的直方图边界
    internal static readonly double[] DefaultHistogramBounds = new double[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000 };

    // 短默认直方图边界。基于 http.server.request.duration 的推荐语义约定值。
    internal static readonly double[] DefaultHistogramBoundsShortSeconds = new double[] { 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 };
    internal static readonly
#if NET
        FrozenSet<(string, string)>
#else
    HashSet<(string, string)>
#endif
    // 短默认直方图边界的映射
    DefaultHistogramBoundShortMappings = new HashSet<(string, string)>
    {
            ("Microsoft.AspNetCore.Hosting", "http.server.request.duration"),
            ("Microsoft.AspNetCore.RateLimiting", "aspnetcore.rate_limiting.request.time_in_queue"),
            ("Microsoft.AspNetCore.RateLimiting", "aspnetcore.rate_limiting.request_lease.duration"),
            ("Microsoft.AspNetCore.Server.Kestrel", "kestrel.tls_handshake.duration"),
            ("OpenTelemetry.Instrumentation.AspNet", "http.server.request.duration"),
            ("OpenTelemetry.Instrumentation.AspNetCore", "http.server.request.duration"),
            ("OpenTelemetry.Instrumentation.Http", "http.client.request.duration"),
            ("System.Net.Http", "http.client.request.duration"),
            ("System.Net.Http", "http.client.request.time_in_queue"),
            ("System.Net.NameResolution", "dns.lookup.duration"),
    }
#if NET
        .ToFrozenSet()
#endif
    ;

    // 长默认直方图边界。不是基于标准的。将来可能会改变。
    internal static readonly double[] DefaultHistogramBoundsLongSeconds = new double[] { 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 30, 60, 120, 300 };
    internal static readonly
#if NET
        FrozenSet<(string, string)>
#else
    HashSet<(string, string)>
#endif
    // 长默认直方图边界的映射
    DefaultHistogramBoundLongMappings = new HashSet<(string, string)>
    {
            ("Microsoft.AspNetCore.Http.Connections", "signalr.server.connection.duration"),
            ("Microsoft.AspNetCore.Server.Kestrel", "kestrel.connection.duration"),
            ("System.Net.Http", "http.client.connection.duration"),
    }
#if NET
        .ToFrozenSet()
#endif
    ;

    // 聚合器存储
    internal readonly AggregatorStore AggregatorStore;

    /// <summary>
    /// Metric 构造函数
    /// </summary>
    /// <param name="instrumentIdentity">度量流标识</param>
    /// <param name="temporality">聚合时间类型</param>
    /// <param name="cardinalityLimit">基数限制</param>
    /// <param name="exemplarFilter">示例过滤器类型（可选）</param>
    /// <param name="exemplarReservoirFactory">示例库工厂方法（可选）</param>
    internal Metric(
        MetricStreamIdentity instrumentIdentity,
        AggregationTemporality temporality,
        int cardinalityLimit,
        ExemplarFilterType? exemplarFilter = null,
        Func<ExemplarReservoir?>? exemplarReservoirFactory = null)
    {
        this.InstrumentIdentity = instrumentIdentity;

        AggregationType aggType;
        if (instrumentIdentity.InstrumentType == typeof(ObservableCounter<long>)
            || instrumentIdentity.InstrumentType == typeof(ObservableCounter<int>)
            || instrumentIdentity.InstrumentType == typeof(ObservableCounter<short>)
            || instrumentIdentity.InstrumentType == typeof(ObservableCounter<byte>))
        {
            aggType = AggregationType.LongSumIncomingCumulative;
            this.MetricType = MetricType.LongSum;
        }
        else if (instrumentIdentity.InstrumentType == typeof(Counter<long>)
            || instrumentIdentity.InstrumentType == typeof(Counter<int>)
            || instrumentIdentity.InstrumentType == typeof(Counter<short>)
            || instrumentIdentity.InstrumentType == typeof(Counter<byte>))
        {
            aggType = AggregationType.LongSumIncomingDelta;
            this.MetricType = MetricType.LongSum;
        }
        else if (instrumentIdentity.InstrumentType == typeof(Counter<double>)
            || instrumentIdentity.InstrumentType == typeof(Counter<float>))
        {
            aggType = AggregationType.DoubleSumIncomingDelta;
            this.MetricType = MetricType.DoubleSum;
        }
        else if (instrumentIdentity.InstrumentType == typeof(ObservableCounter<double>)
            || instrumentIdentity.InstrumentType == typeof(ObservableCounter<float>))
        {
            aggType = AggregationType.DoubleSumIncomingCumulative;
            this.MetricType = MetricType.DoubleSum;
        }
        else if (instrumentIdentity.InstrumentType == typeof(ObservableUpDownCounter<long>)
            || instrumentIdentity.InstrumentType == typeof(ObservableUpDownCounter<int>)
            || instrumentIdentity.InstrumentType == typeof(ObservableUpDownCounter<short>)
            || instrumentIdentity.InstrumentType == typeof(ObservableUpDownCounter<byte>))
        {
            aggType = AggregationType.LongSumIncomingCumulative;
            this.MetricType = MetricType.LongSumNonMonotonic;
        }
        else if (instrumentIdentity.InstrumentType == typeof(UpDownCounter<long>)
            || instrumentIdentity.InstrumentType == typeof(UpDownCounter<int>)
            || instrumentIdentity.InstrumentType == typeof(UpDownCounter<short>)
            || instrumentIdentity.InstrumentType == typeof(UpDownCounter<byte>))
        {
            aggType = AggregationType.LongSumIncomingDelta;
            this.MetricType = MetricType.LongSumNonMonotonic;
        }
        else if (instrumentIdentity.InstrumentType == typeof(UpDownCounter<double>)
            || instrumentIdentity.InstrumentType == typeof(UpDownCounter<float>))
        {
            aggType = AggregationType.DoubleSumIncomingDelta;
            this.MetricType = MetricType.DoubleSumNonMonotonic;
        }
        else if (instrumentIdentity.InstrumentType == typeof(ObservableUpDownCounter<double>)
            || instrumentIdentity.InstrumentType == typeof(ObservableUpDownCounter<float>))
        {
            aggType = AggregationType.DoubleSumIncomingCumulative;
            this.MetricType = MetricType.DoubleSumNonMonotonic;
        }
        else if (instrumentIdentity.InstrumentType == typeof(ObservableGauge<double>)
            || instrumentIdentity.InstrumentType == typeof(ObservableGauge<float>))
        {
            aggType = AggregationType.DoubleGauge;
            this.MetricType = MetricType.DoubleGauge;
        }
        else if (instrumentIdentity.InstrumentType == typeof(Gauge<double>)
            || instrumentIdentity.InstrumentType == typeof(Gauge<float>))
        {
            aggType = AggregationType.DoubleGauge;
            this.MetricType = MetricType.DoubleGauge;
        }
        else if (instrumentIdentity.InstrumentType == typeof(ObservableGauge<long>)
            || instrumentIdentity.InstrumentType == typeof(ObservableGauge<int>)
            || instrumentIdentity.InstrumentType == typeof(ObservableGauge<short>)
            || instrumentIdentity.InstrumentType == typeof(ObservableGauge<byte>))
        {
            aggType = AggregationType.LongGauge;
            this.MetricType = MetricType.LongGauge;
        }
        else if (instrumentIdentity.InstrumentType == typeof(Gauge<long>)
            || instrumentIdentity.InstrumentType == typeof(Gauge<int>)
            || instrumentIdentity.InstrumentType == typeof(Gauge<short>)
            || instrumentIdentity.InstrumentType == typeof(Gauge<byte>))
        {
            aggType = AggregationType.LongGauge;
            this.MetricType = MetricType.LongGauge;
        }
        else if (instrumentIdentity.IsHistogram)
        {
            var explicitBucketBounds = instrumentIdentity.HistogramBucketBounds;
            var exponentialMaxSize = instrumentIdentity.ExponentialHistogramMaxSize;
            var histogramRecordMinMax = instrumentIdentity.HistogramRecordMinMax;

            this.MetricType = exponentialMaxSize == 0
                ? MetricType.Histogram
                : MetricType.ExponentialHistogram;

            if (this.MetricType == MetricType.Histogram)
            {
                aggType = explicitBucketBounds != null && explicitBucketBounds.Length == 0
                    ? (histogramRecordMinMax ? AggregationType.HistogramWithMinMax : AggregationType.Histogram)
                    : (histogramRecordMinMax ? AggregationType.HistogramWithMinMaxBuckets : AggregationType.HistogramWithBuckets);
            }
            else
            {
                aggType = histogramRecordMinMax ? AggregationType.Base2ExponentialHistogramWithMinMax : AggregationType.Base2ExponentialHistogram;
            }
        }
        else
        {
            throw new NotSupportedException($"Unsupported Instrument Type: {instrumentIdentity.InstrumentType.FullName}");
        }

        this.AggregatorStore = new AggregatorStore(
            instrumentIdentity,
            aggType,
            temporality,
            cardinalityLimit,
            exemplarFilter,
            exemplarReservoirFactory);
        this.Temporality = temporality;
    }

    /// <summary>
    /// 获取度量流的 <see cref="Metrics.MetricType"/>。
    /// </summary>
    public MetricType MetricType { get; private set; }

    /// <summary>
    /// 获取度量流的 <see cref="AggregationTemporality"/>。
    /// </summary>
    public AggregationTemporality Temporality { get; private set; }

    /// <summary>
    /// 获取度量流的名称。
    /// </summary>
    public string Name => this.InstrumentIdentity.InstrumentName;

    /// <summary>
    /// 获取度量流的描述。
    /// </summary>
    public string Description => this.InstrumentIdentity.Description;

    /// <summary>
    /// 获取度量流的单位。
    /// </summary>
    public string Unit => this.InstrumentIdentity.Unit;

    /// <summary>
    /// 获取度量流的仪表名称。
    /// </summary>
    public string MeterName => this.InstrumentIdentity.MeterName;

    /// <summary>
    /// 获取度量流的仪表版本。
    /// </summary>
    public string MeterVersion => this.InstrumentIdentity.MeterVersion;

    /// <summary>
    /// 获取度量流的属性（标签）。
    /// </summary>
    public IEnumerable<KeyValuePair<string, object?>>? MeterTags => this.InstrumentIdentity.MeterTags;

    /// <summary>
    /// 获取度量流的 <see cref="MetricStreamIdentity"/>。
    /// </summary>
    internal MetricStreamIdentity InstrumentIdentity { get; private set; }

    // 是否激活
    internal bool Active { get; set; } = true;

    /// <summary>
    /// 获取度量流的度量点。
    /// </summary>
    /// <returns><see cref="MetricPointsAccessor"/>。</returns>
    public MetricPointsAccessor GetMetricPoints()
        => this.AggregatorStore.GetMetricPoints();

    /// <summary>
    /// 更新长整型值
    /// </summary>
    /// <param name="value">长整型值</param>
    /// <param name="tags">标签</param>
    internal void UpdateLong(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => this.AggregatorStore.Update(value, tags);

    /// <summary>
    /// 更新双精度值
    /// </summary>
    /// <param name="value">双精度值</param>
    /// <param name="tags">标签</param>
    internal void UpdateDouble(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => this.AggregatorStore.Update(value, tags);

    /// <summary>
    /// 生成当前度量点的快照
    /// </summary>
    /// <returns>批次大小</returns>
    internal int Snapshot()
        => this.AggregatorStore.Snapshot();
}
