// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// AlignedHistogramBucketExemplarReservoir 实现。
/// </summary>
/// <remarks>
/// 规范: <see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#alignedhistogrambucketexemplarreservoir"/>.
/// </remarks>
internal sealed class AlignedHistogramBucketExemplarReservoir : FixedSizeExemplarReservoir
{
    /// <summary>
    /// 构造函数，初始化 AlignedHistogramBucketExemplarReservoir 实例。
    /// </summary>
    /// <param name="numberOfBuckets">桶的数量。</param>
    public AlignedHistogramBucketExemplarReservoir(int numberOfBuckets)
        : base(numberOfBuckets + 1) // 调用基类构造函数，桶的数量加1
    {
    }

    /// <summary>
    /// 提供一个测量值（long 类型）。
    /// </summary>
    /// <param name="measurement">测量值。</param>
    public override void Offer(in ExemplarMeasurement<long> measurement)
    {
        Debug.Fail("AlignedHistogramBucketExemplarReservoir 不应与 long 类型值一起使用"); // 如果使用 long 类型值，则失败
    }

    /// <summary>
    /// 提供一个测量值（double 类型）。
    /// </summary>
    /// <param name="measurement">测量值。</param>
    public override void Offer(in ExemplarMeasurement<double> measurement)
    {
        Debug.Assert(
            measurement.ExplicitBucketHistogramBucketIndex != -1, // 确保 ExplicitBucketHistogramBucketIndex 不为 -1
            "ExplicitBucketHistogramBucketIndex 为 -1");

        this.UpdateExemplar(measurement.ExplicitBucketHistogramBucketIndex, in measurement); // 更新示例
    }
}
