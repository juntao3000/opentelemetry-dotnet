// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/*
 MetricPoint 结构体表示一个度量数据点。它包含了度量数据点的各种属性和方法，用于管理和操作度量数据。以下是它的主要作用和功能：
1.	度量数据存储：MetricPoint 结构体存储了度量数据点的各种值，包括运行时值、快照值和增量值。
2.	度量类型管理：通过 aggType 字段，MetricPoint 可以区分不同类型的度量（如直方图、计数器、仪表等），并根据类型执行相应的操作。
3.	标签管理：Tags 属性存储了与度量数据点相关的标签，这些标签用于标识和区分不同的度量数据点。
4.	快照和更新：MetricPoint 提供了多种方法用于更新度量数据点的值（如 Update 和 UpdateWithExemplar 方法），以及生成度量数据点的快照（如 TakeSnapshot 和 TakeSnapshotWithExemplar 方法）。
5.	示例管理：通过 ExemplarReservoir 和相关方法，MetricPoint 可以管理和存储度量数据点的示例（Exemplar），这些示例用于记录特定条件下的度量值。
6.	线程安全：MetricPoint 结构体使用了锁和原子操作（如 Interlocked 类）来确保在多线程环境下对度量数据点的操作是线程安全的。
总的来说，MetricPoint 结构体是一个用于表示和管理度量数据点的核心组件，提供了丰富的功能来处理不同类型的度量数据，并确保在多线程环境下的安全性和高效性。
 */
/// <summary>
/// Represents a metric data point.
/// </summary>
public struct MetricPoint
{
    // 表示在任何给定时间使用此 MetricPoint 的更新线程数。
    // 如果值等于 int.MinValue（-2147483648），则表示此 MetricPoint 可供重用。
    // 我们从不增加没有标签（index == 0）和溢出属性的 MetricPoint 的 ReferenceCount，
    // 但我们总是减少它（在 Update 方法中）。这应该没问题。
    // 对于没有标签和溢出属性的 MetricPoint，ReferenceCount 无关紧要，因为它们从不被回收。
    internal int ReferenceCount;

    // 默认的简单示例库池大小
    private const int DefaultSimpleReservoirPoolSize = 1;

    // 聚合存储
    private readonly AggregatorStore aggregatorStore;

    // 聚合类型
    private readonly AggregationType aggType;

    // MetricPoint 的可选组件
    private MetricPointOptionalComponents? mpComponents;

    // 表示双精度/长整型度量类型的时间调整“值”或直方图的“计数”
    private MetricPointValueStorage runningValue;

    // 表示双精度/长整型度量类型的“值”或直方图的“计数”
    private MetricPointValueStorage snapshotValue;

    // 上次增量值
    private MetricPointValueStorage deltaLastValue;

    // MetricPoint 构造函数
    internal MetricPoint(
        AggregatorStore aggregatorStore,
        AggregationType aggType,
        KeyValuePair<string, object?>[]? tagKeysAndValues,
        double[] histogramExplicitBounds,
        int exponentialHistogramMaxSize,
        int exponentialHistogramMaxScale,
        LookupData? lookupData = null)
    {
        Debug.Assert(aggregatorStore != null, "AggregatorStore 为空。");
        Debug.Assert(histogramExplicitBounds != null, "直方图显式边界为空。");
        Debug.Assert(!aggregatorStore!.OutputDelta || lookupData != null, "LookupData 为空。");

        this.aggType = aggType;
        this.Tags = new ReadOnlyTagCollection(tagKeysAndValues);
        this.runningValue = default;
        this.snapshotValue = default;
        this.deltaLastValue = default;
        this.MetricPointStatus = MetricPointStatus.NoCollectPending;
        this.ReferenceCount = 1;
        this.LookupData = lookupData;

        var isExemplarEnabled = aggregatorStore!.IsExemplarEnabled();

        ExemplarReservoir? reservoir;
        try
        {
            reservoir = isExemplarEnabled
                ? aggregatorStore.ExemplarReservoirFactory?.Invoke()
                : null;
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.MetricViewException("ExemplarReservoirFactory", ex);
            reservoir = null;
        }

        if (this.aggType == AggregationType.HistogramWithBuckets ||
            this.aggType == AggregationType.HistogramWithMinMaxBuckets)
        {
            this.mpComponents = new MetricPointOptionalComponents();
            this.mpComponents.HistogramBuckets = new HistogramBuckets(histogramExplicitBounds);
            if (isExemplarEnabled && reservoir == null)
            {
                reservoir = new AlignedHistogramBucketExemplarReservoir(histogramExplicitBounds!.Length);
            }
        }
        else if (this.aggType == AggregationType.Histogram ||
                 this.aggType == AggregationType.HistogramWithMinMax)
        {
            this.mpComponents = new MetricPointOptionalComponents();
            this.mpComponents.HistogramBuckets = new HistogramBuckets(null);
        }
        else if (this.aggType == AggregationType.Base2ExponentialHistogram ||
            this.aggType == AggregationType.Base2ExponentialHistogramWithMinMax)
        {
            this.mpComponents = new MetricPointOptionalComponents();
            this.mpComponents.Base2ExponentialBucketHistogram = new Base2ExponentialBucketHistogram(exponentialHistogramMaxSize, exponentialHistogramMaxScale);
            if (isExemplarEnabled && reservoir == null)
            {
                reservoir = new SimpleFixedSizeExemplarReservoir(Math.Min(20, exponentialHistogramMaxSize));
            }
        }
        else
        {
            this.mpComponents = null;
        }

        if (isExemplarEnabled && reservoir == null)
        {
            reservoir = new SimpleFixedSizeExemplarReservoir(DefaultSimpleReservoirPoolSize);
        }

        if (reservoir != null)
        {
            if (this.mpComponents == null)
            {
                this.mpComponents = new MetricPointOptionalComponents();
            }

            reservoir.Initialize(aggregatorStore);

            this.mpComponents.ExemplarReservoir = reservoir;
        }

        // 注意：故意最后设置，因为这用于检测有效的 MetricPoints。
        this.aggregatorStore = aggregatorStore;
    }

    /// <summary>
    /// 获取与度量点关联的标签。
    /// </summary>
    public readonly ReadOnlyTagCollection Tags
    {
        // MethodImplOptions.AggressiveInlining 表示尽量内联以提高性能
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    /// <summary>
    /// 获取与度量点关联的开始时间（UTC）。
    /// </summary>
    public readonly DateTimeOffset StartTime => this.aggregatorStore.StartTimeExclusive;

    /// <summary>
    /// 获取与度量点关联的结束时间（UTC）。
    /// </summary>
    public readonly DateTimeOffset EndTime => this.aggregatorStore.EndTimeInclusive;

    /// <summary>
    /// 度量点的状态。
    /// </summary>
    internal MetricPointStatus MetricPointStatus
    {
        // MethodImplOptions.AggressiveInlining 表示尽量内联以提高性能
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get;

        // MethodImplOptions.AggressiveInlining 表示尽量内联以提高性能
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private set;
    }

    /// <summary>
    /// 当 AggregatorStore 回收 MetricPoints 时，这用于验证给定线程是否使用正确的 MetricPoint 进行更新，方法是将其与字典中添加的内容进行检查。
    /// 此外，当线程发现其使用的 MetricPoint 已被回收时，这有助于避免对标签进行排序以添加新的字典条目。
    /// Snapshot 方法可以使用它来跳过尝试回收已被回收并添加到队列中的索引。
    /// </summary>
    internal LookupData? LookupData { readonly get; private set; }

    /// <summary>
    /// 检查度量点是否已初始化。
    /// </summary>
    internal readonly bool IsInitialized => this.aggregatorStore != null;

    /// <summary>
    /// 获取与度量点关联的长整型总和值。
    /// </summary>
    /// <remarks>
    /// 适用于 <see cref="MetricType.LongSum"/> 度量类型。
    /// </remarks>
    /// <returns>长整型总和值。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly long GetSumLong()
    {
        if (this.aggType != AggregationType.LongSumIncomingDelta && this.aggType != AggregationType.LongSumIncomingCumulative)
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetSumLong));
        }

        return this.snapshotValue.AsLong;
    }

    /// <summary>
    /// 获取与度量点关联的双精度总和值。
    /// </summary>
    /// <remarks>
    /// 适用于 <see cref="MetricType.DoubleSum"/> 度量类型。
    /// </remarks>
    /// <returns>双精度总和值。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double GetSumDouble()
    {
        if (this.aggType != AggregationType.DoubleSumIncomingDelta && this.aggType != AggregationType.DoubleSumIncomingCumulative)
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetSumDouble));
        }

        return this.snapshotValue.AsDouble;
    }

    /// <summary>
    /// 获取与度量点关联的最后一个长整型仪表值。
    /// </summary>
    /// <remarks>
    /// 适用于 <see cref="MetricType.LongGauge"/> 度量类型。
    /// </remarks>
    /// <returns>长整型仪表值。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly long GetGaugeLastValueLong()
    {
        if (this.aggType != AggregationType.LongGauge)
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetGaugeLastValueLong));
        }

        return this.snapshotValue.AsLong;
    }

    /// <summary>
    /// 获取与度量点关联的最后一个双精度仪表值。
    /// </summary>
    /// <remarks>
    /// 适用于 <see cref="MetricType.DoubleGauge"/> 度量类型。
    /// </remarks>
    /// <returns>双精度仪表值。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double GetGaugeLastValueDouble()
    {
        if (this.aggType != AggregationType.DoubleGauge)
        {
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetGaugeLastValueDouble));
        }

        return this.snapshotValue.AsDouble;
    }

    /// <summary>
    /// 获取与度量点关联的直方图的计数值。
    /// </summary>
    /// <remarks>
    /// 适用于 <see cref="MetricType.Histogram"/> 度量类型。
    /// </remarks>
    /// <returns>计数值。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly long GetHistogramCount()
    {
        // 检查当前聚合类型是否为直方图类型
        if (this.aggType != AggregationType.HistogramWithBuckets &&
            this.aggType != AggregationType.Histogram &&
            this.aggType != AggregationType.HistogramWithMinMaxBuckets &&
            this.aggType != AggregationType.HistogramWithMinMax &&
            this.aggType != AggregationType.Base2ExponentialHistogram &&
            this.aggType != AggregationType.Base2ExponentialHistogramWithMinMax)
        {
            // 如果不是直方图类型，抛出不支持的度量类型异常
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetHistogramCount));
        }

        // 返回快照值的长整型值
        return this.snapshotValue.AsLong;
    }

    /// <summary>
    /// 获取与度量点关联的直方图的总和值。
    /// </summary>
    /// <remarks>
    /// 适用于 <see cref="MetricType.Histogram"/> 度量类型。
    /// </remarks>
    /// <returns>总和值。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double GetHistogramSum()
    {
        // 检查当前聚合类型是否为直方图类型
        if (this.aggType != AggregationType.HistogramWithBuckets &&
            this.aggType != AggregationType.Histogram &&
            this.aggType != AggregationType.HistogramWithMinMaxBuckets &&
            this.aggType != AggregationType.HistogramWithMinMax &&
            this.aggType != AggregationType.Base2ExponentialHistogram &&
            this.aggType != AggregationType.Base2ExponentialHistogramWithMinMax)
        {
            // 如果不是直方图类型，抛出不支持的度量类型异常
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetHistogramSum));
        }

        // 确保 HistogramBuckets 和 Base2ExponentialBucketHistogram 不同时为空
        Debug.Assert(
            this.mpComponents?.HistogramBuckets != null
            || this.mpComponents?.Base2ExponentialBucketHistogram != null,
            "HistogramBuckets 和 Base2ExponentialBucketHistogram 都为空");

        // 返回快照总和值
        return this.mpComponents!.HistogramBuckets != null
            ? this.mpComponents.HistogramBuckets.SnapshotSum
            : this.mpComponents.Base2ExponentialBucketHistogram!.SnapshotSum;
    }

    /// <summary>
    /// 获取与度量点关联的直方图的桶。
    /// </summary>
    /// <remarks>
    /// 适用于 <see cref="MetricType.Histogram"/> 度量类型。
    /// </remarks>
    /// <returns><see cref="HistogramBuckets"/>。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly HistogramBuckets GetHistogramBuckets()
    {
        // 检查当前聚合类型是否为直方图类型
        if (this.aggType != AggregationType.HistogramWithBuckets &&
            this.aggType != AggregationType.Histogram &&
            this.aggType != AggregationType.HistogramWithMinMaxBuckets &&
            this.aggType != AggregationType.HistogramWithMinMax)
        {
            // 如果不是直方图类型，抛出不支持的度量类型异常
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetHistogramBuckets));
        }

        // 确保 HistogramBuckets 不为空
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets 为空");

        // 返回 HistogramBuckets
        return this.mpComponents!.HistogramBuckets!;
    }

    /// <summary>
    /// 获取与度量点关联的指数直方图数据。
    /// </summary>
    /// <remarks>
    /// 适用于 <see cref="MetricType.Histogram"/> 度量类型。
    /// </remarks>
    /// <returns><see cref="ExponentialHistogramData"/>。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ExponentialHistogramData GetExponentialHistogramData()
    {
        // 检查当前聚合类型是否为指数直方图类型
        if (this.aggType != AggregationType.Base2ExponentialHistogram &&
            this.aggType != AggregationType.Base2ExponentialHistogramWithMinMax)
        {
            // 如果不是指数直方图类型，抛出不支持的度量类型异常
            this.ThrowNotSupportedMetricTypeException(nameof(this.GetExponentialHistogramData));
        }

        // 确保 Base2ExponentialBucketHistogram 不为空
        Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram 为空");

        // 返回指数直方图数据
        return this.mpComponents!.Base2ExponentialBucketHistogram!.GetExponentialHistogramData();
    }

    /// <summary>
    /// 获取直方图的最小值和最大值。
    /// </summary>
    /// <param name="min"> 直方图的最小值。</param>
    /// <param name="max"> 直方图的最大值。</param>
    /// <returns>如果最小值和最大值存在，则为 True，否则为 False。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryGetHistogramMinMaxValues(out double min, out double max)
    {
        // 检查当前聚合类型是否为带最小值和最大值的直方图类型
        if (this.aggType == AggregationType.HistogramWithMinMax
            || this.aggType == AggregationType.HistogramWithMinMaxBuckets)
        {
            // 确保 HistogramBuckets 不为空
            Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets 为空");

            // 获取最小值和最大值
            min = this.mpComponents!.HistogramBuckets!.SnapshotMin;
            max = this.mpComponents.HistogramBuckets.SnapshotMax;
            return true;
        }

        // 检查当前聚合类型是否为带最小值和最大值的指数直方图类型
        if (this.aggType == AggregationType.Base2ExponentialHistogramWithMinMax)
        {
            // 确保 Base2ExponentialBucketHistogram 不为空
            Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram 为空");

            // 获取最小值和最大值
            min = this.mpComponents!.Base2ExponentialBucketHistogram!.SnapshotMin;
            max = this.mpComponents.Base2ExponentialBucketHistogram.SnapshotMax;
            return true;
        }

        // 如果不符合上述类型，返回默认值
        min = 0;
        max = 0;
        return false;
    }

    /// <summary>
    /// 获取与度量点关联的示例。
    /// </summary>
    /// <param name="exemplars"><see cref="ReadOnlyExemplarCollection"/>.</param>
    /// <returns><see langword="true" /> 如果示例存在；否则为 <see langword="false" />。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryGetExemplars(out ReadOnlyExemplarCollection exemplars)
    {
        // 获取与度量点关联的示例集合，如果没有则返回空集合
        exemplars = this.mpComponents?.Exemplars ?? ReadOnlyExemplarCollection.Empty;
        // 返回示例集合的最大数量是否大于0
        return exemplars.MaximumCount > 0;
    }

    /// <summary>
    /// 复制当前的 MetricPoint 实例。
    /// </summary>
    /// <returns>返回 MetricPoint 的副本。</returns>
    internal readonly MetricPoint Copy()
    {
        // 创建当前 MetricPoint 的副本
        MetricPoint copy = this;
        // 复制可选组件
        copy.mpComponents = this.mpComponents?.Copy();
        return copy;
    }

    /// <summary>
    /// 更新长整型值的度量点。
    /// </summary>
    /// <param name="number">长整型值。</param>
    internal void Update(long number)
    {
        // 根据聚合类型执行相应的更新操作
        switch (this.aggType)
        {
            case AggregationType.LongSumIncomingDelta:
                {
                    // 使用 Interlocked.Add 方法原子性地增加 runningValue 的值
                    Interlocked.Add(ref this.runningValue.AsLong, number);
                    break;
                }

            case AggregationType.LongSumIncomingCumulative:
            case AggregationType.LongGauge:
                {
                    // 使用 Interlocked.Exchange 方法原子性地交换 runningValue 的值
                    Interlocked.Exchange(ref this.runningValue.AsLong, number);
                    break;
                }

            case AggregationType.Histogram:
                {
                    // 更新直方图
                    this.UpdateHistogram(number);
                    return;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    // 更新带最小值和最大值的直方图
                    this.UpdateHistogramWithMinMax(number);
                    return;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    // 更新带桶的直方图
                    this.UpdateHistogramWithBuckets(number);
                    return;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    // 更新带最小值和最大值的桶直方图
                    this.UpdateHistogramWithBucketsAndMinMax(number);
                    return;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    // 更新以 2 为底的指数直方图
                    this.UpdateBase2ExponentialHistogram(number);
                    return;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    // 更新带最小值和最大值的以 2 为底的指数直方图
                    this.UpdateBase2ExponentialHistogramWithMinMax(number);
                    return;
                }
        }

        // 完成更新操作
        this.CompleteUpdate();
    }

    /// <summary>
    /// 更新带示例的长整型值的度量点。
    /// </summary>
    /// <param name="number">长整型值。</param>
    /// <param name="tags">标签。</param>
    /// <param name="offerExemplar">是否提供示例。</param>
    internal void UpdateWithExemplar(long number, ReadOnlySpan<KeyValuePair<string, object?>> tags, bool offerExemplar)
    {
        // 根据聚合类型执行相应的更新操作
        switch (this.aggType)
        {
            case AggregationType.LongSumIncomingDelta:
                {
                    // 使用 Interlocked.Add 方法原子性地增加 runningValue 的值
                    Interlocked.Add(ref this.runningValue.AsLong, number);
                    break;
                }

            case AggregationType.LongSumIncomingCumulative:
            case AggregationType.LongGauge:
                {
                    // 使用 Interlocked.Exchange 方法原子性地交换 runningValue 的值
                    Interlocked.Exchange(ref this.runningValue.AsLong, number);
                    break;
                }

            case AggregationType.Histogram:
                {
                    // 更新带示例的直方图
                    this.UpdateHistogram(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    // 更新带最小值和最大值的直方图
                    this.UpdateHistogramWithMinMax(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    // 更新带桶的直方图
                    this.UpdateHistogramWithBuckets(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    // 更新带最小值和最大值的桶直方图
                    this.UpdateHistogramWithBucketsAndMinMax(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    // 更新带示例的以 2 为底的指数直方图
                    this.UpdateBase2ExponentialHistogram(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    // 更新带最小值和最大值的以 2 为底的指数直方图
                    this.UpdateBase2ExponentialHistogramWithMinMax(number, tags, offerExemplar);
                    return;
                }
        }

        // 更新示例
        this.UpdateExemplar(number, tags, offerExemplar);

        // 完成更新操作
        this.CompleteUpdate();
    }

    /// <summary>
    /// 更新双精度值的度量点。
    /// </summary>
    /// <param name="number">双精度值。</param>
    internal void Update(double number)
    {
        // 根据聚合类型执行相应的更新操作
        switch (this.aggType)
        {
            case AggregationType.DoubleSumIncomingDelta:
                {
                    // 使用 InterlockedHelper.Add 方法原子性地增加 runningValue 的值
                    InterlockedHelper.Add(ref this.runningValue.AsDouble, number);
                    break;
                }

            case AggregationType.DoubleSumIncomingCumulative:
            case AggregationType.DoubleGauge:
                {
                    // 使用 Interlocked.Exchange 方法原子性地交换 runningValue 的值
                    Interlocked.Exchange(ref this.runningValue.AsDouble, number);
                    break;
                }

            case AggregationType.Histogram:
                {
                    // 更新直方图
                    this.UpdateHistogram(number);
                    return;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    // 更新带最小值和最大值的直方图
                    this.UpdateHistogramWithMinMax(number);
                    return;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    // 更新带桶的直方图
                    this.UpdateHistogramWithBuckets(number);
                    return;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    // 更新带最小值和最大值的桶直方图
                    this.UpdateHistogramWithBucketsAndMinMax(number);
                    return;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    // 更新以 2 为底的指数直方图
                    this.UpdateBase2ExponentialHistogram(number);
                    return;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    // 更新带最小值和最大值的以 2 为底的指数直方图
                    this.UpdateBase2ExponentialHistogramWithMinMax(number);
                    return;
                }
        }

        // 完成更新操作
        this.CompleteUpdate();
    }

    /// <summary>
    /// 更新带示例的双精度值的度量点。
    /// </summary>
    /// <param name="number">双精度值。</param>
    /// <param name="tags">标签。</param>
    /// <param name="offerExemplar">是否提供示例。</param>
    internal void UpdateWithExemplar(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags, bool offerExemplar)
    {
        // 根据聚合类型执行相应的更新操作
        switch (this.aggType)
        {
            case AggregationType.DoubleSumIncomingDelta:
                {
                    // 使用 InterlockedHelper.Add 方法原子性地增加 runningValue 的值
                    InterlockedHelper.Add(ref this.runningValue.AsDouble, number);
                    break;
                }

            case AggregationType.DoubleSumIncomingCumulative:
            case AggregationType.DoubleGauge:
                {
                    // 使用 Interlocked.Exchange 方法原子性地交换 runningValue 的值
                    Interlocked.Exchange(ref this.runningValue.AsDouble, number);
                    break;
                }

            case AggregationType.Histogram:
                {
                    // 更新带示例的直方图
                    this.UpdateHistogram(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.HistogramWithMinMax:
                {
                    // 更新带最小值和最大值的直方图
                    this.UpdateHistogramWithMinMax(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.HistogramWithBuckets:
                {
                    // 更新带桶的直方图
                    this.UpdateHistogramWithBuckets(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    // 更新带最小值和最大值的桶直方图
                    this.UpdateHistogramWithBucketsAndMinMax(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.Base2ExponentialHistogram:
                {
                    // 更新带示例的以 2 为底的指数直方图
                    this.UpdateBase2ExponentialHistogram(number, tags, offerExemplar);
                    return;
                }

            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    // 更新带最小值和最大值的以 2 为底的指数直方图
                    this.UpdateBase2ExponentialHistogramWithMinMax(number, tags, offerExemplar);
                    return;
                }
        }

        // 更新示例
        this.UpdateExemplar(number, tags, offerExemplar);

        // 完成更新操作
        this.CompleteUpdate();
    }

    /// <summary>
    /// 生成当前度量点的快照。
    /// </summary>
    /// <param name="outputDelta">是否输出增量数据。</param>
    internal void TakeSnapshot(bool outputDelta)
    {
        switch (this.aggType)
        {
            // 处理长整型增量和累积总和的快照
            case AggregationType.LongSumIncomingDelta:
            case AggregationType.LongSumIncomingCumulative:
                {
                    if (outputDelta)
                    {
                        // 读取当前运行值
                        long initValue = Interlocked.Read(ref this.runningValue.AsLong);
                        // 计算快照值
                        this.snapshotValue.AsLong = initValue - this.deltaLastValue.AsLong;
                        // 更新上次增量值
                        this.deltaLastValue.AsLong = initValue;
                        // 设置度量点状态为无收集待处理
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // 再次检查值是否已更新，如果是则重置状态
                        // 这确保没有更新丢失
                        if (initValue != Interlocked.Read(ref this.runningValue.AsLong))
                        {
                            this.MetricPointStatus = MetricPointStatus.CollectPending;
                        }
                    }
                    else
                    {
                        // 直接读取当前运行值作为快照值
                        this.snapshotValue.AsLong = Interlocked.Read(ref this.runningValue.AsLong);
                    }

                    break;
                }

            // 处理双精度增量和累积总和的快照
            case AggregationType.DoubleSumIncomingDelta:
            case AggregationType.DoubleSumIncomingCumulative:
                {
                    if (outputDelta)
                    {
                        // 读取当前运行值
                        double initValue = InterlockedHelper.Read(ref this.runningValue.AsDouble);
                        // 计算快照值
                        this.snapshotValue.AsDouble = initValue - this.deltaLastValue.AsDouble;
                        // 更新上次增量值
                        this.deltaLastValue.AsDouble = initValue;
                        // 设置度量点状态为无收集待处理
                        this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                        // 再次检查值是否已更新，如果是则重置状态
                        // 这确保没有更新丢失
                        if (initValue != InterlockedHelper.Read(ref this.runningValue.AsDouble))
                        {
                            this.MetricPointStatus = MetricPointStatus.CollectPending;
                        }
                    }
                    else
                    {
                        // 直接读取当前运行值作为快照值
                        this.snapshotValue.AsDouble = InterlockedHelper.Read(ref this.runningValue.AsDouble);
                    }

                    break;
                }

            // 处理长整型仪表的快照
            case AggregationType.LongGauge:
                {
                    // 读取当前运行值作为快照值
                    this.snapshotValue.AsLong = Interlocked.Read(ref this.runningValue.AsLong);
                    // 设置度量点状态为无收集待处理
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // 再次检查值是否已更新，如果是则重置状态
                    // 这确保没有更新丢失
                    if (this.snapshotValue.AsLong != Interlocked.Read(ref this.runningValue.AsLong))
                    {
                        this.MetricPointStatus = MetricPointStatus.CollectPending;
                    }

                    break;
                }

            // 处理双精度仪表的快照
            case AggregationType.DoubleGauge:
                {
                    // 读取当前运行值作为快照值
                    this.snapshotValue.AsDouble = InterlockedHelper.Read(ref this.runningValue.AsDouble);
                    // 设置度量点状态为无收集待处理
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // 再次检查值是否已更新，如果是则重置状态
                    // 这确保没有更新丢失
                    if (this.snapshotValue.AsDouble != InterlockedHelper.Read(ref this.runningValue.AsDouble))
                    {
                        this.MetricPointStatus = MetricPointStatus.CollectPending;
                    }

                    break;
                }

            // 处理带桶的直方图的快照
            case AggregationType.HistogramWithBuckets:
                {
                    // 确保 HistogramBuckets 不为空
                    Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets 为空");

                    var histogramBuckets = this.mpComponents!.HistogramBuckets!;

                    // 获取锁
                    this.mpComponents.AcquireLock();

                    // 设置快照值和快照总和
                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

                    if (outputDelta)
                    {
                        // 重置运行值和运行总和
                        this.runningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                    }

                    // 生成快照
                    histogramBuckets.Snapshot(outputDelta);

                    // 设置度量点状态为无收集待处理
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // 释放锁
                    this.mpComponents.ReleaseLock();

                    break;
                }

            // 处理直方图的快照
            case AggregationType.Histogram:
                {
                    // 确保 HistogramBuckets 不为空
                    Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets 为空");

                    var histogramBuckets = this.mpComponents!.HistogramBuckets!;

                    // 获取锁
                    this.mpComponents.AcquireLock();

                    // 设置快照值和快照总和
                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

                    if (outputDelta)
                    {
                        // 重置运行值和运行总和
                        this.runningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                    }

                    // 设置度量点状态为无收集待处理
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // 释放锁
                    this.mpComponents.ReleaseLock();

                    break;
                }

            // 处理带最小值和最大值的桶直方图的快照
            case AggregationType.HistogramWithMinMaxBuckets:
                {
                    // 确保 HistogramBuckets 不为空
                    Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets 为空");

                    var histogramBuckets = this.mpComponents!.HistogramBuckets!;

                    // 获取锁
                    this.mpComponents.AcquireLock();

                    // 设置快照值、快照总和、快照最小值和快照最大值
                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;
                    histogramBuckets.SnapshotMin = histogramBuckets.RunningMin;
                    histogramBuckets.SnapshotMax = histogramBuckets.RunningMax;

                    if (outputDelta)
                    {
                        // 重置运行值、运行总和、运行最小值和运行最大值
                        this.runningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                        histogramBuckets.RunningMin = double.PositiveInfinity;
                        histogramBuckets.RunningMax = double.NegativeInfinity;
                    }

                    // 生成快照
                    histogramBuckets.Snapshot(outputDelta);

                    // 设置度量点状态为无收集待处理
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // 释放锁
                    this.mpComponents.ReleaseLock();

                    break;
                }

            // 处理带最小值和最大值的直方图的快照
            case AggregationType.HistogramWithMinMax:
                {
                    // 确保 HistogramBuckets 不为空
                    Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets 为空");

                    var histogramBuckets = this.mpComponents!.HistogramBuckets!;

                    // 获取锁
                    this.mpComponents.AcquireLock();

                    // 设置快照值、快照总和、快照最小值和快照最大值
                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;
                    histogramBuckets.SnapshotMin = histogramBuckets.RunningMin;
                    histogramBuckets.SnapshotMax = histogramBuckets.RunningMax;

                    if (outputDelta)
                    {
                        // 重置运行值、运行总和、运行最小值和运行最大值
                        this.runningValue.AsLong = 0;
                        histogramBuckets.RunningSum = 0;
                        histogramBuckets.RunningMin = double.PositiveInfinity;
                        histogramBuckets.RunningMax = double.NegativeInfinity;
                    }

                    // 设置度量点状态为无收集待处理
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // 释放锁
                    this.mpComponents.ReleaseLock();

                    break;
                }

            // 处理以 2 为底的指数直方图的快照
            case AggregationType.Base2ExponentialHistogram:
                {
                    // 确保 Base2ExponentialBucketHistogram 不为空
                    Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram 为空");

                    var histogram = this.mpComponents!.Base2ExponentialBucketHistogram!;

                    // 获取锁
                    this.mpComponents.AcquireLock();

                    // 设置快照值和快照总和
                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogram.SnapshotSum = histogram.RunningSum;
                    histogram.Snapshot();

                    if (outputDelta)
                    {
                        // 重置运行值和运行总和
                        this.runningValue.AsLong = 0;
                        histogram.RunningSum = 0;
                        histogram.Reset();
                    }

                    // 设置度量点状态为无收集待处理
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // 释放锁
                    this.mpComponents.ReleaseLock();

                    break;
                }

            // 处理带最小值和最大值的以 2 为底的指数直方图的快照
            case AggregationType.Base2ExponentialHistogramWithMinMax:
                {
                    // 确保 Base2ExponentialBucketHistogram 不为空
                    Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram 为空");

                    var histogram = this.mpComponents!.Base2ExponentialBucketHistogram!;

                    // 获取锁
                    this.mpComponents.AcquireLock();

                    // 设置快照值、快照总和、快照最小值和快照最大值
                    this.snapshotValue.AsLong = this.runningValue.AsLong;
                    histogram.SnapshotSum = histogram.RunningSum;
                    histogram.Snapshot();
                    histogram.SnapshotMin = histogram.RunningMin;
                    histogram.SnapshotMax = histogram.RunningMax;

                    if (outputDelta)
                    {
                        // 重置运行值、运行总和、运行最小值和运行最大值
                        this.runningValue.AsLong = 0;
                        histogram.RunningSum = 0;
                        histogram.Reset();
                        histogram.RunningMin = double.PositiveInfinity;
                        histogram.RunningMax = double.NegativeInfinity;
                    }

                    // 设置度量点状态为无收集待处理
                    this.MetricPointStatus = MetricPointStatus.NoCollectPending;

                    // 释放锁
                    this.mpComponents.ReleaseLock();

                    break;
                }
        }
    }

    // TakeSnapshotWithExemplar 方法用于生成当前度量点的快照，并收集示例。
    internal void TakeSnapshotWithExemplar(bool outputDelta)
    {
        // 确保 mpComponents 不为空
        Debug.Assert(this.mpComponents != null, "this.mpComponents was null");
        // 确保 ExemplarReservoir 不为空
        Debug.Assert(this.mpComponents!.ExemplarReservoir != null, "this.mpComponents.ExemplarReservoir was null");

        // 生成快照
        this.TakeSnapshot(outputDelta);

        // 收集示例
        this.mpComponents.Exemplars = this.mpComponents.ExemplarReservoir!.Collect();
    }

    // NullifyMetricPointState 方法将 MetricPoint 的成员对象引用设置为 `null`，以便更快地被 GC 回收。
    internal void NullifyMetricPointState()
    {
        // 将 LookupData 设置为 null
        this.LookupData = null;
        // 将 mpComponents 设置为 null
        this.mpComponents = null;
    }

    // UpdateHistogram 方法用于更新直方图的度量点。
    private void UpdateHistogram(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool offerExemplar = false)
    {
        // 确保 HistogramBuckets 不为空
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

        // 获取 HistogramBuckets
        var histogramBuckets = this.mpComponents!.HistogramBuckets!;

        // 获取锁
        this.mpComponents.AcquireLock();

        unchecked
        {
            // 增加运行值
            this.runningValue.AsLong++;
            // 增加运行总和
            histogramBuckets.RunningSum += number;
        }

        // 释放锁
        this.mpComponents.ReleaseLock();

        // 更新示例
        this.UpdateExemplar(number, tags, offerExemplar);

        // 完成更新操作
        this.CompleteUpdate();
    }

    // UpdateHistogramWithMinMax 方法用于更新带最小值和最大值的直方图的度量点。
    private void UpdateHistogramWithMinMax(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool offerExemplar = false)
    {
        // 确保 HistogramBuckets 不为空
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

        // 获取 HistogramBuckets
        var histogramBuckets = this.mpComponents!.HistogramBuckets!;

        // 获取锁
        this.mpComponents.AcquireLock();

        unchecked
        {
            // 增加运行值
            this.runningValue.AsLong++;
            // 增加运行总和
            histogramBuckets.RunningSum += number;
        }

        // 更新运行最小值
        histogramBuckets.RunningMin = Math.Min(histogramBuckets.RunningMin, number);
        // 更新运行最大值
        histogramBuckets.RunningMax = Math.Max(histogramBuckets.RunningMax, number);

        // 释放锁
        this.mpComponents.ReleaseLock();

        // 更新示例
        this.UpdateExemplar(number, tags, offerExemplar);

        // 完成更新操作
        this.CompleteUpdate();
    }

    // UpdateHistogramWithBuckets 方法用于更新带桶的直方图的度量点。
    private void UpdateHistogramWithBuckets(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool offerExemplar = false)
    {
        // 确保 HistogramBuckets 不为空
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "HistogramBuckets was null");

        // 获取 HistogramBuckets
        var histogramBuckets = this.mpComponents!.HistogramBuckets;

        // 查找桶索引
        int bucketIndex = histogramBuckets!.FindBucketIndex(number);

        // 获取锁
        this.mpComponents.AcquireLock();

        unchecked
        {
            // 增加运行值
            this.runningValue.AsLong++;
            // 增加运行总和
            histogramBuckets.RunningSum += number;
            // 增加桶计数
            histogramBuckets.BucketCounts[bucketIndex].RunningValue++;
        }

        // 释放锁
        this.mpComponents.ReleaseLock();

        // 更新示例
        this.UpdateExemplar(number, tags, offerExemplar, bucketIndex);

        // 完成更新操作
        this.CompleteUpdate();
    }

    // UpdateHistogramWithBucketsAndMinMax 方法用于更新带最小值和最大值的桶直方图的度量点。
    private void UpdateHistogramWithBucketsAndMinMax(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool offerExemplar = false)
    {
        // 确保 HistogramBuckets 不为空
        Debug.Assert(this.mpComponents?.HistogramBuckets != null, "histogramBuckets was null");

        // 获取 HistogramBuckets
        var histogramBuckets = this.mpComponents!.HistogramBuckets;

        // 查找桶索引
        int bucketIndex = histogramBuckets!.FindBucketIndex(number);

        // 获取锁
        this.mpComponents.AcquireLock();

        unchecked
        {
            // 增加运行值
            this.runningValue.AsLong++;
            // 增加运行总和
            histogramBuckets.RunningSum += number;
            // 增加桶计数
            histogramBuckets.BucketCounts[bucketIndex].RunningValue++;
        }

        // 更新运行最小值
        histogramBuckets.RunningMin = Math.Min(histogramBuckets.RunningMin, number);
        // 更新运行最大值
        histogramBuckets.RunningMax = Math.Max(histogramBuckets.RunningMax, number);

        // 释放锁
        this.mpComponents.ReleaseLock();

        // 更新示例
        this.UpdateExemplar(number, tags, offerExemplar, bucketIndex);

        // 完成更新操作
        this.CompleteUpdate();
    }

    // 更新以 2 为底的指数直方图
    private void UpdateBase2ExponentialHistogram(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool offerExemplar = false)
    {
        // 如果 number 小于 0，则完成更新操作并返回
        if (number < 0)
        {
            this.CompleteUpdateWithoutMeasurement();
            return;
        }

        // 确保 Base2ExponentialBucketHistogram 不为空
        Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

        // 获取 Base2ExponentialBucketHistogram
        var histogram = this.mpComponents!.Base2ExponentialBucketHistogram!;

        // 获取锁
        this.mpComponents.AcquireLock();

        unchecked
        {
            // 增加运行值
            this.runningValue.AsLong++;
            // 增加运行总和
            histogram.RunningSum += number;
            // 记录值到直方图中
            histogram.Record(number);
        }

        // 释放锁
        this.mpComponents.ReleaseLock();

        // 更新示例
        this.UpdateExemplar(number, tags, offerExemplar);

        // 完成更新操作
        this.CompleteUpdate();
    }

    // 更新带最小值和最大值的以 2 为底的指数直方图
    private void UpdateBase2ExponentialHistogramWithMinMax(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags = default, bool offerExemplar = false)
    {
        // 如果 number 小于 0，则完成更新操作并返回
        if (number < 0)
        {
            this.CompleteUpdateWithoutMeasurement();
            return;
        }

        // 确保 Base2ExponentialBucketHistogram 不为空
        Debug.Assert(this.mpComponents?.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

        // 获取 Base2ExponentialBucketHistogram
        var histogram = this.mpComponents!.Base2ExponentialBucketHistogram!;

        // 获取锁
        this.mpComponents.AcquireLock();

        unchecked
        {
            // 增加运行值
            this.runningValue.AsLong++;
            // 增加运行总和
            histogram.RunningSum += number;
            // 记录值到直方图中
            histogram.Record(number);
        }

        // 更新运行最小值
        histogram.RunningMin = Math.Min(histogram.RunningMin, number);
        // 更新运行最大值
        histogram.RunningMax = Math.Max(histogram.RunningMax, number);

        // 释放锁
        this.mpComponents.ReleaseLock();

        // 更新示例
        this.UpdateExemplar(number, tags, offerExemplar);

        // 完成更新操作
        this.CompleteUpdate();
    }

    // 更新长整型值的示例
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly void UpdateExemplar(long number, ReadOnlySpan<KeyValuePair<string, object?>> tags, bool offerExemplar)
    {
        // 如果提供示例
        if (offerExemplar)
        {
            // 确保 ExemplarReservoir 不为空
            Debug.Assert(this.mpComponents?.ExemplarReservoir != null, "ExemplarReservoir was null");

            // 提供示例
            this.mpComponents!.ExemplarReservoir!.Offer(
                new ExemplarMeasurement<long>(number, tags));
        }
    }

    // 更新双精度值的示例
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly void UpdateExemplar(double number, ReadOnlySpan<KeyValuePair<string, object?>> tags, bool offerExemplar, int explicitBucketHistogramBucketIndex = -1)
    {
        // 如果提供示例
        if (offerExemplar)
        {
            // 确保 ExemplarReservoir 不为空
            Debug.Assert(this.mpComponents?.ExemplarReservoir != null, "ExemplarReservoir was null");

            // 提供示例
            this.mpComponents!.ExemplarReservoir!.Offer(
                new ExemplarMeasurement<double>(number, tags, explicitBucketHistogramBucketIndex));
        }
    }

    /// <summary>
    /// 完成更新操作。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CompleteUpdate()
    {
        // 存在与 Snapshot 的竞争：
        // Update() 更新值
        // Snapshot 快照值
        // Snapshot 将状态设置为 NoCollectPending
        // Update 将状态设置为 CollectPending -- 这是不对的，因为 Snapshot
        // 已经包含了更新的值。
        // 在下一个 Snapshot 之前没有新的 Update 调用的情况下，
        // 这会导致导出一个 Update，即使它没有更新。
        // TODO: 对于 Delta，可以通过忽略零点来缓解
        this.MetricPointStatus = MetricPointStatus.CollectPending;

        this.CompleteUpdateWithoutMeasurement();
    }

    /// <summary>
    /// 完成没有测量的更新操作。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CompleteUpdateWithoutMeasurement()
    {
        // 如果输出增量数据
        if (this.aggregatorStore.OutputDelta)
        {
            // 原子性地减少 ReferenceCount 的值
            Interlocked.Decrement(ref this.ReferenceCount);
        }
    }

    /// <summary>
    /// 抛出不支持的度量类型异常。
    /// </summary>
    /// <param name="methodName">方法名称。</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowNotSupportedMetricTypeException(string methodName)
    {
        // 抛出 NotSupportedException 异常，提示方法不支持此度量类型
        throw new NotSupportedException($"{methodName} is not supported for this metric type.");
    }
}
