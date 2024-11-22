// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 表示一个以 2 ^ (2 ^ (-scale)) 为底的指数桶直方图。
/// 指数桶直方图有无限数量的桶，这些桶由 <c>Bucket[index] = ( base ^ index, base ^ (index + 1) ]</c> 标识，其中 <c>index</c> 是一个整数。
/// </summary>
internal sealed partial class Base2ExponentialBucketHistogram
{
    // 运行时的总和
    internal double RunningSum;
    // 快照时的总和
    internal double SnapshotSum;

    // 运行时的最小值
    internal double RunningMin = double.PositiveInfinity;
    // 快照时的最小值
    internal double SnapshotMin;

    // 运行时的最大值
    internal double RunningMax = double.NegativeInfinity;
    // 快照时的最大值
    internal double SnapshotMax;

    // 快照时的指数直方图数据
    internal ExponentialHistogramData SnapshotExponentialHistogramData = new();

    // 缩放因子
    private int scale;
    // 缩放因子计算结果 2 ^ scale / log(2)
    private double scalingFactor;

    /// <summary>
    /// 初始化 <see cref="Base2ExponentialBucketHistogram"/> 类的新实例。
    /// </summary>
    /// <param name="maxBuckets">
    /// 正负范围内每个范围的最大桶数，不包括特殊的零桶。默认值为 160。
    /// </param>
    /// <param name="scale">
    /// 最大缩放因子。默认值为 20。
    /// </param>
    public Base2ExponentialBucketHistogram(int maxBuckets = 160, int scale = 20)
    {
        /*
        以下表格是基于 [ MapToIndex(double.Epsilon), MapToIndex(double.MaxValue) ] 计算的：

        | Scale | Index Range               |
        | ----- | ------------------------- |
        | < -11 | [-1, 0]                   |
        | -11   | [-1, 0]                   |
        | -10   | [-2, 0]                   |
        | -9    | [-3, 1]                   |
        | -8    | [-5, 3]                   |
        | -7    | [-9, 7]                   |
        | -6    | [-17, 15]                 |
        | -5    | [-34, 31]                 |
        | -4    | [-68, 63]                 |
        | -3    | [-135, 127]               |
        | -2    | [-269, 255]               |
        | -1    | [-538, 511]               |
        | 0     | [-1075, 1023]             |
        | 1     | [-2149, 2047]             |
        | 2     | [-4297, 4095]             |
        | 3     | [-8593, 8191]             |
        | 4     | [-17185, 16383]           |
        | 5     | [-34369, 32767]           |
        | 6     | [-68737, 65535]           |
        | 7     | [-137473, 131071]         |
        | 8     | [-274945, 262143]         |
        | 9     | [-549889, 524287]         |
        | 10    | [-1099777, 1048575]       |
        | 11    | [-2199553, 2097151]       |
        | 12    | [-4399105, 4194303]       |
        | 13    | [-8798209, 8388607]       |
        | 14    | [-17596417, 16777215]     |
        | 15    | [-35192833, 33554431]     |
        | 16    | [-70385665, 67108863]     |
        | 17    | [-140771329, 134217727]   |
        | 18    | [-281542657, 268435455]   |
        | 19    | [-563085313, 536870911]   |
        | 20    | [-1126170625, 1073741823] |
        | 21    | [underflow, 2147483647]   |
        | > 21  | [underflow, overflow]     |
        */
        Guard.ThrowIfOutOfRange(scale, min: -11, max: 20);

        /*
        无论缩放因子如何，MapToIndex(1) 总是 -1，所以我们至少需要两个桶：
            bucket[-1] = (1/base, 1]
            bucket[0] = (1, base]
        */
        Guard.ThrowIfOutOfRange(maxBuckets, min: 2);

        this.Scale = scale;
        this.PositiveBuckets = new CircularBufferBuckets(maxBuckets);
        this.NegativeBuckets = new CircularBufferBuckets(maxBuckets);
    }

    // 缩放因子属性
    internal int Scale
    {
        get => this.scale;

        set
        {
            this.scale = value;

            // Math.ScaleB(Math.Log2(Math.E), value) 的子集
            this.scalingFactor = BitConverter.Int64BitsToDouble(0x71547652B82FEL | ((0x3FFL + value) << 52 /* fraction width */));
        }
    }

    // 缩放因子计算结果属性
    internal double ScalingFactor => this.scalingFactor;

    // 正数桶
    internal CircularBufferBuckets PositiveBuckets { get; }

    // 零值计数
    internal long ZeroCount { get; private set; }

    // 负数桶
    internal CircularBufferBuckets NegativeBuckets { get; }

    /// <summary>
    /// 将有限的正 IEEE 754 双精度浮点数映射到 <c>Bucket[index] = ( base ^ index, base ^ (index + 1) ]</c>，其中 <c>index</c> 是一个整数。
    /// </summary>
    /// <param name="value">
    /// 要进行桶化的值。必须是有限的正数。
    /// </param>
    /// <returns>
    /// 返回桶的索引。
    /// </returns>
    public int MapToIndex(double value)
    {
        Debug.Assert(MathHelper.IsFinite(value), "IEEE-754 +Inf, -Inf 和 NaN 应在调用此方法之前被过滤掉。");
        Debug.Assert(value != 0, "IEEE-754 零值应由 ZeroCount 处理。");
        Debug.Assert(value > 0, "IEEE-754 负值应在调用此方法之前进行归一化。");

        var bits = BitConverter.DoubleToInt64Bits(value);
        var fraction = bits & 0xFFFFFFFFFFFFFL /* fraction mask */;

        if (this.Scale > 0)
        {
            // TODO: 鉴于缩放因子>0时需要查找表，我们真的需要这个吗？
            if (fraction == 0)
            {
                var exp = (int)((bits & 0x7FF0000000000000L /* exponent mask */) >> 52 /* fraction width */);
                return ((exp - 1023 /* exponent bias */) << this.Scale) - 1;
            }

            // TODO: 由于精度问题，接近桶边界的值应仔细检查以避免偏差。

            return (int)Math.Ceiling(Math.Log(value) * this.scalingFactor) - 1;
        }
        else
        {
            var exp = (int)((bits & 0x7FF0000000000000L /* exponent mask */) >> 52 /* fraction width */);

            if (exp == 0)
            {
                exp -= MathHelper.LeadingZero64(fraction - 1) - 12 /* 64 - fraction width */;
            }
            else if (fraction == 0)
            {
                exp--;
            }

            return (exp - 1023 /* exponent bias */) >> -this.Scale;
        }
    }

    /// <summary>
    /// 记录一个值到直方图中。
    /// </summary>
    /// <param name="value">要记录的值。</param>
    public void Record(double value)
    {
        if (!MathHelper.IsFinite(value))
        {
            return;
        }

        var c = value.CompareTo(0);

        if (c == 0)
        {
            this.ZeroCount++;
            return;
        }

        var index = this.MapToIndex(c > 0 ? value : -value);
        var buckets = c > 0 ? this.PositiveBuckets : this.NegativeBuckets;
        var n = buckets.TryIncrement(index);

        if (n == 0)
        {
            return;
        }

        this.PositiveBuckets.ScaleDown(n);
        this.NegativeBuckets.ScaleDown(n);
        this.Scale -= n;
        n = buckets.TryIncrement(index >> n);
        Debug.Assert(n == 0, "缩放后增量应始终成功。");
    }

    /// <summary>
    /// 重置直方图。
    /// </summary>
    internal void Reset()
    {
        // TODO: 确定这是否足以用于增量时间性。
        // 我不确定我们是否应该重置缩放因子。
        this.ZeroCount = 0;
        this.PositiveBuckets.Reset();
        this.NegativeBuckets.Reset();
    }

    /// <summary>
    /// 创建直方图的快照。
    /// </summary>
    internal void Snapshot()
    {
        this.SnapshotExponentialHistogramData.Scale = this.Scale;
        this.SnapshotExponentialHistogramData.ZeroCount = this.ZeroCount;
        this.SnapshotExponentialHistogramData.PositiveBuckets.SnapshotBuckets(this.PositiveBuckets);
        this.SnapshotExponentialHistogramData.NegativeBuckets.SnapshotBuckets(this.NegativeBuckets);
    }

    /// <summary>
    /// 获取快照的指数直方图数据。
    /// </summary>
    /// <returns>指数直方图数据。</returns>
    internal ExponentialHistogramData GetExponentialHistogramData()
    {
        return this.SnapshotExponentialHistogramData;
    }

    /// <summary>
    /// 复制当前直方图。
    /// </summary>
    /// <returns>复制的直方图。</returns>
    internal Base2ExponentialBucketHistogram Copy()
    {
        Debug.Assert(this.PositiveBuckets.Capacity == this.NegativeBuckets.Capacity, "正负桶的容量不相等。");

        return new Base2ExponentialBucketHistogram(this.PositiveBuckets.Capacity, this.SnapshotExponentialHistogramData.Scale)
        {
            SnapshotSum = this.SnapshotSum,
            SnapshotMin = this.SnapshotMin,
            SnapshotMax = this.SnapshotMax,
            SnapshotExponentialHistogramData = this.SnapshotExponentialHistogramData.Copy(),
        };
    }
}
