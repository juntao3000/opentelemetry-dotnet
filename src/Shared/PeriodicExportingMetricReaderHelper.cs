// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// PeriodicExportingMetricReaderHelper 类，提供创建周期性导出度量读取器的帮助方法。
/// </summary>
internal static class PeriodicExportingMetricReaderHelper
{
    // 默认导出间隔（毫秒）
    internal const int DefaultExportIntervalMilliseconds = 60000;
    // 默认导出超时时间（毫秒）
    internal const int DefaultExportTimeoutMilliseconds = 30000;

    /// <summary>
    /// 创建一个周期性导出度量读取器。
    /// </summary>
    /// <param name="exporter">用于导出度量的导出器实例。</param>
    /// <param name="options">度量读取器选项。</param>
    /// <param name="defaultExportIntervalMilliseconds">默认导出间隔（毫秒）。</param>
    /// <param name="defaultExportTimeoutMilliseconds">默认导出超时时间（毫秒）。</param>
    /// <returns>返回一个 PeriodicExportingMetricReader 实例。</returns>
    internal static PeriodicExportingMetricReader CreatePeriodicExportingMetricReader(
        BaseExporter<Metric> exporter,
        MetricReaderOptions options,
        int defaultExportIntervalMilliseconds = DefaultExportIntervalMilliseconds,
        int defaultExportTimeoutMilliseconds = DefaultExportTimeoutMilliseconds)
    {
        // 获取导出间隔，如果未设置则使用默认值
        var exportInterval = options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds ?? defaultExportIntervalMilliseconds;

        // 获取导出超时时间，如果未设置则使用默认值
        var exportTimeout = options.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds ?? defaultExportTimeoutMilliseconds;

        // 创建一个周期性导出度量读取器实例，并设置 TemporalityPreference
        var metricReader = new PeriodicExportingMetricReader(exporter, exportInterval, exportTimeout)
        {
            TemporalityPreference = options.TemporalityPreference,
        };

        // 返回创建的周期性导出度量读取器实例
        return metricReader;
    }
}
