// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 扩展方法以简化 Console 导出器的注册。
/// </summary>
public static class ConsoleExporterMetricsExtensions
{
    // 默认导出间隔（毫秒）
    private const int DefaultExportIntervalMilliseconds = 10000;
    // 默认导出超时时间（毫秒）
    private const int DefaultExportTimeoutMilliseconds = Timeout.Infinite;

    /// <summary>
    /// 使用默认选项将 <see cref="ConsoleMetricExporter"/> 添加到 <see cref="MeterProviderBuilder"/>。
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> 使用的构建器。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 实例以便链式调用。</returns>
    public static MeterProviderBuilder AddConsoleExporter(this MeterProviderBuilder builder)
        => AddConsoleExporter(builder, name: null, configureExporter: null);

    /// <summary>
    /// 将 <see cref="ConsoleMetricExporter"/> 添加到 <see cref="MeterProviderBuilder"/>。
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> 使用的构建器。</param>
    /// <param name="configureExporter">配置 <see cref="ConsoleExporterOptions"/> 的回调操作。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 实例以便链式调用。</returns>
    public static MeterProviderBuilder AddConsoleExporter(this MeterProviderBuilder builder, Action<ConsoleExporterOptions> configureExporter)
        => AddConsoleExporter(builder, name: null, configureExporter);

    /// <summary>
    /// 将 <see cref="ConsoleMetricExporter"/> 添加到 <see cref="MeterProviderBuilder"/>。
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> 使用的构建器。</param>
    /// <param name="name">检索选项时使用的可选名称。</param>
    /// <param name="configureExporter">配置 <see cref="ConsoleExporterOptions"/> 的可选回调操作。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 实例以便链式调用。</returns>
    public static MeterProviderBuilder AddConsoleExporter(
        this MeterProviderBuilder builder,
        string? name,
        Action<ConsoleExporterOptions>? configureExporter)
    {
        // 检查 builder 是否为 null
        Guard.ThrowIfNull(builder);

        // 如果 name 为 null，则使用默认名称
        name ??= Options.DefaultName;

        // 如果提供了 configureExporter，则配置服务
        if (configureExporter != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configureExporter));
        }

        // 添加读取器
        return builder.AddReader(sp =>
        {
            return BuildConsoleExporterMetricReader(
                sp.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(name),
                sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(name));
        });
    }

    /// <summary>
    /// 将 <see cref="ConsoleMetricExporter"/> 添加到 <see cref="MeterProviderBuilder"/>。
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> 使用的构建器。</param>
    /// <param name="configureExporterAndMetricReader">配置 <see cref="ConsoleExporterOptions"/> 和 <see cref="MetricReaderOptions"/> 的回调操作。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 实例以便链式调用。</returns>
    public static MeterProviderBuilder AddConsoleExporter(
        this MeterProviderBuilder builder,
        Action<ConsoleExporterOptions, MetricReaderOptions>? configureExporterAndMetricReader)
        => AddConsoleExporter(builder, name: null, configureExporterAndMetricReader);

    /// <summary>
    /// 将 <see cref="ConsoleMetricExporter"/> 添加到 <see cref="MeterProviderBuilder"/>。
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> 使用的构建器。</param>
    /// <param name="name">检索选项时使用的名称。</param>
    /// <param name="configureExporterAndMetricReader">配置 <see cref="ConsoleExporterOptions"/> 和 <see cref="MetricReaderOptions"/> 的回调操作。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 实例以便链式调用。</returns>
    public static MeterProviderBuilder AddConsoleExporter(
        this MeterProviderBuilder builder,
        string? name,
        Action<ConsoleExporterOptions, MetricReaderOptions>? configureExporterAndMetricReader)
    {
        // 检查 builder 是否为 null
        Guard.ThrowIfNull(builder);

        // 如果 name 为 null，则使用默认名称
        name ??= Options.DefaultName;

        // 添加读取器
        return builder.AddReader(sp =>
        {
            var exporterOptions = sp.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(name);
            var metricReaderOptions = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(name);

            // 配置导出器和度量读取器
            configureExporterAndMetricReader?.Invoke(exporterOptions, metricReaderOptions);

            return BuildConsoleExporterMetricReader(exporterOptions, metricReaderOptions);
        });
    }

    /// <summary>
    /// 构建 Console 导出器度量读取器。
    /// </summary>
    /// <param name="exporterOptions">导出器选项。</param>
    /// <param name="metricReaderOptions">度量读取器选项。</param>
    /// <returns>返回 <see cref="MetricReader"/> 实例。</returns>
    private static MetricReader BuildConsoleExporterMetricReader(
        ConsoleExporterOptions exporterOptions,
        MetricReaderOptions metricReaderOptions)
    {
        // 创建 ConsoleMetricExporter 实例
        var metricExporter = new ConsoleMetricExporter(exporterOptions);

        // 创建并返回周期性导出度量读取器
        return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
            metricExporter,
            metricReaderOptions,
            DefaultExportIntervalMilliseconds,
            DefaultExportTimeoutMilliseconds);
    }
}
