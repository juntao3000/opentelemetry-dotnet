// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif
using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Exemplar 实现。
/// </summary>
/// <remarks>
/// 规范: <see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplar"/>。
/// </remarks>
public struct Exemplar
{
#if NET
    // 视图定义的标签键集合
    internal FrozenSet<string>? ViewDefinedTagKeys;
#else
        // 视图定义的标签键集合
        internal HashSet<string>? ViewDefinedTagKeys;
#endif

    // 空的只读过滤标签集合
    private static readonly ReadOnlyFilteredTagCollection Empty = new(excludedKeys: null, Array.Empty<KeyValuePair<string, object?>>(), count: 0);
    // 标签数量
    private int tagCount;
    // 标签存储
    private KeyValuePair<string, object?>[]? tagStorage;
    // 指标点值存储
    private MetricPointValueStorage valueStorage;
    // 是否处于临界区
    private int isCriticalSectionOccupied;

    /// <summary>
    /// 获取时间戳。
    /// </summary>
    public DateTimeOffset Timestamp { readonly get; private set; }

    /// <summary>
    /// 获取 TraceId。
    /// </summary>
    public ActivityTraceId TraceId { readonly get; private set; }

    /// <summary>
    /// 获取 SpanId。
    /// </summary>
    public ActivitySpanId SpanId { readonly get; private set; }

    /// <summary>
    /// 获取 long 类型的值。
    /// </summary>
    public long LongValue
    {
        readonly get => this.valueStorage.AsLong;
        private set => this.valueStorage.AsLong = value;
    }

    /// <summary>
    /// 获取 double 类型的值。
    /// </summary>
    public double DoubleValue
    {
        readonly get => this.valueStorage.AsDouble;
        private set => this.valueStorage.AsDouble = value;
    }

    /// <summary>
    /// 获取过滤后的标签。
    /// </summary>
    /// <remarks>
    /// 注意: <see cref="FilteredTags"/> 表示在测量时提供但由于视图配置的过滤而被丢弃的标签集合
    /// (<see cref="MetricStreamConfiguration.TagKeys"/>)。如果未配置视图标签过滤，<see cref="FilteredTags"/> 将为空。
    /// </remarks>
    public readonly ReadOnlyFilteredTagCollection FilteredTags
    {
        get
        {
            if (this.tagCount == 0)
            {
                return Empty;
            }
            else
            {
                Debug.Assert(this.tagStorage != null, "tagStorage was null");

                return new(this.ViewDefinedTagKeys, this.tagStorage!, this.tagCount);
            }
        }
    }

    /// <summary>
    /// 更新 Exemplar。
    /// </summary>
    internal void Update<T>(in ExemplarMeasurement<T> measurement)
        where T : struct
    {
        if (Interlocked.Exchange(ref this.isCriticalSectionOccupied, 1) != 0)
        {
            // 注意: 如果到达这里，意味着其他线程已经在更新 Exemplar。为了避免自旋，我们中止。
            // 这样做的目的是，对于几乎同时提供的两个 Exemplar，存储哪一个实际上没有区别，
            // 因此这是一种优化，让失败的线程回到工作中，而不是自旋。
            return;
        }

        this.Timestamp = DateTimeOffset.UtcNow;

        if (typeof(T) == typeof(long))
        {
            this.LongValue = (long)(object)measurement.Value;
        }
        else if (typeof(T) == typeof(double))
        {
            this.DoubleValue = (double)(object)measurement.Value;
        }
        else
        {
            Debug.Fail("Invalid value type");
            this.DoubleValue = Convert.ToDouble(measurement.Value);
        }

        var currentActivity = Activity.Current;
        if (currentActivity != null)
        {
            this.TraceId = currentActivity.TraceId;
            this.SpanId = currentActivity.SpanId;
        }
        else
        {
            this.TraceId = default;
            this.SpanId = default;
        }

        if (this.ViewDefinedTagKeys != null)
        {
            this.StoreRawTags(measurement.Tags);
        }

        Interlocked.Exchange(ref this.isCriticalSectionOccupied, 0);
    }

    /// <summary>
    /// 重置 Exemplar。
    /// </summary>
    internal void Reset()
    {
        this.Timestamp = default;
    }

    /// <summary>
    /// 判断 Exemplar 是否已更新。
    /// </summary>
    internal readonly bool IsUpdated()
    {
        return this.Timestamp != default;
    }

    /// <summary>
    /// 收集 Exemplar。
    /// </summary>
    internal void Collect(ref Exemplar destination, bool reset)
    {
        if (Interlocked.Exchange(ref this.isCriticalSectionOccupied, 1) != 0)
        {
            this.AcquireLockRare();
        }

        if (this.IsUpdated())
        {
            this.Copy(ref destination);
            if (reset)
            {
                this.Reset();
            }
        }
        else
        {
            destination.Reset();
        }

        Interlocked.Exchange(ref this.isCriticalSectionOccupied, 0);
    }

    /// <summary>
    /// 复制 Exemplar。
    /// </summary>
    internal readonly void Copy(ref Exemplar destination)
    {
        destination.Timestamp = this.Timestamp;
        destination.TraceId = this.TraceId;
        destination.SpanId = this.SpanId;
        destination.valueStorage = this.valueStorage;
        destination.ViewDefinedTagKeys = this.ViewDefinedTagKeys;
        destination.tagCount = this.tagCount;
        if (destination.tagCount > 0)
        {
            Debug.Assert(this.tagStorage != null, "tagStorage was null");

            destination.tagStorage = new KeyValuePair<string, object?>[destination.tagCount];
            Array.Copy(this.tagStorage!, 0, destination.tagStorage, 0, destination.tagCount);
        }
    }

    /// <summary>
    /// 存储原始标签。
    /// </summary>
    private void StoreRawTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        this.tagCount = tags.Length;
        if (tags.Length == 0)
        {
            return;
        }

        if (this.tagStorage == null || this.tagStorage.Length < this.tagCount)
        {
            this.tagStorage = new KeyValuePair<string, object?>[this.tagCount];
        }

        tags.CopyTo(this.tagStorage);
    }

    /// <summary>
    /// 获取锁（罕见情况）。
    /// </summary>
    private void AcquireLockRare()
    {
        SpinWait spinWait = default;
        do
        {
            spinWait.SpinOnce();
        }
        while (Interlocked.Exchange(ref this.isCriticalSectionOccupied, 1) != 0);
    }
}
