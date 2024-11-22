// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 定义 <see cref="MetricReader" /> 在 <see cref="AggregationTemporality" /> 方面的行为。
/// </summary>
public enum MetricReaderTemporalityPreference
{
    /// <summary>
    /// 所有聚合都使用累积时间性进行。
    /// </summary>
    Cumulative = 1,

    /// <summary>
    /// 所有单调性质的测量都使用增量时间性进行聚合。
    /// 非单调测量的聚合使用累积时间性。
    /// </summary>
    Delta = 2,
}
