// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 描述一种支持 <see cref="ExportModes.Pull"/> 的 <see cref="BaseExporter{Metric}"/> 类型。
/// </summary>
public interface IPullMetricExporter
{
    /// <summary>
    /// 获取或设置 Collect 委托。
    /// </summary>
    Func<int, bool>? Collect { get; set; } // Collect 委托，用于收集指标数据
}
