// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

// OpenTelemetryBuilderSdkExtensions 类包含扩展 IOpenTelemetryBuilder 接口的方法。
public static class OpenTelemetryBuilderSdkExtensions
{
    // ConfigureResource 方法注册一个操作来配置跟踪、指标和日志使用的 ResourceBuilder。
    public static IOpenTelemetryBuilder ConfigureResource(
        this IOpenTelemetryBuilder builder,
        Action<ResourceBuilder> configure)
    {
        // 检查 builder 是否为 null
        Guard.ThrowIfNull(builder);
        // 检查 configure 是否为 null
        Guard.ThrowIfNull(configure);

        // 配置 OpenTelemetryMeterProvider
        builder.Services.ConfigureOpenTelemetryMeterProvider(
            builder => builder.ConfigureResource(configure));

        // 配置 OpenTelemetryTracerProvider
        builder.Services.ConfigureOpenTelemetryTracerProvider(
            builder => builder.ConfigureResource(configure));

        // 配置 OpenTelemetryLoggerProvider
        builder.Services.ConfigureOpenTelemetryLoggerProvider(
            builder => builder.ConfigureResource(configure));

        // 返回 builder 以便链式调用
        return builder;
    }

    // WithMetrics 方法将指标服务添加到构建器中。
    public static IOpenTelemetryBuilder WithMetrics(
        this IOpenTelemetryBuilder builder)
        => WithMetrics(builder, b => { });

    // WithMetrics 方法将指标服务添加到构建器中，并接受一个配置回调。
    public static IOpenTelemetryBuilder WithMetrics(
        this IOpenTelemetryBuilder builder,
        Action<MeterProviderBuilder> configure)
    {
        // 注册 MetricsListener
        OpenTelemetryMetricsBuilderExtensions.RegisterMetricsListener(
            builder.Services,
            configure);

        // 返回 builder 以便链式调用
        return builder;
    }

    // WithTracing 方法将跟踪服务添加到构建器中。
    public static IOpenTelemetryBuilder WithTracing(this IOpenTelemetryBuilder builder)
        => WithTracing(builder, b => { });

    // WithTracing 方法将跟踪服务添加到构建器中，并接受一个配置回调。
    public static IOpenTelemetryBuilder WithTracing(
        this IOpenTelemetryBuilder builder,
        Action<TracerProviderBuilder> configure)
    {
        // 检查 configure 是否为 null
        Guard.ThrowIfNull(configure);

        // 创建 TracerProviderBuilderBase 实例
        var tracerProviderBuilder = new TracerProviderBuilderBase(builder.Services);

        // 调用配置回调
        configure(tracerProviderBuilder);

        // 返回 builder 以便链式调用
        return builder;
    }

    // WithLogging 方法将日志服务添加到构建器中。
    public static IOpenTelemetryBuilder WithLogging(this IOpenTelemetryBuilder builder)
        => WithLogging(builder, configureBuilder: null, configureOptions: null);

    // WithLogging 方法将日志服务添加到构建器中，并接受一个配置回调。
    public static IOpenTelemetryBuilder WithLogging(
        this IOpenTelemetryBuilder builder,
        Action<LoggerProviderBuilder> configure)
    {
        // 检查 configure 是否为 null
        Guard.ThrowIfNull(configure);

        // 调用 WithLogging 方法
        return WithLogging(builder, configureBuilder: configure, configureOptions: null);
    }

    // WithLogging 方法将日志服务添加到构建器中，并接受两个可选的配置回调。
    public static IOpenTelemetryBuilder WithLogging(
        this IOpenTelemetryBuilder builder,
        Action<LoggerProviderBuilder>? configureBuilder,
        Action<OpenTelemetryLoggerOptions>? configureOptions)
    {
        // 添加 OpenTelemetry 日志提供程序
        builder.Services.AddLogging(
            logging => logging.UseOpenTelemetry(configureBuilder, configureOptions));

        // 返回 builder 以便链式调用
        return builder;
    }
}
