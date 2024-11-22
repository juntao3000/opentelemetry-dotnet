// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
#if NET
using System.Collections.Frozen;
#endif
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

internal sealed class AggregatorStore
{
    // 记录感兴趣的标签键集合（在 .NET 版本中使用 FrozenSet，否则使用 HashSet）
#if NET
    internal readonly FrozenSet<string>? TagKeysInteresting;
#else
    internal readonly HashSet<string>? TagKeysInteresting;
#endif

    // 感兴趣的标签键数量
    private readonly int tagsKeysInterestingCount;

    // 是否输出增量数据
    internal readonly bool OutputDelta;

    // 度量点的数量
    internal readonly int NumberOfMetricPoints;

    // 用于存储标签到度量点索引的字典（仅在增量模式下使用）
    internal readonly ConcurrentDictionary<Tags, LookupData>? TagsToMetricPointIndexDictionaryDelta;

    // 用于创建示例库的工厂方法
    internal readonly Func<ExemplarReservoir?>? ExemplarReservoirFactory;

    // 丢弃的测量数量
    internal long DroppedMeasurements = 0;

    // 默认的示例过滤器类型
    private const ExemplarFilterType DefaultExemplarFilter = ExemplarFilterType.AlwaysOff;

    // 用于比较维度的委托
    private static readonly Comparison<KeyValuePair<string, object?>> DimensionComparisonDelegate = (x, y) => x.Key.CompareTo(y.Key);

    // 用于零标签的锁
    private readonly Lock lockZeroTags = new();

    // 用于溢出标签的锁
    private readonly Lock lockOverflowTag = new();

    // 保存可重用的度量点队列
    private readonly Queue<int>? availableMetricPoints;

    // 用于存储标签到度量点索引的字典
    private readonly ConcurrentDictionary<Tags, int> tagsToMetricPointIndexDictionary = new();

    // 度量名称
    private readonly string name;

    // 度量点数组
    private readonly MetricPoint[] metricPoints;

    // 当前度量点批次
    private readonly int[] currentMetricPointBatch;

    // 聚合类型
    private readonly AggregationType aggType;

    // 直方图边界
    private readonly double[] histogramBounds;

    // 指数直方图的最大尺寸
    private readonly int exponentialHistogramMaxSize;

    // 指数直方图的最大比例
    private readonly int exponentialHistogramMaxScale;

    // 更新长整型值的回调
    private readonly UpdateLongDelegate updateLongCallback;

    // 更新双精度值的回调
    private readonly UpdateDoubleDelegate updateDoubleCallback;

    // 示例过滤器类型
    private readonly ExemplarFilterType exemplarFilter;

    // 查找聚合存储的函数
    private readonly Func<KeyValuePair<string, object?>[], int, int> lookupAggregatorStore;

    // 度量点索引
    private int metricPointIndex = 0;

    // 批次大小
    private int batchSize = 0;

    // 是否已初始化零标签度量点
    private bool zeroTagMetricPointInitialized;

    // 是否已初始化溢出标签度量点
    private bool overflowTagMetricPointInitialized;

    // AggregatorStore 构造函数
    internal AggregatorStore(
        MetricStreamIdentity metricStreamIdentity, // 度量流标识
        AggregationType aggType, // 聚合类型
        AggregationTemporality temporality, // 聚合时间类型
        int cardinalityLimit, // 基数限制
        ExemplarFilterType? exemplarFilter = null, // 示例过滤器类型（可选）
        Func<ExemplarReservoir?>? exemplarReservoirFactory = null) // 示例库工厂方法（可选）
    {
        this.name = metricStreamIdentity.InstrumentName; // 初始化度量名称

        // 增加 CardinalityLimit 2 以保留额外空间。
        // 此调整考虑了溢出属性和提供零标签的情况。
        // 以前，这些包含在原始的 cardinalityLimit 中，但现在它们被显式添加以增强清晰度。
        this.NumberOfMetricPoints = cardinalityLimit + 2;

        this.metricPoints = new MetricPoint[this.NumberOfMetricPoints]; // 初始化度量点数组
        this.currentMetricPointBatch = new int[this.NumberOfMetricPoints]; // 初始化当前度量点批次数组
        this.aggType = aggType; // 初始化聚合类型
        this.OutputDelta = temporality == AggregationTemporality.Delta; // 初始化是否输出增量数据
        this.histogramBounds = metricStreamIdentity.HistogramBucketBounds ?? FindDefaultHistogramBounds(in metricStreamIdentity); // 初始化直方图边界
        this.exponentialHistogramMaxSize = metricStreamIdentity.ExponentialHistogramMaxSize; // 初始化指数直方图的最大尺寸
        this.exponentialHistogramMaxScale = metricStreamIdentity.ExponentialHistogramMaxScale; // 初始化指数直方图的最大比例
        this.StartTimeExclusive = DateTimeOffset.UtcNow; // 初始化度量点的开始时间
        this.ExemplarReservoirFactory = exemplarReservoirFactory; // 初始化示例库工厂方法

        if (metricStreamIdentity.TagKeys == null)
        {
            this.updateLongCallback = this.UpdateLong; // 初始化更新长整型值的回调
            this.updateDoubleCallback = this.UpdateDouble; // 初始化更新双精度值的回调
        }
        else
        {
            this.updateLongCallback = this.UpdateLongCustomTags; // 初始化更新长整型值的回调（自定义标签）
            this.updateDoubleCallback = this.UpdateDoubleCustomTags; // 初始化更新双精度值的回调（自定义标签）
#if NET
            var hs = FrozenSet.ToFrozenSet(metricStreamIdentity.TagKeys, StringComparer.Ordinal); // 使用 FrozenSet 初始化感兴趣的标签键集合
#else
            var hs = new HashSet<string>(metricStreamIdentity.TagKeys, StringComparer.Ordinal); // 使用 HashSet 初始化感兴趣的标签键集合
#endif
            this.TagKeysInteresting = hs; // 设置感兴趣的标签键集合
            this.tagsKeysInterestingCount = hs.Count; // 设置感兴趣的标签键数量
        }

        this.exemplarFilter = exemplarFilter ?? DefaultExemplarFilter; // 初始化示例过滤器类型
        Debug.Assert(
            this.exemplarFilter == ExemplarFilterType.AlwaysOff
            || this.exemplarFilter == ExemplarFilterType.AlwaysOn
            || this.exemplarFilter == ExemplarFilterType.TraceBased,
            "this.exemplarFilter had an unexpected value"); // 断言示例过滤器类型的值是否有效

        // 将 metricPointIndex 设置为 1，因为我们将保留 metricPoints[1] 用于溢出属性。
        // 新的属性应从索引 2 开始添加
        this.metricPointIndex = 1;

        // 始终回收未使用的度量点以进行增量聚合时间类型
        if (this.OutputDelta)
        {
            this.availableMetricPoints = new Queue<int>(cardinalityLimit); // 初始化可用度量点队列

            // 没有仅接受容量作为参数的重载
            // 使用 ConcurrentDictionary 类中定义的 DefaultConcurrencyLevel：
            // https://github.com/dotnet/runtime/blob/v7.0.5/src/libraries/System.Collections.Concurrent/src/System/Collections/Concurrent/ConcurrentDictionary.cs#L2020
            // 我们预计最多（用户提供的基数限制）* 2 个条目 - 一个用于排序，一个用于未排序的输入
            //
            // 初始化标签到度量点索引的字典（增量模式）
            this.TagsToMetricPointIndexDictionaryDelta = new ConcurrentDictionary<Tags, LookupData>(concurrencyLevel: Environment.ProcessorCount, capacity: cardinalityLimit * 2); 

            // 将所有索引（保留的除外）添加到队列中，以便线程可以随时访问这些度量点以供使用。
            // 索引 0 和 1 保留用于无标签和溢出
            for (int i = 2; i < this.NumberOfMetricPoints; i++)
            {
                this.availableMetricPoints.Enqueue(i); // 将索引添加到可用度量点队列
            }

            this.lookupAggregatorStore = this.LookupAggregatorStoreForDeltaWithReclaim; // 设置查找聚合存储的函数（增量模式）
        }
        else
        {
            this.lookupAggregatorStore = this.LookupAggregatorStore; // 设置查找聚合存储的函数
        }
    }

    // 更新长整型值
    private delegate void UpdateLongDelegate(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

    // 更新双精度值
    private delegate void UpdateDoubleDelegate(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

    // 度量点的开始时间（UTC）
    internal DateTimeOffset StartTimeExclusive { get; private set; }

    // 度量点的结束时间（UTC）
    internal DateTimeOffset EndTimeInclusive { get; private set; }

    // 直方图边界
    internal double[] HistogramBounds => this.histogramBounds;

    // 检查是否启用了示例过滤器
    internal bool IsExemplarEnabled()
    {
        // 使用示例过滤器来指示启用/禁用，而不是使用另一个单独的标志。
        // 如果示例过滤器类型不是 AlwaysOff，则表示启用了示例过滤器。
        return this.exemplarFilter != ExemplarFilterType.AlwaysOff;
    }

    // 更新长整型值
    // 这个函数的作用是更新长整型的度量值。它首先查找与给定标签匹配的度量点索引，然后调用 UpdateLongMetricPoint 方法更新度量点的值。
    internal void Update(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        try
        {
            // 调用更新长整型值的回调方法
            this.updateLongCallback(value, tags);
        }
        catch (Exception)
        {
            // 如果发生异常，增加丢弃的测量数量
            Interlocked.Increment(ref this.DroppedMeasurements);
            // 记录测量丢弃事件
            OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, "SDK internal error occurred.", "Contact SDK owners.");
        }
    }

    // 更新双精度值
    internal void Update(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        try
        {
            // 调用更新双精度值的回调方法
            this.updateDoubleCallback(value, tags);
        }
        catch (Exception)
        {
            // 如果发生异常，增加丢弃的测量数量
            Interlocked.Increment(ref this.DroppedMeasurements);
            // 记录测量丢弃事件
            OpenTelemetrySdkEventSource.Log.MeasurementDropped(this.name, "SDK internal error occurred.", "Contact SDK owners.");
        }
    }

    // Snapshot 方法用于生成当前度量点的快照
    // 它会根据是否输出增量数据来调用不同的快照方法
    // 最后更新度量点的结束时间，并返回批次大小
    internal int Snapshot()
    {
        // 初始化批次大小为 0
        this.batchSize = 0;

        // 如果输出增量数据
        if (this.OutputDelta)
        {
            // 调用 SnapshotDeltaWithMetricPointReclaim 方法生成增量快照
            this.SnapshotDeltaWithMetricPointReclaim();
        }
        else
        {
            // 否则，调用 SnapshotCumulative 方法生成累积快照
            var indexSnapshot = Math.Min(this.metricPointIndex, this.NumberOfMetricPoints - 1);
            this.SnapshotCumulative(indexSnapshot);
        }

        // 更新度量点的结束时间为当前时间
        this.EndTimeInclusive = DateTimeOffset.UtcNow;

        // 返回批次大小
        return this.batchSize;
    }

    // SnapshotDeltaWithMetricPointReclaim 方法用于生成当前度量点的增量快照，并回收未使用的度量点
    internal void SnapshotDeltaWithMetricPointReclaim()
    {
        // Index = 0 保留用于没有维度的情况
        ref var metricPointWithNoTags = ref this.metricPoints[0];
        if (metricPointWithNoTags.MetricPointStatus != MetricPointStatus.NoCollectPending)
        {
            // 生成没有标签的度量点的快照
            this.TakeMetricPointSnapshot(ref metricPointWithNoTags, outputDelta: true);

            // 将当前度量点批次的索引设置为 0
            this.currentMetricPointBatch[this.batchSize] = 0;
            this.batchSize++;
        }

        // 为溢出标签的度量点生成快照
        ref var metricPointForOverflow = ref this.metricPoints[1];
        if (metricPointForOverflow.MetricPointStatus != MetricPointStatus.NoCollectPending)
        {
            // 生成溢出标签的度量点的快照
            this.TakeMetricPointSnapshot(ref metricPointForOverflow, outputDelta: true);

            // 将当前度量点批次的索引设置为 1
            this.currentMetricPointBatch[this.batchSize] = 1;
            this.batchSize++;
        }

        // Index 0 和 1 保留用于没有标签和溢出标签的情况
        for (int i = 2; i < this.NumberOfMetricPoints; i++)
        {
            ref var metricPoint = ref this.metricPoints[i];

            if (metricPoint.MetricPointStatus == MetricPointStatus.NoCollectPending)
            {
                // 如果度量点在上一个收集周期中被标记为回收，则回收该度量点
                if (metricPoint.LookupData != null && metricPoint.LookupData.DeferredReclaim == true)
                {
                    this.ReclaimMetricPoint(ref metricPoint, i);
                    continue;
                }

                // 检查度量点是否可以在当前收集周期中回收
                // 如果 metricPoint.LookupData 为 null，则表示度量点已被回收并在队列中
                // 如果收集线程能够成功地将引用计数从零交换为 int.MinValue，则表示度量点可以用于其他标签
                if (metricPoint.LookupData != null && Interlocked.CompareExchange(ref metricPoint.ReferenceCount, int.MinValue, 0) == 0)
                {
                    // 这类似于双重检查锁定。在某些罕见情况下，收集线程可能会读取状态为 NoCollectPending，
                    // 然后在它能够将 ReferenceCount 设置为 int.MinValue 之前被切换出去。
                    // 在此期间，更新线程可能会进来并更新度量点，从而将其状态设置为 CollectPending。
                    // 请注意，更新后的 ReferenceCount 将为 0。
                    // 如果收集线程现在醒来，它将能够将 ReferenceCount 设置为 int.MinValue，从而使度量点对新的更新无效。
                    // 在这种情况下，度量点在生成快照之前不应被回收。
                    if (metricPoint.MetricPointStatus == MetricPointStatus.NoCollectPending)
                    {
                        this.ReclaimMetricPoint(ref metricPoint, i);
                    }
                    else
                    {
                        // 度量点的 ReferenceCount 为 int.MinValue，但仍有待收集的内容。
                        // 生成度量点的快照，并将其标记为在下一个收集周期中回收。
                        metricPoint.LookupData.DeferredReclaim = true;

                        this.TakeMetricPointSnapshot(ref metricPoint, outputDelta: true);

                        this.currentMetricPointBatch[this.batchSize] = i;
                        this.batchSize++;
                    }
                }

                continue;
            }

            // 生成度量点的快照
            this.TakeMetricPointSnapshot(ref metricPoint, outputDelta: true);

            // 将当前度量点批次的索引设置为 i
            this.currentMetricPointBatch[this.batchSize] = i;
            this.batchSize++;
        }

        // 如果 EndTimeInclusive 不为默认值，则将 StartTimeExclusive 设置为 EndTimeInclusive
        if (this.EndTimeInclusive != default)
        {
            this.StartTimeExclusive = this.EndTimeInclusive;
        }
    }

    // SnapshotCumulative 方法用于生成当前度量点的累积快照
    internal void SnapshotCumulative(int indexSnapshot)
    {
        // 遍历所有度量点，直到 indexSnapshot
        for (int i = 0; i <= indexSnapshot; i++)
        {
            // 获取当前度量点的引用
            ref var metricPoint = ref this.metricPoints[i];

            // 如果度量点未初始化，则跳过
            if (!metricPoint.IsInitialized)
            {
                continue;
            }

            // 生成度量点的快照，不输出增量数据
            this.TakeMetricPointSnapshot(ref metricPoint, outputDelta: false);

            // 将当前度量点的索引添加到当前度量点批次中
            this.currentMetricPointBatch[this.batchSize] = i;

            // 增加批次大小
            this.batchSize++;
        }
    }

    internal MetricPointsAccessor GetMetricPoints()
        => new(this.metricPoints, this.currentMetricPointBatch, this.batchSize);

    // 查找默认的直方图边界
    private static double[] FindDefaultHistogramBounds(in MetricStreamIdentity metricStreamIdentity)
    {
        // 如果度量单位是秒
        if (metricStreamIdentity.Unit == "s")
        {
            // 如果度量名称和仪器名称在默认短秒直方图边界映射中
            if (Metric.DefaultHistogramBoundShortMappings
                .Contains((metricStreamIdentity.MeterName, metricStreamIdentity.InstrumentName)))
            {
                // 返回默认短秒直方图边界
                return Metric.DefaultHistogramBoundsShortSeconds;
            }

            // 如果度量名称和仪器名称在默认长秒直方图边界映射中
            if (Metric.DefaultHistogramBoundLongMappings
                .Contains((metricStreamIdentity.MeterName, metricStreamIdentity.InstrumentName)))
            {
                // 返回默认长秒直方图边界
                return Metric.DefaultHistogramBoundsLongSeconds;
            }
        }

        // 返回默认直方图边界
        return Metric.DefaultHistogramBounds;
    }

    // TakeMetricPointSnapshot 方法用于生成度量点的快照
    // 如果启用了示例过滤器，则调用带有示例的快照方法
    // 否则，调用普通快照方法
    private void TakeMetricPointSnapshot(ref MetricPoint metricPoint, bool outputDelta)
    {
        // 检查是否启用了示例过滤器
        if (this.IsExemplarEnabled())
        {
            // 如果启用了示例过滤器，则调用带有示例的快照方法
            metricPoint.TakeSnapshotWithExemplar(outputDelta);
        }
        else
        {
            // 如果未启用示例过滤器，则调用普通快照方法
            metricPoint.TakeSnapshot(outputDelta);
        }
    }

    // ReclaimMetricPoint 方法用于回收未使用的度量点
    private void ReclaimMetricPoint(ref MetricPoint metricPoint, int metricPointIndex)
    {
        /*
         该方法执行以下三件事：
          1. 将 `metricPoint.LookupData` 和 `metricPoint.mpComponents` 设置为 `null`，以便 GC 更快地回收它们。
          2. 尝试从查找字典中删除此 MetricPoint 的条目。检索此 MetricPoint 的更新线程会意识到该 MetricPoint 无效，因为其引用计数已被设置为负数。
             当这种情况发生时，更新线程也会尝试从查找字典中删除此 MetricPoint 的条目。
             我们只关心条目从查找字典中删除，而不关心哪个线程删除它。
          3. 将此 MetricPoint 的数组索引放入可用度量点队列中。这使得更新线程可以使用此 MetricPoint 来跟踪新的维度组合。
        */

        var lookupData = metricPoint.LookupData;

        // 仅在检查 `metricPoint.LookupData` 不为 `null` 后调用此方法。
        Debug.Assert(lookupData != null, "提供的 MetricPoint 的 LookupData 为 null");

        // 将度量点的状态设置为 null，以便 GC 更快地回收它们
        metricPoint.NullifyMetricPointState();

        Debug.Assert(this.TagsToMetricPointIndexDictionaryDelta != null, "this.tagsToMetricPointIndexDictionaryDelta 为 null");

        // 锁定字典以确保线程安全
        lock (this.TagsToMetricPointIndexDictionaryDelta!)
        {
            LookupData? dictionaryValue;
            if (lookupData!.SortedTags != Tags.EmptyTags)
            {
                // 检查是否没有其他线程为相同的标签添加新条目。
                // 如果没有，则删除现有条目。
                if (this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(lookupData.SortedTags, out dictionaryValue) &&
                    dictionaryValue == lookupData)
                {
                    this.TagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.SortedTags, out var _);
                    this.TagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.GivenTags, out var _);
                }
            }
            else
            {
                if (this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(lookupData.GivenTags, out dictionaryValue) &&
                    dictionaryValue == lookupData)
                {
                    this.TagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.GivenTags, out var _);
                }
            }

            Debug.Assert(this.availableMetricPoints != null, "this.availableMetricPoints 为 null");

            // 将度量点的索引放入可用度量点队列中
            this.availableMetricPoints!.Enqueue(metricPointIndex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeZeroTagPointIfNotInitialized()
    {
        // 检查零标签度量点是否已初始化
        if (!this.zeroTagMetricPointInitialized)
        {
            // 使用锁来确保线程安全
            lock (this.lockZeroTags)
            {
                // 再次检查零标签度量点是否已初始化，以防止多线程环境下的重复初始化
                if (!this.zeroTagMetricPointInitialized)
                {
                    // 如果输出增量数据
                    if (this.OutputDelta)
                    {
                        // 创建一个新的 LookupData 对象，表示零标签度量点
                        var lookupData = new LookupData(0, Tags.EmptyTags, Tags.EmptyTags);
                        // 初始化零标签度量点
                        this.metricPoints[0] = new MetricPoint(this, this.aggType, null, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);
                    }
                    else
                    {
                        // 初始化零标签度量点（不输出增量数据）
                        this.metricPoints[0] = new MetricPoint(this, this.aggType, null, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale);
                    }

                    // 标记零标签度量点已初始化
                    this.zeroTagMetricPointInitialized = true;
                }
            }
        }
    }

    // 初始化溢出标签度量点，如果尚未初始化
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeOverflowTagPointIfNotInitialized()
    {
        // 检查溢出标签度量点是否已初始化
        if (!this.overflowTagMetricPointInitialized)
        {
            // 使用锁来确保线程安全
            lock (this.lockOverflowTag)
            {
                // 再次检查溢出标签度量点是否已初始化，以防止多线程环境下的重复初始化
                if (!this.overflowTagMetricPointInitialized)
                {
                    // 创建一个包含溢出标签的键值对数组
                    var keyValuePairs = new KeyValuePair<string, object?>[] { new("otel.metric.overflow", true) };
                    // 创建一个 Tags 对象，包含上述键值对
                    var tags = new Tags(keyValuePairs);

                    // 如果输出增量数据
                    if (this.OutputDelta)
                    {
                        // 创建一个新的 LookupData 对象，表示溢出标签度量点
                        var lookupData = new LookupData(1, tags, tags);
                        // 初始化溢出标签度量点
                        this.metricPoints[1] = new MetricPoint(this, this.aggType, keyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);
                    }
                    else
                    {
                        // 初始化溢出标签度量点（不输出增量数据）
                        this.metricPoints[1] = new MetricPoint(this, this.aggType, keyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale);
                    }

                    // 标记溢出标签度量点已初始化
                    this.overflowTagMetricPointInitialized = true;
                }
            }
        }
    }

    // 这个函数的作用是根据传入的标签键值对数组查找或创建相应的度量点索引。
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LookupAggregatorStore(KeyValuePair<string, object?>[] tagKeysAndValues, int length)
    {
        // 创建一个 Tags 对象，用于存储传入的标签键值对
        var givenTags = new Tags(tagKeysAndValues);

        // 尝试从字典中获取给定标签的聚合器索引
        if (!this.tagsToMetricPointIndexDictionary.TryGetValue(givenTags, out var aggregatorIndex))
        {
            // 如果字典中不存在给定标签的聚合器索引
            if (length > 1)
            {
                // 注意：我们使用的是 ThreadStatic 存储，因此需要为字典存储进行深拷贝。
                // 创建或获取新的数组以临时保存排序后的标签键和值
                var storage = ThreadStaticStorage.GetStorage();
                storage.CloneKeysAndValues(tagKeysAndValues, length, out var tempSortedTagKeysAndValues);

                // 对标签键值对进行排序
                Array.Sort(tempSortedTagKeysAndValues, DimensionComparisonDelegate);

                // 创建一个 Tags 对象，用于存储排序后的标签键值对
                var sortedTags = new Tags(tempSortedTagKeysAndValues);

                // 再次尝试从字典中获取排序后的标签的聚合器索引
                if (!this.tagsToMetricPointIndexDictionary.TryGetValue(sortedTags, out aggregatorIndex))
                {
                    // 如果字典中仍不存在排序后的标签的聚合器索引
                    aggregatorIndex = this.metricPointIndex;
                    if (aggregatorIndex >= this.NumberOfMetricPoints)
                    {
                        // 抱歉！数据点已用完。
                        // TODO: 一旦我们支持清理未使用的点（通常是增量），我们可以在这里重新获取它们。
                        return -1;
                    }

                    // 注意：我们使用的是 ThreadStatic 存储（最多 MaxTagCacheSize 标签），用于标签的输入顺序和排序顺序，
                    // 因此需要为字典存储进行深拷贝。
                    if (length <= ThreadStaticStorage.MaxTagCacheSize)
                    {
                        var givenTagKeysAndValues = new KeyValuePair<string, object?>[length];
                        tagKeysAndValues.CopyTo(givenTagKeysAndValues.AsSpan());

                        var sortedTagKeysAndValues = new KeyValuePair<string, object?>[length];
                        tempSortedTagKeysAndValues.CopyTo(sortedTagKeysAndValues.AsSpan());

                        givenTags = new Tags(givenTagKeysAndValues);
                        sortedTags = new Tags(sortedTagKeysAndValues);
                    }

                    // 加锁以确保线程安全
                    lock (this.tagsToMetricPointIndexDictionary)
                    {
                        // 获取锁后再次检查字典中是否存在排序后的标签的聚合器索引
                        if (!this.tagsToMetricPointIndexDictionary.TryGetValue(sortedTags, out aggregatorIndex))
                        {
                            // 如果字典中仍不存在排序后的标签的聚合器索引
                            aggregatorIndex = ++this.metricPointIndex;
                            if (aggregatorIndex >= this.NumberOfMetricPoints)
                            {
                                // 抱歉！数据点已用完。
                                // TODO: 一旦我们支持清理未使用的点（通常是增量），我们可以在这里重新获取它们。
                                return -1;
                            }

                            // 初始化度量点
                            ref var metricPoint = ref this.metricPoints[aggregatorIndex];
                            metricPoint = new MetricPoint(this, this.aggType, sortedTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale);

                            // 在初始化 MetricPoint 后添加到字典中，因为如果字典条目被找到，其他线程可以开始写入 MetricPoint。
                            // 添加排序后的标签和给定顺序的标签到字典中
                            this.tagsToMetricPointIndexDictionary.TryAdd(sortedTags, aggregatorIndex);
                            this.tagsToMetricPointIndexDictionary.TryAdd(givenTags, aggregatorIndex);
                        }
                    }
                }
            }
            else
            {
                // 这个 else 块用于标签长度为 1 的情况
                aggregatorIndex = this.metricPointIndex;
                if (aggregatorIndex >= this.NumberOfMetricPoints)
                {
                    // 抱歉！数据点已用完。
                    // TODO: 一旦我们支持清理未使用的点（通常是增量），我们可以在这里重新获取它们。
                    return -1;
                }

                // 注意：我们使用的是 ThreadStatic 存储，因此需要为字典存储进行深拷贝。
                var givenTagKeysAndValues = new KeyValuePair<string, object?>[length];

                tagKeysAndValues.CopyTo(givenTagKeysAndValues.AsSpan());

                givenTags = new Tags(givenTagKeysAndValues);

                // 加锁以确保线程安全
                lock (this.tagsToMetricPointIndexDictionary)
                {
                    // 获取锁后再次检查字典中是否存在给定标签的聚合器索引
                    if (!this.tagsToMetricPointIndexDictionary.TryGetValue(givenTags, out aggregatorIndex))
                    {
                        // 如果字典中仍不存在给定标签的聚合器索引
                        aggregatorIndex = ++this.metricPointIndex;
                        if (aggregatorIndex >= this.NumberOfMetricPoints)
                        {
                            // 抱歉！数据点已用完。
                            // TODO: 一旦我们支持清理未使用的点（通常是增量），我们可以在这里重新获取它们。
                            return -1;
                        }

                        // 初始化度量点
                        ref var metricPoint = ref this.metricPoints[aggregatorIndex];
                        metricPoint = new MetricPoint(this, this.aggType, givenTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale);

                        // 在初始化 MetricPoint 后添加到字典中，因为如果字典条目被找到，其他线程可以开始写入 MetricPoint。
                        // 给定标签长度为 1 时，givenTags 将始终排序
                        this.tagsToMetricPointIndexDictionary.TryAdd(givenTags, aggregatorIndex);
                    }
                }
            }
        }

        // 返回聚合器索引
        return aggregatorIndex;
    }

    // LookupAggregatorStoreForDeltaWithReclaim 方法用于查找或创建度量点索引，并在必要时回收未使用的度量点
    // 该方法首先尝试从字典中获取给定标签的聚合器索引，如果不存在则创建新的度量点
    // 如果在创建新度量点时遇到并发问题，则会重试获取可用的度量点
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LookupAggregatorStoreForDeltaWithReclaim(KeyValuePair<string, object?>[] tagKeysAndValues, int length)
    {
        int index;
        var givenTags = new Tags(tagKeysAndValues);

        Debug.Assert(this.TagsToMetricPointIndexDictionaryDelta != null, "this.tagsToMetricPointIndexDictionaryDelta 为 null");

        bool newMetricPointCreated = false;

        // 尝试从字典中获取给定标签的聚合器索引
        if (!this.TagsToMetricPointIndexDictionaryDelta!.TryGetValue(givenTags, out var lookupData))
        {
            if (length > 1)
            {
                // 注意：我们使用的是 ThreadStatic 存储，因此需要为字典存储进行深拷贝。
                // 创建或获取新的数组以临时保存排序后的标签键和值
                var storage = ThreadStaticStorage.GetStorage();
                storage.CloneKeysAndValues(tagKeysAndValues, length, out var tempSortedTagKeysAndValues);

                // 对标签键值对进行排序
                Array.Sort(tempSortedTagKeysAndValues, DimensionComparisonDelegate);

                var sortedTags = new Tags(tempSortedTagKeysAndValues);

                // 再次尝试从字典中获取排序后的标签的聚合器索引
                if (!this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(sortedTags, out lookupData))
                {
                    // 注意：我们使用的是 ThreadStatic 存储（最多 MaxTagCacheSize 标签），用于标签的输入顺序和排序顺序，
                    // 因此需要为字典存储进行深拷贝。
                    if (length <= ThreadStaticStorage.MaxTagCacheSize)
                    {
                        var givenTagKeysAndValues = new KeyValuePair<string, object?>[length];
                        tagKeysAndValues.CopyTo(givenTagKeysAndValues.AsSpan());

                        var sortedTagKeysAndValues = new KeyValuePair<string, object?>[length];
                        tempSortedTagKeysAndValues.CopyTo(sortedTagKeysAndValues.AsSpan());

                        givenTags = new Tags(givenTagKeysAndValues);
                        sortedTags = new Tags(sortedTagKeysAndValues);
                    }

                    Debug.Assert(this.availableMetricPoints != null, "this.availableMetricPoints 为 null");

                    // 加锁以确保线程安全
                    lock (this.TagsToMetricPointIndexDictionaryDelta)
                    {
                        // 获取锁后再次检查字典中是否存在排序后的标签的聚合器索引
                        if (!this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(sortedTags, out lookupData))
                        {
                            // 检查是否有可用的度量点
                            if (this.availableMetricPoints!.Count > 0)
                            {
                                index = this.availableMetricPoints.Dequeue();
                            }
                            else
                            {
                                // 没有可用的度量点
                                return -1;
                            }

                            lookupData = new LookupData(index, sortedTags, givenTags);

                            ref var metricPoint = ref this.metricPoints[index];
                            metricPoint = new MetricPoint(this, this.aggType, sortedTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);
                            newMetricPointCreated = true;

                            // 在初始化 MetricPoint 后添加到字典中，因为如果字典条目被找到，其他线程可以开始写入 MetricPoint。
                            // 添加排序后的标签和给定顺序的标签到字典中
                            this.TagsToMetricPointIndexDictionaryDelta.TryAdd(sortedTags, lookupData);
                            this.TagsToMetricPointIndexDictionaryDelta.TryAdd(givenTags, lookupData);
                        }
                    }
                }
            }
            else
            {
                // 这个 else 块用于标签长度为 1 的情况

                // 注意：我们使用的是 ThreadStatic 存储，因此需要为字典存储进行深拷贝。
                var givenTagKeysAndValues = new KeyValuePair<string, object?>[length];

                tagKeysAndValues.CopyTo(givenTagKeysAndValues.AsSpan());

                givenTags = new Tags(givenTagKeysAndValues);

                Debug.Assert(this.availableMetricPoints != null, "this.availableMetricPoints 为 null");

                // 加锁以确保线程安全
                lock (this.TagsToMetricPointIndexDictionaryDelta)
                {
                    // 获取锁后再次检查字典中是否存在给定标签的聚合器索引
                    if (!this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(givenTags, out lookupData))
                    {
                        // 检查是否有可用的度量点
                        if (this.availableMetricPoints!.Count > 0)
                        {
                            index = this.availableMetricPoints.Dequeue();
                        }
                        else
                        {
                            // 没有可用的度量点
                            return -1;
                        }

                        lookupData = new LookupData(index, Tags.EmptyTags, givenTags);

                        ref var metricPoint = ref this.metricPoints[index];
                        metricPoint = new MetricPoint(this, this.aggType, givenTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);
                        newMetricPointCreated = true;

                        // 在初始化 MetricPoint 后添加到字典中，因为如果字典条目被找到，其他线程可以开始写入 MetricPoint。
                        // 给定标签长度为 1 时，givenTags 将始终排序
                        this.TagsToMetricPointIndexDictionaryDelta.TryAdd(givenTags, lookupData);
                    }
                }
            }
        }

        // 找到度量点
        index = lookupData.Index;

        // 如果运行线程创建了一个新的度量点，则 Snapshot 方法不能回收该度量点，因为度量点初始化时引用计数为 1。
        // 它可以简单地返回索引。

        if (!newMetricPointCreated)
        {
            // 如果运行线程没有创建度量点，它可能正在处理一个已被 Snapshot 方法回收的索引。
            // 这种情况可能发生在线程从 CPU 切换出去后检索到索引，但 Snapshot 方法在线程再次唤醒之前回收了该索引。

            ref var metricPointAtIndex = ref this.metricPoints[index];
            var referenceCount = Interlocked.Increment(ref metricPointAtIndex.ReferenceCount);

            if (referenceCount < 0)
            {
                // 罕见情况：Snapshot 方法已将度量点标记为可重用，因为它在上一个收集周期中未更新。

                // 示例场景：
                // 线程 T1 想要记录 (k1,v1) 的测量值。
                // 线程 T1 在索引 100 处创建一个新的度量点，并在字典中添加 (k1,v1) 的条目，相关的 LookupData 值；此时度量点的引用计数为 1。
                // 线程 T1 完成更新并将引用计数减为 0。
                // 稍后，另一个更新线程（也可能是 T1）想要记录 (k1,v1) 的测量值
                // 它查找字典并检索到索引 100。此时度量点的引用计数为 0。
                // 这个更新线程被 CPU 切换出去。
                // 使用回收行为，Snapshot 方法回收索引 100，因为该索引的度量点状态为 NoCollectPending，引用计数为 0。
                // Snapshot 线程将引用计数设置为 int.MinValue。
                // 更新线程唤醒并增加引用计数，但发现值为负数。

                // 重试获取度量点的尝试。
                index = this.RemoveStaleEntriesAndGetAvailableMetricPointRare(lookupData, length);
            }
            else if (metricPointAtIndex.LookupData != lookupData)
            {
                // 罕见情况：如果度量点被 Snapshot 方法释放，另一个线程可能会使用不同的输入标签回收该度量点。

                // 示例场景：
                // 线程 T1 想要记录 (k1,v1) 的测量值。
                // 线程 T1 在索引 100 处创建一个新的度量点，并在字典中添加 (k1,v1) 的条目，相关的 LookupData 值；此时度量点的引用计数为 1。
                // 线程 T1 完成更新并将引用计数减为 0。
                // 稍后，另一个更新线程 T2（也可能是 T1）想要记录 (k1,v1) 的测量值
                // 它查找字典并检索到索引 100。此时度量点的引用计数为 0。
                // 这个更新线程 T2 被 CPU 切换出去。
                // 使用回收行为，Snapshot 方法回收索引 100，因为该索引的度量点状态为 NoCollectPending，引用计数为 0。
                // Snapshot 线程将引用计数设置为 int.MinValue。
                // 更新线程 T3 想要记录 (k2,v2) 的测量值。
                // 线程 T3 查找可用索引并找到索引 100。
                // 线程 T3 在索引 100 处创建一个新的度量点，并在字典中添加 (k2,v2) 的条目，相关的 LookupData 值；此时度量点的引用计数为 1。
                // 更新线程 T2 唤醒并增加引用计数，发现值为正但 LookupData 值不匹配 (k1,v1)。

                // 删除引用，因为它不是正确的度量点。
                Interlocked.Decrement(ref metricPointAtIndex.ReferenceCount);

                // 重试获取度量点的尝试。
                index = this.RemoveStaleEntriesAndGetAvailableMetricPointRare(lookupData, length);
            }
        }

        return index;
    }

    // 这个方法总是在 `lock(this.tagsToMetricPointIndexDictionaryDelta)` 下调用，所以它与其他添加或删除 `this.tagsToMetricPointIndexDictionaryDelta` 条目的代码是安全的
    private bool TryGetAvailableMetricPointRare(
        Tags givenTags,
        Tags sortedTags,
        int length,
        [NotNullWhen(true)]
        out LookupData? lookupData,
        out bool newMetricPointCreated)
    {
        // 断言 TagsToMetricPointIndexDictionaryDelta 和 availableMetricPoints 不为 null
        Debug.Assert(this.TagsToMetricPointIndexDictionaryDelta != null, "this.tagsToMetricPointIndexDictionaryDelta 为 null");
        Debug.Assert(this.availableMetricPoints != null, "this.availableMetricPoints 为 null");

        int index;
        newMetricPointCreated = false;

        if (length > 1)
        {
            // 获取锁后再次检查字典中是否存在给定标签的聚合器索引
            if (!this.TagsToMetricPointIndexDictionaryDelta!.TryGetValue(givenTags, out lookupData) &&
                !this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(sortedTags, out lookupData))
            {
                // 检查是否有可用的度量点
                if (this.availableMetricPoints!.Count > 0)
                {
                    index = this.availableMetricPoints.Dequeue();
                }
                else
                {
                    // 没有可用的度量点
                    return false;
                }

                // 创建新的 LookupData 对象
                lookupData = new LookupData(index, sortedTags, givenTags);

                // 初始化度量点
                ref var metricPoint = ref this.metricPoints[index];
                metricPoint = new MetricPoint(this, this.aggType, sortedTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);
                newMetricPointCreated = true;

                // 在初始化 MetricPoint 后添加到字典中，因为如果字典条目被找到，其他线程可以开始写入 MetricPoint。
                // 添加排序后的标签和给定顺序的标签到字典中
                this.TagsToMetricPointIndexDictionaryDelta.TryAdd(sortedTags, lookupData);
                this.TagsToMetricPointIndexDictionaryDelta.TryAdd(givenTags, lookupData);
            }
        }
        else
        {
            // 获取锁后再次检查字典中是否存在给定标签的聚合器索引
            if (!this.TagsToMetricPointIndexDictionaryDelta!.TryGetValue(givenTags, out lookupData))
            {
                // 检查是否有可用的度量点
                if (this.availableMetricPoints!.Count > 0)
                {
                    index = this.availableMetricPoints.Dequeue();
                }
                else
                {
                    // 没有可用的度量点
                    return false;
                }

                // 创建新的 LookupData 对象
                lookupData = new LookupData(index, Tags.EmptyTags, givenTags);

                // 初始化度量点
                ref var metricPoint = ref this.metricPoints[index];
                metricPoint = new MetricPoint(this, this.aggType, givenTags.KeyValuePairs, this.histogramBounds, this.exponentialHistogramMaxSize, this.exponentialHistogramMaxScale, lookupData);
                newMetricPointCreated = true;

                // 在初始化 MetricPoint 后添加到字典中，因为如果字典条目被找到，其他线程可以开始写入 MetricPoint。
                // 给定标签长度为 1 时，givenTags 将始终排序
                this.TagsToMetricPointIndexDictionaryDelta.TryAdd(givenTags, lookupData);
            }
        }

        return true;
    }

    // 这个方法本质上是一个重试尝试，用于在 `LookupAggregatorStoreForDeltaWithReclaim` 无法找到 MetricPoint 时使用。
    // 如果在此方法中仍然无法找到 MetricPoint，我们不会进一步重试，而是简单地丢弃测量值。
    // 这个方法会获取 `lock (this.tagsToMetricPointIndexDictionaryDelta)`
    private int RemoveStaleEntriesAndGetAvailableMetricPointRare(LookupData lookupData, int length)
    {
        bool foundMetricPoint = false;
        bool newMetricPointCreated = false;
        var sortedTags = lookupData.SortedTags;
        var inputTags = lookupData.GivenTags;

        // 获取锁
        // 尝试从字典中删除过时的条目
        // 获取新 MetricPoint 的索引（它可能是自我声明的，也可能是另一个线程添加的新条目）
        // 如果是自我声明的，则在字典中添加新条目
        // 如果找到了可用的 MetricPoint，则只增加引用计数

        Debug.Assert(this.TagsToMetricPointIndexDictionaryDelta != null, "this.tagsToMetricPointIndexDictionaryDelta 为 null");

        // 删除这些标签的条目并获取另一个 MetricPoint。
        lock (this.TagsToMetricPointIndexDictionaryDelta!)
        {
            LookupData? dictionaryValue;
            if (lookupData.SortedTags != Tags.EmptyTags)
            {
                // 检查是否没有其他线程在此期间为相同的标签添加新条目。
                // 如果没有，则删除现有条目。
                if (this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(lookupData.SortedTags, out dictionaryValue))
                {
                    if (dictionaryValue == lookupData)
                    {
                        // 没有其他线程为相同的标签添加新条目。
                        this.TagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.SortedTags, out _);
                        this.TagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.GivenTags, out _);
                    }
                    else
                    {
                        // 一些其他线程为这些标签添加了新条目。使用新的 MetricPoint
                        lookupData = dictionaryValue;
                        foundMetricPoint = true;
                    }
                }
            }
            else
            {
                if (this.TagsToMetricPointIndexDictionaryDelta.TryGetValue(lookupData.GivenTags, out dictionaryValue))
                {
                    if (dictionaryValue == lookupData)
                    {
                        // 没有其他线程为相同的标签添加新条目。
                        this.TagsToMetricPointIndexDictionaryDelta.TryRemove(lookupData.GivenTags, out _);
                    }
                    else
                    {
                        // 一些其他线程为这些标签添加了新条目。使用新的 MetricPoint
                        lookupData = dictionaryValue;
                        foundMetricPoint = true;
                    }
                }
            }

            if (!foundMetricPoint
                && this.TryGetAvailableMetricPointRare(inputTags, sortedTags, length, out var tempLookupData, out newMetricPointCreated))
            {
                foundMetricPoint = true;
                lookupData = tempLookupData;
            }
        }

        if (foundMetricPoint)
        {
            var index = lookupData.Index;

            // 如果运行线程创建了一个新的 MetricPoint，则 Snapshot 方法不能回收该 MetricPoint，因为 MetricPoint 初始化时引用计数为 1。
            // 它可以简单地返回索引。

            if (!newMetricPointCreated)
            {
                // 如果运行线程没有创建 MetricPoint，它可能正在处理一个已被 Snapshot 方法回收的索引。
                // 这种情况可能发生在线程从 CPU 切换出去后检索到索引，但 Snapshot 方法在线程再次唤醒之前回收了该索引。

                ref var metricPointAtIndex = ref this.metricPoints[index];
                var referenceCount = Interlocked.Increment(ref metricPointAtIndex.ReferenceCount);

                if (referenceCount < 0)
                {
                    // 极少见的情况：Snapshot 方法已经将 MetricPoint 标记为可重用，因为它在上一个收集周期中未更新，即使在重试尝试中也是如此。
                    // 示例场景在 `LookupAggregatorStoreForDeltaWithReclaim` 方法中提到。

                    // 不再重试并丢弃测量值。
                    return -1;
                }
                else if (metricPointAtIndex.LookupData != lookupData)
                {
                    // 罕见情况：如果度量点被 Snapshot 方法释放，另一个线程可能会使用不同的输入标签回收该度量点。
                    // 示例场景在 `LookupAggregatorStoreForDeltaWithReclaim` 方法中提到。

                    // 删除引用，因为它不是正确的度量点。
                    Interlocked.Decrement(ref metricPointAtIndex.ReferenceCount);

                    // 不再重试并丢弃测量值。
                    return -1;
                }
            }

            return index;
        }
        else
        {
            // 没有可用的 MetricPoint
            return -1;
        }
    }

    // 更新长整型值
    // 这个函数的作用是更新长整型的度量值。它首先查找与给定标签匹配的度量点索引，然后调用 UpdateLongMetricPoint 方法更新度量点的值。
    private void UpdateLong(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        // 查找与给定标签匹配的度量点索引
        var index = this.FindMetricAggregatorsDefault(tags);

        // 更新度量点的值
        this.UpdateLongMetricPoint(index, value, tags);
    }

    // UpdateLongCustomTags 方法用于更新长整型值（自定义标签）
    // 这个函数的作用是更新长整型的度量值。它首先查找与给定标签匹配的度量点索引，然后调用 UpdateLongMetricPoint 方法更新度量点的值。
    private void UpdateLongCustomTags(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        // 查找与给定标签匹配的度量点索引
        var index = this.FindMetricAggregatorsCustomTag(tags);

        // 更新度量点的值
        this.UpdateLongMetricPoint(index, value, tags);
    }

    /// <summary>
    /// 更新长整型值的度量点
    /// </summary>
    /// <param name="metricPointIndex">度量点索引</param>
    /// <param name="value">长整型值</param>
    /// <param name="tags">标签</param>
    /// <remarks>
    /// 这个函数的作用是更新长整型的度量值。它首先检查度量点索引是否有效，如果无效则增加丢弃的测量数量并初始化溢出标签度量点。
    /// 然后根据示例过滤器类型决定是否更新度量点的值以及是否提供示例。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateLongMetricPoint(int metricPointIndex, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        // 检查度量点索引是否有效
        if (metricPointIndex < 0)
        {
            // 增加丢弃的测量数量
            Interlocked.Increment(ref this.DroppedMeasurements);
            // 初始化溢出标签度量点
            this.InitializeOverflowTagPointIfNotInitialized();
            // 更新溢出标签度量点的值
            this.metricPoints[1].Update(value);

            return;
        }

        // 获取示例过滤器类型
        var exemplarFilterType = this.exemplarFilter;
        // 根据示例过滤器类型决定是否更新度量点的值以及是否提供示例
        if (exemplarFilterType == ExemplarFilterType.AlwaysOff)
        {
            // 如果示例过滤器类型为 AlwaysOff，则直接更新度量点的值
            this.metricPoints[metricPointIndex].Update(value);
        }
        else if (exemplarFilterType == ExemplarFilterType.AlwaysOn)
        {
            // 如果示例过滤器类型为 AlwaysOn，则更新度量点的值并提供示例
            this.metricPoints[metricPointIndex].UpdateWithExemplar(
                value,
                tags,
                offerExemplar: true);
        }
        else
        {
            // 如果示例过滤器类型为 TraceBased，则根据当前活动是否记录来决定是否提供示例
            this.metricPoints[metricPointIndex].UpdateWithExemplar(
                value,
                tags,
                offerExemplar: Activity.Current?.Recorded ?? false);
        }
    }

    // 更新双精度值
    // 这个函数的作用是更新双精度的度量值。它首先查找与给定标签匹配的度量点索引，然后调用 UpdateDoubleMetricPoint 方法更新度量点的值。
    private void UpdateDouble(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        // 查找与给定标签匹配的度量点索引
        var index = this.FindMetricAggregatorsDefault(tags);

        // 更新度量点的值
        this.UpdateDoubleMetricPoint(index, value, tags);
    }

    // UpdateDoubleCustomTags 方法用于更新双精度值（自定义标签）
    // 这个函数的作用是更新双精度的度量值。它首先查找与给定标签匹配的度量点索引，然后调用 UpdateDoubleMetricPoint 方法更新度量点的值。
    private void UpdateDoubleCustomTags(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        // 查找与给定标签匹配的度量点索引
        var index = this.FindMetricAggregatorsCustomTag(tags);

        // 更新度量点的值
        this.UpdateDoubleMetricPoint(index, value, tags);
    }

    /// <summary>
    /// 更新双精度值的度量点
    /// </summary>
    /// <param name="metricPointIndex">度量点索引</param>
    /// <param name="value">双精度值</param>
    /// <param name="tags">标签</param>
    /// <remarks>
    /// 这个函数的作用是更新双精度的度量值。它首先检查度量点索引是否有效，如果无效则增加丢弃的测量数量并初始化溢出标签度量点。
    /// 然后根据示例过滤器类型决定是否更新度量点的值以及是否提供示例。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateDoubleMetricPoint(int metricPointIndex, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        // 检查度量点索引是否有效
        if (metricPointIndex < 0)
        {
            // 增加丢弃的测量数量
            Interlocked.Increment(ref this.DroppedMeasurements);
            // 初始化溢出标签度量点
            this.InitializeOverflowTagPointIfNotInitialized();
            // 更新溢出标签度量点的值
            this.metricPoints[1].Update(value);

            return;
        }

        // 获取示例过滤器类型
        var exemplarFilterType = this.exemplarFilter;
        // 根据示例过滤器类型决定是否更新度量点的值以及是否提供示例
        if (exemplarFilterType == ExemplarFilterType.AlwaysOff)
        {
            // 如果示例过滤器类型为 AlwaysOff，则直接更新度量点的值
            this.metricPoints[metricPointIndex].Update(value);
        }
        else if (exemplarFilterType == ExemplarFilterType.AlwaysOn)
        {
            // 如果示例过滤器类型为 AlwaysOn，则更新度量点的值并提供示例
            this.metricPoints[metricPointIndex].UpdateWithExemplar(
                value,
                tags,
                offerExemplar: true);
        }
        else
        {
            // 如果示例过滤器类型为 TraceBased，则根据当前活动是否记录来决定是否提供示例
            this.metricPoints[metricPointIndex].UpdateWithExemplar(
                value,
                tags,
                offerExemplar: Activity.Current?.Recorded ?? false);
        }
    }

    // 这个函数的作用是根据传入的标签键值对数组查找或创建相应的度量点索引。
    private int FindMetricAggregatorsDefault(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        // 获取标签的长度
        int tagLength = tags.Length;

        // 如果标签长度为0，表示没有标签
        if (tagLength == 0)
        {
            // 初始化零标签度量点（如果尚未初始化）
            this.InitializeZeroTagPointIfNotInitialized();
            // 返回零标签度量点的索引0
            return 0;
        }

        // 获取线程静态存储，用于临时存储标签键值对
        var storage = ThreadStaticStorage.GetStorage();

        // 将标签拆分为键和值，并存储在tagKeysAndValues中
        storage.SplitToKeysAndValues(tags, tagLength, out var tagKeysAndValues);

        // 查找或创建度量点索引
        return this.lookupAggregatorStore(tagKeysAndValues, tagLength);
    }

    // FindMetricAggregatorsCustomTag 方法用于查找或创建自定义标签的度量点索引
    // 这个函数的作用是根据传入的标签键值对数组查找或创建相应的度量点索引。
    // 如果标签长度为0或感兴趣的标签键数量为0，则初始化零标签度量点并返回索引0。
    // 否则，将标签拆分为键和值，并存储在tagKeysAndValues中，然后查找或创建度量点索引。
    private int FindMetricAggregatorsCustomTag(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        // 获取标签的长度
        int tagLength = tags.Length;

        // 如果标签长度为0或感兴趣的标签键数量为0
        if (tagLength == 0 || this.tagsKeysInterestingCount == 0)
        {
            // 初始化零标签度量点（如果尚未初始化）
            this.InitializeZeroTagPointIfNotInitialized();
            // 返回零标签度量点的索引0
            return 0;
        }

        // 获取线程静态存储，用于临时存储标签键值对
        var storage = ThreadStaticStorage.GetStorage();

        // 断言感兴趣的标签键集合不为null
        Debug.Assert(this.TagKeysInteresting != null, "this.TagKeysInteresting was null");

        // 将标签拆分为键和值，并存储在tagKeysAndValues中
        storage.SplitToKeysAndValues(tags, tagLength, this.TagKeysInteresting!, out var tagKeysAndValues, out var actualLength);

        // 实际的标签数量取决于用户选择的标签数量
        if (actualLength == 0)
        {
            // 初始化零标签度量点（如果尚未初始化）
            this.InitializeZeroTagPointIfNotInitialized();
            // 返回零标签度量点的索引0
            return 0;
        }

        // 断言tagKeysAndValues不为null
        Debug.Assert(tagKeysAndValues != null, "tagKeysAndValues was null");

        // 查找或创建度量点索引
        return this.lookupAggregatorStore(tagKeysAndValues!, actualLength);
    }
}
