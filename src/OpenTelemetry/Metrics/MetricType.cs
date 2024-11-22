// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 枚举用于定义<see cref="Metric"/>的类型。
/// </summary>
[Flags]
public enum MetricType : byte
{
    /*
    类型:
        0x10: Sum (求和)
        0x20: Gauge (测量)
        0x30: Summary (保留)
        0x40: Histogram (直方图)
        0x50: ExponentialHistogram (指数直方图)
        0x60: (未使用)
        0x70: (未使用)
        0x80: SumNonMonotonic (非单调求和)

    点类型:
        0x04: I1 (有符号1字节整数)
        0x05: U1 (无符号1字节整数)
        0x06: I2 (有符号2字节整数)
        0x07: U2 (无符号2字节整数)
        0x08: I4 (有符号4字节整数)
        0x09: U4 (无符号4字节整数)
        0x0a: I8 (有符号8字节整数)
        0x0b: U8 (无符号8字节整数)
        0x0c: R4 (4字节浮点数)
        0x0d: R8 (8字节浮点数)
    */

    /// <summary>
    /// Long类型的求和。
    /// </summary>
    LongSum = 0x1a,

    /// <summary>
    /// Double类型的求和。
    /// </summary>
    DoubleSum = 0x1d,

    /// <summary>
    /// Long类型的测量。
    /// </summary>
    LongGauge = 0x2a,

    /// <summary>
    /// Double类型的测量。
    /// </summary>
    DoubleGauge = 0x2d,

    /// <summary>
    /// 直方图。
    /// </summary>
    Histogram = 0x40,

    /// <summary>
    /// 指数直方图。
    /// </summary>
    ExponentialHistogram = 0x50,

    /// <summary>
    /// Long类型的非单调求和。
    /// </summary>
    LongSumNonMonotonic = 0x8a,

    /// <summary>
    /// Double类型的非单调求和。
    /// </summary>
    DoubleSumNonMonotonic = 0x8d,
}
