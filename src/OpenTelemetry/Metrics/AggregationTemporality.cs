// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 枚举，用于定义<see cref="Metric"/>的聚合时间性。
/// </summary>
public enum AggregationTemporality : byte
{
    /// <summary>
    /// 累积的。
    /// </summary>
    Cumulative = 0b1, // 累积的聚合时间性

    /// <summary>
    /// 增量的。
    /// </summary>
    Delta = 0b10, // 增量的聚合时间性
}
