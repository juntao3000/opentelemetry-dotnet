// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 存储直方图 MetricStream 的配置。
/// </summary>
public class HistogramConfiguration : MetricStreamConfiguration
{
    /// <summary>
    /// 获取或设置一个值，该值指示是否应收集最小值和最大值。
    /// </summary>
    public bool RecordMinMax { get; set; } = true; // 是否记录最小值和最大值，默认为 true
}
