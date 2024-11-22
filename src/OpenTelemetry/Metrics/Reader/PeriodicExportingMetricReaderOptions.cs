// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 包含周期性指标读取器选项。
/// </summary>
/// <remarks>
/// 注意：OTEL_METRIC_EXPORT_INTERVAL 和 OTEL_METRIC_EXPORT_TIMEOUT 环境变量在对象构造期间解析。
/// </remarks>
public class PeriodicExportingMetricReaderOptions
{
    // OTEL_METRIC_EXPORT_INTERVAL 环境变量的键
    internal const string OTelMetricExportIntervalEnvVarKey = "OTEL_METRIC_EXPORT_INTERVAL";
    // OTEL_METRIC_EXPORT_TIMEOUT 环境变量的键
    internal const string OTelMetricExportTimeoutEnvVarKey = "OTEL_METRIC_EXPORT_TIMEOUT";

    /// <summary>
    /// 初始化 <see cref="PeriodicExportingMetricReaderOptions"/> 类的新实例。
    /// </summary>
    public PeriodicExportingMetricReaderOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal PeriodicExportingMetricReaderOptions(IConfiguration configuration)
    {
        // 尝试从环境变量中获取导出间隔时间
        if (configuration.TryGetIntValue(OpenTelemetrySdkEventSource.Log, OTelMetricExportIntervalEnvVarKey, out var interval))
        {
            this.ExportIntervalMilliseconds = interval;
        }

        // 尝试从环境变量中获取导出超时时间
        if (configuration.TryGetIntValue(OpenTelemetrySdkEventSource.Log, OTelMetricExportTimeoutEnvVarKey, out var timeout))
        {
            this.ExportTimeoutMilliseconds = timeout;
        }
    }

    /// <summary>
    /// 获取或设置以毫秒为单位的指标导出间隔。
    /// 如果未设置，默认值取决于与指标读取器关联的指标导出器的类型。
    /// </summary>
    public int? ExportIntervalMilliseconds { get; set; }

    /// <summary>
    /// 获取或设置以毫秒为单位的指标导出超时时间。
    /// 如果未设置，默认值取决于与指标读取器关联的指标导出器的类型。
    /// </summary>
    public int? ExportTimeoutMilliseconds { get; set; }
}
