// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter;

// OtlpServiceCollectionExtensions类：提供扩展方法以注册OTLP导出器服务
internal static class OtlpServiceCollectionExtensions
{
    // AddOtlpExporterLoggingServices方法：注册OTLP日志导出器服务
    public static void AddOtlpExporterLoggingServices(this IServiceCollection services)
    {
        Debug.Assert(services != null, "services was null"); // 确保services不为空

        AddOtlpExporterSharedServices(services!, registerSdkLimitOptions: true); // 注册共享服务并启用SDK限制选项
    }

    // AddOtlpExporterMetricsServices方法：注册OTLP指标导出器服务
    public static void AddOtlpExporterMetricsServices(this IServiceCollection services, string name)
    {
        Debug.Assert(services != null, "services was null"); // 确保services不为空
        Debug.Assert(name != null, "name was null"); // 确保name不为空

        AddOtlpExporterSharedServices(services!, registerSdkLimitOptions: false); // 注册共享服务但不启用SDK限制选项

        // 配置MetricReaderOptions
        services!.AddOptions<MetricReaderOptions>(name).Configure<IConfiguration>(
            (readerOptions, config) =>
            {
                // 从配置中获取OTLP Temporality Preference
                var otlpTemporalityPreference = config[OtlpSpecConfigDefinitions.MetricsTemporalityPreferenceEnvVarName];
                if (!string.IsNullOrWhiteSpace(otlpTemporalityPreference)
                    && Enum.TryParse<MetricReaderTemporalityPreference>(otlpTemporalityPreference, ignoreCase: true, out var enumValue))
                {
                    readerOptions.TemporalityPreference = enumValue; // 设置Temporality Preference
                }
            });
    }

    // AddOtlpExporterTracingServices方法：注册OTLP跟踪导出器服务
    public static void AddOtlpExporterTracingServices(this IServiceCollection services)
    {
        Debug.Assert(services != null, "services was null"); // 确保services不为空

        AddOtlpExporterSharedServices(services!, registerSdkLimitOptions: true); // 注册共享服务并启用SDK限制选项
    }

    // AddOtlpExporterSharedServices方法：注册OTLP导出器共享服务
    private static void AddOtlpExporterSharedServices(
        IServiceCollection services,
        bool registerSdkLimitOptions)
    {
        // 注册OtlpExporterOptions工厂
        services.RegisterOptionsFactory(OtlpExporterOptions.CreateOtlpExporterOptions);
        // 注册ExperimentalOptions工厂
        services.RegisterOptionsFactory(configuration => new ExperimentalOptions(configuration));

        if (registerSdkLimitOptions)
        {
            // 注册SdkLimitOptions工厂
            services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
        }
    }
}
