// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

// OpenTelemetry 协议 (OTLP) 导出器扩展方法，用于简化 OTLP 导出器的注册。
public static class OtlpMetricExporterExtensions
{
    // 将 OtlpMetricExporter 添加到 MeterProviderBuilder，使用默认选项。
    public static MeterProviderBuilder AddOtlpExporter(this MeterProviderBuilder builder)
        => AddOtlpExporter(builder, name: null, configure: null);

    // 将 OtlpMetricExporter 添加到 MeterProviderBuilder。
    // 参数 configure 是用于配置 OtlpExporterOptions 的回调操作。
    public static MeterProviderBuilder AddOtlpExporter(this MeterProviderBuilder builder, Action<OtlpExporterOptions> configure)
        => AddOtlpExporter(builder, name: null, configure);

    // 将 OtlpMetricExporter 添加到 MeterProviderBuilder。
    // 参数 name 是用于检索选项的可选名称。
    // 参数 configure 是用于配置 OtlpExporterOptions 的可选回调操作。
    public static MeterProviderBuilder AddOtlpExporter(
        this MeterProviderBuilder builder,
        string? name,
        Action<OtlpExporterOptions>? configure)
    {
        // 检查 builder 是否为 null
        Guard.ThrowIfNull(builder);

        // 如果 name 为 null，则使用默认名称
        var finalOptionsName = name ?? Options.DefaultName;

        // 配置服务
        builder.ConfigureServices(services =>
        {
            if (name != null && configure != null)
            {
                // 如果使用命名选项，则将配置委托注册到选项管道中。
                services.Configure(finalOptionsName, configure);
            }

            // 添加 OTLP 导出器度量服务
            services.AddOtlpExporterMetricsServices(finalOptionsName);
        });

        // 添加读取器
        return builder.AddReader(sp =>
        {
            OtlpExporterOptions exporterOptions;

            if (name == null)
            {
                // 如果不使用命名选项，则总是创建一个新实例。
                // 原因是 OtlpExporterOptions 由所有信号共享。
                // 没有名称，所有信号的委托将混合在一起。
                exporterOptions = sp.GetRequiredService<IOptionsFactory<OtlpExporterOptions>>().Create(finalOptionsName);

                // 配置委托在新实例上内联执行。
                configure?.Invoke(exporterOptions);
            }
            else
            {
                // 使用命名选项时，可以正确利用 Options API 创建或重用实例。
                exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(finalOptionsName);
            }

            // 构建 OTLP 导出器度量读取器
            return BuildOtlpExporterMetricReader(
                sp,
                exporterOptions,
                sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(finalOptionsName),
                sp.GetRequiredService<IOptionsMonitor<ExperimentalOptions>>().Get(finalOptionsName));
        });
    }

    // 将 OtlpMetricExporter 添加到 MeterProviderBuilder。
    // 参数 configureExporterAndMetricReader 是用于配置 OtlpExporterOptions 和 MetricReaderOptions 的回调操作。
    public static MeterProviderBuilder AddOtlpExporter(
        this MeterProviderBuilder builder,
        Action<OtlpExporterOptions, MetricReaderOptions> configureExporterAndMetricReader)
        => AddOtlpExporter(builder, name: null, configureExporterAndMetricReader);

    // 将 OtlpMetricExporter 添加到 MeterProviderBuilder。
    // 参数 name 是用于检索选项的可选名称。
    // 参数 configureExporterAndMetricReader 是用于配置 OtlpExporterOptions 和 MetricReaderOptions 的可选回调操作。
    public static MeterProviderBuilder AddOtlpExporter(
        this MeterProviderBuilder builder,
        string? name,
        Action<OtlpExporterOptions, MetricReaderOptions>? configureExporterAndMetricReader)
    {
        // 检查 builder 是否为 null
        Guard.ThrowIfNull(builder);

        // 如果 name 为 null，则使用默认名称
        var finalOptionsName = name ?? Options.DefaultName;

        // 配置服务
        builder.ConfigureServices(services =>
        {
            // 添加 OTLP 导出器度量服务
            services.AddOtlpExporterMetricsServices(finalOptionsName);
        });

        // 添加读取器
        return builder.AddReader(sp =>
        {
            OtlpExporterOptions exporterOptions;
            if (name == null)
            {
                // 如果不使用命名选项，则总是创建一个新实例。
                // 原因是 OtlpExporterOptions 由所有信号共享。
                // 没有名称，所有信号的委托将混合在一起。
                exporterOptions = sp.GetRequiredService<IOptionsFactory<OtlpExporterOptions>>().Create(finalOptionsName);
            }
            else
            {
                // 使用命名选项时，可以正确利用 Options API 创建或重用实例。
                exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(finalOptionsName);
            }

            // 获取 MetricReaderOptions
            var metricReaderOptions = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(finalOptionsName);

            // 配置导出器和度量读取器
            configureExporterAndMetricReader?.Invoke(exporterOptions, metricReaderOptions);

            // 构建 OTLP 导出器度量读取器
            return BuildOtlpExporterMetricReader(
                sp,
                exporterOptions,
                metricReaderOptions,
                sp.GetRequiredService<IOptionsMonitor<ExperimentalOptions>>().Get(finalOptionsName));
        });
    }

    // 构建 OTLP 导出器度量读取器
    internal static MetricReader BuildOtlpExporterMetricReader(
        IServiceProvider serviceProvider,
        OtlpExporterOptions exporterOptions,
        MetricReaderOptions metricReaderOptions,
        ExperimentalOptions experimentalOptions,
        bool skipUseOtlpExporterRegistrationCheck = false,
        Func<BaseExporter<Metric>, BaseExporter<Metric>>? configureExporterInstance = null)
    {
        // 确保 serviceProvider 不为 null
        Debug.Assert(serviceProvider != null, "serviceProvider was null");
        // 确保 exporterOptions 不为 null
        Debug.Assert(exporterOptions != null, "exporterOptions was null");
        // 确保 metricReaderOptions 不为 null
        Debug.Assert(metricReaderOptions != null, "metricReaderOptions was null");
        // 确保 experimentalOptions 不为 null
        Debug.Assert(experimentalOptions != null, "experimentalOptions was null");

        if (!skipUseOtlpExporterRegistrationCheck)
        {
            // 确保没有使用 OTLP 导出器的注册
            serviceProvider!.EnsureNoUseOtlpExporterRegistrations();
        }

        // 尝试启用 IHttpClientFactory 集成
        exporterOptions!.TryEnableIHttpClientFactoryIntegration(serviceProvider!, "OtlpMetricExporter");

        BaseExporter<Metric> metricExporter;

        if (experimentalOptions != null && experimentalOptions.UseCustomProtobufSerializer)
        {
            // 使用自定义 Protobuf 序列化器
            metricExporter = new ProtobufOtlpMetricExporter(exporterOptions!, experimentalOptions!);
        }
        else
        {
            // 使用默认的 OTLP 序列化器
            metricExporter = new OtlpMetricExporter(exporterOptions!, experimentalOptions!);
        }

        if (configureExporterInstance != null)
        {
            // 配置导出器实例
            metricExporter = configureExporterInstance(metricExporter);
        }

        // 创建周期性导出度量读取器
        return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
            metricExporter,
            metricReaderOptions!);
    }
}
