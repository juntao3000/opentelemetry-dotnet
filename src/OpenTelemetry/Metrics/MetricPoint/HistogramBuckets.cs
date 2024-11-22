// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 与直方图度量类型关联的 <see cref="HistogramBucket"/> 集合。
/// </summary>
// 注意：不实现 IEnumerable<> 以防止意外装箱。
public class HistogramBuckets
{
    // 默认的二分查找边界数量
    internal const int DefaultBoundaryCountForBinarySearch = 50;

    // 显式边界数组
    internal readonly double[]? ExplicitBounds;

    // 直方图桶计数数组
    internal readonly HistogramBucketValues[] BucketCounts;

    // 运行中的总和
    internal double RunningSum;
    // 快照总和
    internal double SnapshotSum;

    // 运行中的最小值
    internal double RunningMin = double.PositiveInfinity;
    // 快照最小值
    internal double SnapshotMin;

    // 运行中的最大值
    internal double RunningMax = double.NegativeInfinity;
    // 快照最大值
    internal double SnapshotMax;

    // 桶查找树的根节点
    private readonly BucketLookupNode? bucketLookupTreeRoot;

    // 查找直方图桶索引的函数
    private readonly Func<double, int> findHistogramBucketIndex;

    // 构造函数，初始化 HistogramBuckets 实例
    internal HistogramBuckets(double[]? explicitBounds)
    {
        this.ExplicitBounds = explicitBounds;
        this.findHistogramBucketIndex = this.FindBucketIndexLinear;
        if (explicitBounds != null && explicitBounds.Length >= DefaultBoundaryCountForBinarySearch)
        {
            this.bucketLookupTreeRoot = ConstructBalancedBST(explicitBounds, 0, explicitBounds.Length)!;
            this.findHistogramBucketIndex = this.FindBucketIndexBinary;

            // 构造平衡二叉搜索树
            static BucketLookupNode? ConstructBalancedBST(double[] values, int min, int max)
            {
                if (min == max)
                {
                    return null;
                }

                int median = min + ((max - min) / 2);
                return new BucketLookupNode
                {
                    Index = median,
                    UpperBoundInclusive = values[median],
                    LowerBoundExclusive = median > 0 ? values[median - 1] : double.NegativeInfinity,
                    Left = ConstructBalancedBST(values, min, median),
                    Right = ConstructBalancedBST(values, median + 1, max),
                };
            }
        }

        this.BucketCounts = explicitBounds != null ? new HistogramBucketValues[explicitBounds.Length + 1] : Array.Empty<HistogramBucketValues>();
    }

    /// <summary>
    /// 返回一个枚举器，该枚举器可遍历 <see cref="HistogramBuckets"/>。
    /// </summary>
    /// <returns><see cref="Enumerator"/>。</returns>
    public Enumerator GetEnumerator() => new(this);

    // 复制当前的 HistogramBuckets 实例
    internal HistogramBuckets Copy()
    {
        HistogramBuckets copy = new HistogramBuckets(this.ExplicitBounds);

        Array.Copy(this.BucketCounts, copy.BucketCounts, this.BucketCounts.Length);
        copy.SnapshotSum = this.SnapshotSum;
        copy.SnapshotMin = this.SnapshotMin;
        copy.SnapshotMax = this.SnapshotMax;

        return copy;
    }

    // 查找桶索引
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int FindBucketIndex(double value)
    {
        return this.findHistogramBucketIndex(value);
    }

    // 使用二分查找查找桶索引
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int FindBucketIndexBinary(double value)
    {
        BucketLookupNode? current = this.bucketLookupTreeRoot;

        Debug.Assert(current != null, "Bucket root was null.");

        do
        {
            if (value <= current!.LowerBoundExclusive)
            {
                current = current.Left;
            }
            else if (value > current.UpperBoundInclusive)
            {
                current = current.Right;
            }
            else
            {
                return current.Index;
            }
        }
        while (current != null);

        Debug.Assert(this.ExplicitBounds != null, "ExplicitBounds was null.");

        return this.ExplicitBounds!.Length;
    }

    // 使用线性查找查找桶索引
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int FindBucketIndexLinear(double value)
    {
        Debug.Assert(this.ExplicitBounds != null, "ExplicitBounds was null.");

        int i;
        for (i = 0; i < this.ExplicitBounds!.Length; i++)
        {
            // 上边界是包含的
            if (value <= this.ExplicitBounds[i])
            {
                break;
            }
        }

        return i;
    }

    // 快照当前的桶计数
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Snapshot(bool outputDelta)
    {
        var bucketCounts = this.BucketCounts;

        if (outputDelta)
        {
            for (int i = 0; i < bucketCounts.Length; i++)
            {
                ref var values = ref bucketCounts[i];
                ref var running = ref values.RunningValue;
                values.SnapshotValue = running;
                running = 0L;
            }
        }
        else
        {
            for (int i = 0; i < bucketCounts.Length; i++)
            {
                ref var values = ref bucketCounts[i];
                values.SnapshotValue = values.RunningValue;
            }
        }
    }

    /// <summary>
    /// 枚举 <see cref="HistogramBuckets"/> 的元素。
    /// </summary>
    // 注意：不实现 IEnumerator<> 以防止意外装箱。
    public struct Enumerator
    {
        // 桶的数量
        private readonly int numberOfBuckets;
        // 直方图测量值
        private readonly HistogramBuckets histogramMeasurements;
        // 当前索引
        private int index;

        // 构造函数，初始化 Enumerator 实例
        internal Enumerator(HistogramBuckets histogramMeasurements)
        {
            this.histogramMeasurements = histogramMeasurements;
            this.index = 0;
            this.Current = default;
            this.numberOfBuckets = histogramMeasurements.BucketCounts.Length;
        }

        /// <summary>
        /// 获取枚举器当前位置的 <see cref="HistogramBucket"/>。
        /// </summary>
        public HistogramBucket Current { get; private set; }

        /// <summary>
        /// 将枚举器推进到 <see cref="HistogramBuckets"/> 的下一个元素。
        /// </summary>
        /// <returns>如果枚举器成功地推进到下一个元素，则为 <see langword="true"/>；如果枚举器已越过集合的末尾，则为 <see langword="false"/>。</returns>
        public bool MoveNext()
        {
            if (this.index < this.numberOfBuckets)
            {
                double explicitBound = this.index < this.numberOfBuckets - 1
                    ? this.histogramMeasurements.ExplicitBounds![this.index]
                    : double.PositiveInfinity;
                long bucketCount = this.histogramMeasurements.BucketCounts[this.index].SnapshotValue;
                this.Current = new HistogramBucket(explicitBound, bucketCount);
                this.index++;
                return true;
            }

            return false;
        }
    }

    // 直方图桶值结构
    internal struct HistogramBucketValues
    {
        // 运行中的值
        public long RunningValue;
        // 快照值
        public long SnapshotValue;
    }

    // 桶查找节点类
    private sealed class BucketLookupNode
    {
        // 上边界（包含）
        public double UpperBoundInclusive { get; set; }

        // 下边界（不包含）
        public double LowerBoundExclusive { get; set; }

        // 索引
        public int Index { get; set; }

        // 左子节点
        public BucketLookupNode? Left { get; set; }

        // 右子节点
        public BucketLookupNode? Right { get; set; }
    }
}
