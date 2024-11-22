// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 配置 <see cref="BaseExportingMetricReader"/> 或 <see cref="PeriodicExportingMetricReader"/> 的选项。
/// </summary>
public class MetricReaderOptions
{
    // 定义周期性导出度量读取器选项的变量
    private PeriodicExportingMetricReaderOptions periodicExportingMetricReaderOptions;

    /// <summary>
    /// 初始化 <see cref="MetricReaderOptions"/> 类的新实例。
    /// </summary>
    public MetricReaderOptions()
        : this(new())
    {
    }

    internal MetricReaderOptions(
        PeriodicExportingMetricReaderOptions defaultPeriodicExportingMetricReaderOptions)
    {
        // 确保 defaultPeriodicExportingMetricReaderOptions 不为空
        Debug.Assert(defaultPeriodicExportingMetricReaderOptions != null, "defaultPeriodicExportingMetricReaderOptions was null");

        // 如果 defaultPeriodicExportingMetricReaderOptions 为空，则创建一个新的实例
        this.periodicExportingMetricReaderOptions = defaultPeriodicExportingMetricReaderOptions ?? new();
    }

    /// <summary>
    /// 获取或设置 <see cref="MetricReaderTemporalityPreference" />。
    /// </summary>
    public MetricReaderTemporalityPreference TemporalityPreference { get; set; } = MetricReaderTemporalityPreference.Cumulative;

    /// <summary>
    /// 获取或设置 <see cref="Metrics.PeriodicExportingMetricReaderOptions" />。
    /// </summary>
    public PeriodicExportingMetricReaderOptions PeriodicExportingMetricReaderOptions
    {
        get => this.periodicExportingMetricReaderOptions;
        set
        {
            // 确保设置的值不为空
            Guard.ThrowIfNull(value);
            this.periodicExportingMetricReaderOptions = value;
        }
    }
}
