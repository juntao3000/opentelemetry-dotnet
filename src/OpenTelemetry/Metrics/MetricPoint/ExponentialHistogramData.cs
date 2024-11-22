// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 包含指数直方图的数据。
/// </summary>
public sealed class ExponentialHistogramData
{
    // 构造函数，初始化 ExponentialHistogramData 实例
    internal ExponentialHistogramData()
    {
        // 初始化正向桶
        this.PositiveBuckets = new();
        // 初始化负向桶
        this.NegativeBuckets = new();
    }

    /// <summary>
    /// 获取指数直方图的比例。
    /// </summary>
    public int Scale { get; internal set; }

    /// <summary>
    /// 获取指数直方图的零计数。
    /// </summary>
    public long ZeroCount { get; internal set; }

    /// <summary>
    /// 获取指数直方图的正向桶。
    /// </summary>
    public ExponentialHistogramBuckets PositiveBuckets { get; private set; }

    /// <summary>
    /// 获取指数直方图的负向桶。
    /// </summary>
    internal ExponentialHistogramBuckets NegativeBuckets { get; private set; }

    // 复制当前 ExponentialHistogramData 实例
    internal ExponentialHistogramData Copy()
    {
        // 创建 ExponentialHistogramData 的副本
        var copy = new ExponentialHistogramData
        {
            // 复制比例
            Scale = this.Scale,
            // 复制零计数
            ZeroCount = this.ZeroCount,
            // 复制正向桶
            PositiveBuckets = this.PositiveBuckets.Copy(),
            // 复制负向桶
            NegativeBuckets = this.NegativeBuckets.Copy(),
        };
        return copy;
    }
}
