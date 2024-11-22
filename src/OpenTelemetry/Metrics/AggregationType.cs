// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

// AggregationType 枚举定义了不同的聚合类型
internal enum AggregationType
{
    /// <summary>
    /// 无效。
    /// </summary>
    Invalid = -1,

    /// <summary>
    /// 从传入的增量测量值计算总和。
    /// </summary>
    LongSumIncomingDelta = 0,

    /// <summary>
    /// 从传入的累积测量值计算总和。
    /// </summary>
    LongSumIncomingCumulative = 1,

    /// <summary>
    /// 从传入的增量测量值计算总和。
    /// </summary>
    DoubleSumIncomingDelta = 2,

    /// <summary>
    /// 从传入的累积测量值计算总和。
    /// </summary>
    DoubleSumIncomingCumulative = 3,

    /// <summary>
    /// 保持最后一个值。
    /// </summary>
    LongGauge = 4,

    /// <summary>
    /// 保持最后一个值。
    /// </summary>
    DoubleGauge = 5,

    /// <summary>
    /// 具有总和、计数和桶的直方图。
    /// </summary>
    HistogramWithBuckets = 6,

    /// <summary>
    /// 具有总和、计数、最小值、最大值和桶的直方图。
    /// </summary>
    HistogramWithMinMaxBuckets = 7,

    /// <summary>
    /// 具有总和和计数的直方图。
    /// </summary>
    Histogram = 8,

    /// <summary>
    /// 具有总和、计数、最小值和最大值的直方图。
    /// </summary>
    HistogramWithMinMax = 9,

    /// <summary>
    /// 具有总和和计数的指数直方图。
    /// </summary>
    Base2ExponentialHistogram = 10,

    /// <summary>
    /// 具有总和、计数、最小值和最大值的指数直方图。
    /// </summary>
    Base2ExponentialHistogramWithMinMax = 11,
}
