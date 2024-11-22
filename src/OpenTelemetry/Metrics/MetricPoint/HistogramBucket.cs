// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 表示直方图度量类型中的一个桶。
/// </summary>
public readonly struct HistogramBucket
{
    // 内部构造函数，初始化HistogramBucket结构的实例
    internal HistogramBucket(double explicitBound, long bucketCount)
    {
        // 设置桶的边界值
        this.ExplicitBound = explicitBound;
        // 设置桶中的项目计数
        this.BucketCount = bucketCount;
    }

    /// <summary>
    /// 获取桶的配置边界值或catch-all桶的<see cref="double.PositiveInfinity"/>。
    /// </summary>
    public double ExplicitBound { get; }

    /// <summary>
    /// 获取桶中的项目计数。
    /// </summary>
    public long BucketCount { get; }
}
