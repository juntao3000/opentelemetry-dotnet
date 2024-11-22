// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 包含用于对<see cref="MetricType"/>类执行常见操作的扩展方法。
/// </summary>
public static class MetricTypeExtensions
{
#pragma warning disable SA1310 // field should not contain an underscore

    // 掩码，用于提取MetricType的类型部分
    internal const MetricType METRIC_TYPE_MASK = (MetricType)0xf0;

    // 单调求和类型
    internal const MetricType METRIC_TYPE_MONOTONIC_SUM = (MetricType)0x10;
    // 测量类型
    internal const MetricType METRIC_TYPE_GAUGE = (MetricType)0x20;
    /* internal const byte METRIC_TYPE_SUMMARY = 0x30; // 未使用 */
    // 直方图类型
    internal const MetricType METRIC_TYPE_HISTOGRAM = (MetricType)0x40;
    // 非单调求和类型
    internal const MetricType METRIC_TYPE_NON_MONOTONIC_SUM = (MetricType)0x80;

    // 掩码，用于提取MetricType的点类型部分
    internal const MetricType POINT_KIND_MASK = (MetricType)0x0f;

    // 有符号1字节整数
    internal const MetricType POINT_KIND_I1 = (MetricType)0x04;
    // 无符号1字节整数
    internal const MetricType POINT_KIND_U1 = (MetricType)0x05;
    // 有符号2字节整数
    internal const MetricType POINT_KIND_I2 = (MetricType)0x06;
    // 无符号2字节整数
    internal const MetricType POINT_KIND_U2 = (MetricType)0x07;
    // 有符号4字节整数
    internal const MetricType POINT_KIND_I4 = (MetricType)0x08;
    // 无符号4字节整数
    internal const MetricType POINT_KIND_U4 = (MetricType)0x09;
    // 有符号8字节整数
    internal const MetricType POINT_KIND_I8 = (MetricType)0x0a;
    // 无符号8字节整数
    internal const MetricType POINT_KIND_U8 = (MetricType)0x0b;
    // 4字节浮点数
    internal const MetricType POINT_KIND_R4 = (MetricType)0x0c;
    // 8字节浮点数
    internal const MetricType POINT_KIND_R8 = (MetricType)0x0d;

#pragma warning restore SA1310 // field should not contain an underscore

    /// <summary>
    /// 确定提供的<see cref="MetricType"/>是否为求和定义。
    /// </summary>
    /// <param name="self"><see cref="MetricType"/>.</param>
    /// <returns>如果提供的<see cref="MetricType"/>是求和定义，则返回<see langword="true"/>。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSum(this MetricType self)
    {
        var type = self & METRIC_TYPE_MASK;
        return type == METRIC_TYPE_MONOTONIC_SUM || type == METRIC_TYPE_NON_MONOTONIC_SUM;
    }

    /// <summary>
    /// 确定提供的<see cref="MetricType"/>是否为测量定义。
    /// </summary>
    /// <param name="self"><see cref="MetricType"/>.</param>
    /// <returns>如果提供的<see cref="MetricType"/>是测量定义，则返回<see langword="true"/>。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsGauge(this MetricType self)
    {
        return (self & METRIC_TYPE_MASK) == METRIC_TYPE_GAUGE;
    }

    /// <summary>
    /// 确定提供的<see cref="MetricType"/>是否为直方图定义。
    /// </summary>
    /// <param name="self"><see cref="MetricType"/>.</param>
    /// <returns>如果提供的<see cref="MetricType"/>是直方图定义，则返回<see langword="true"/>。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHistogram(this MetricType self)
    {
        return self.HasFlag(METRIC_TYPE_HISTOGRAM);
    }

    /// <summary>
    /// 确定提供的<see cref="MetricType"/>是否为双精度浮点数定义。
    /// </summary>
    /// <param name="self"><see cref="MetricType"/>.</param>
    /// <returns>如果提供的<see cref="MetricType"/>是双精度浮点数定义，则返回<see langword="true"/>。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDouble(this MetricType self)
    {
        return (self & POINT_KIND_MASK) == POINT_KIND_R8;
    }

    /// <summary>
    /// 确定提供的<see cref="MetricType"/>是否为长整型定义。
    /// </summary>
    /// <param name="self"><see cref="MetricType"/>.</param>
    /// <returns>如果提供的<see cref="MetricType"/>是长整型定义，则返回<see langword="true"/>。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLong(this MetricType self)
    {
        return (self & POINT_KIND_MASK) == POINT_KIND_I8;
    }
}
