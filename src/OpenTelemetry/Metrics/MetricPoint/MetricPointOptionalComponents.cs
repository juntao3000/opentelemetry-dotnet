// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 存储度量点的可选组件。
/// Histogram, Exemplar 是当前组件。
/// ExponentialHistogram 是未来组件。
/// 这样做是为了控制 MetricPoint（结构）的大小。
/// </summary>
internal sealed class MetricPointOptionalComponents
{
    // 直方图桶
    public HistogramBuckets? HistogramBuckets;

    // 基于2的指数桶直方图
    public Base2ExponentialBucketHistogram? Base2ExponentialBucketHistogram;

    // 示例库
    public ExemplarReservoir? ExemplarReservoir;

    // 只读示例集合，初始化为空集合
    public ReadOnlyExemplarCollection Exemplars = ReadOnlyExemplarCollection.Empty;

    // 标志是否有线程占用了临界区
    private int isCriticalSectionOccupied = 0;

    /// <summary>
    /// 复制当前的 MetricPointOptionalComponents 实例。
    /// </summary>
    /// <returns>返回一个新的 MetricPointOptionalComponents 实例。</returns>
    public MetricPointOptionalComponents Copy()
    {
        MetricPointOptionalComponents copy = new MetricPointOptionalComponents
        {
            HistogramBuckets = this.HistogramBuckets?.Copy(),
            Base2ExponentialBucketHistogram = this.Base2ExponentialBucketHistogram?.Copy(),
            Exemplars = this.Exemplars.Copy(),
        };

        return copy;
    }

    /// <summary>
    /// 获取锁。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AcquireLock()
    {
        if (Interlocked.Exchange(ref this.isCriticalSectionOccupied, 1) != 0)
        {
            this.AcquireLockRare();
        }
    }

    /// <summary>
    /// 释放锁。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReleaseLock()
    {
        Interlocked.Exchange(ref this.isCriticalSectionOccupied, 0);
    }

    /// <summary>
    /// 获取锁的稀有情况处理。
    /// </summary>
    /// <remarks>
    /// 这个方法被标记为 NoInlining，因为它的目的是避免初始化 SpinWait，除非有必要。
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AcquireLockRare()
    {
        var sw = default(SpinWait);
        do
        {
            sw.SpinOnce();
        }
        while (Interlocked.Exchange(ref this.isCriticalSectionOccupied, 1) != 0);
    }
}
