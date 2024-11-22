// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

// 提供扩展方法以向IServiceCollection添加OpenTelemetry服务
internal static class ProviderBuilderServiceCollectionExtensions
{
    // 向IServiceCollection添加OpenTelemetry Logger Provider Builder服务
    public static IServiceCollection AddOpenTelemetryLoggerProviderBuilderServices(this IServiceCollection services)
    {
        Debug.Assert(services != null, "services was null"); // 确保services不为空

        services!.TryAddSingleton<LoggerProviderBuilderSdk>(); // 注册LoggerProviderBuilderSdk为单例
        services!.RegisterOptionsFactory(configuration => new BatchExportLogRecordProcessorOptions(configuration)); // 注册BatchExportLogRecordProcessorOptions的工厂方法
        services!.RegisterOptionsFactory(
            (sp, configuration, name) => new LogRecordExportProcessorOptions(
                sp.GetRequiredService<IOptionsMonitor<BatchExportLogRecordProcessorOptions>>().Get(name))); // 注册LogRecordExportProcessorOptions的工厂方法

        return services!; // 返回修改后的IServiceCollection
    }

    // 向IServiceCollection添加OpenTelemetry Meter Provider Builder服务
    public static IServiceCollection AddOpenTelemetryMeterProviderBuilderServices(this IServiceCollection services)
    {
        Debug.Assert(services != null, "services was null"); // 确保services不为空

        services!.TryAddSingleton<MeterProviderBuilderSdk>(); // 注册MeterProviderBuilderSdk为单例
        services!.RegisterOptionsFactory(configuration => new PeriodicExportingMetricReaderOptions(configuration)); // 注册PeriodicExportingMetricReaderOptions的工厂方法
        services!.RegisterOptionsFactory(
            (sp, configuration, name) => new MetricReaderOptions(
                sp.GetRequiredService<IOptionsMonitor<PeriodicExportingMetricReaderOptions>>().Get(name))); // 注册MetricReaderOptions的工厂方法

        return services!; // 返回修改后的IServiceCollection
    }

    // 向IServiceCollection添加OpenTelemetry Tracer Provider Builder服务
    public static IServiceCollection AddOpenTelemetryTracerProviderBuilderServices(this IServiceCollection services)
    {
        Debug.Assert(services != null, "services was null"); // 确保services不为空

        services!.TryAddSingleton<TracerProviderBuilderSdk>(); // 注册TracerProviderBuilderSdk为单例
        services!.RegisterOptionsFactory(configuration => new BatchExportActivityProcessorOptions(configuration)); // 注册BatchExportActivityProcessorOptions的工厂方法
        services!.RegisterOptionsFactory(
            (sp, configuration, name) => new ActivityExportProcessorOptions(
                sp.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>().Get(name))); // 注册ActivityExportProcessorOptions的工厂方法

        return services!; // 返回修改后的IServiceCollection
    }

    // 向IServiceCollection添加OpenTelemetry共享Provider Builder服务
    public static IServiceCollection AddOpenTelemetrySharedProviderBuilderServices(this IServiceCollection services)
    {
        Debug.Assert(services != null, "services was null"); // 确保services不为空

        // 访问Sdk类只是为了触发其静态构造函数，
        // 该构造函数设置默认的Propagators和默认的Activity Id格式
        _ = Sdk.SuppressInstrumentation;

        services!.AddOptions(); // 添加Options服务

        // 注意：使用主机生成器时，IConfiguration会自动注册，此注册将无效。
        // 这仅在使用Sdk.Create*样式或手动创建ServiceCollection时运行。
        // 此注册的目的是在这些情况下使IConfiguration可用。
        services!.TryAddSingleton<IConfiguration>(
            sp => new ConfigurationBuilder().AddEnvironmentVariables().Build()); // 注册IConfiguration为单例

        return services!; // 返回修改后的IServiceCollection
    }
}
