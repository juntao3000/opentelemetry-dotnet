// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

/// <summary>
/// 包含配置 OpenTelemetry SDK 和访问日志、指标和跟踪提供程序的方法。
/// </summary>
public sealed class OpenTelemetrySdk : IDisposable
{
    // 服务提供者
    private readonly ServiceProvider serviceProvider;

    // 构造函数，配置 OpenTelemetry SDK
    private OpenTelemetrySdk(
        Action<IOpenTelemetryBuilder> configure)
    {
        Debug.Assert(configure != null, "configure was null");

        var services = new ServiceCollection();

        var builder = new OpenTelemetrySdkBuilder(services);

        configure!(builder);

        this.serviceProvider = services.BuildServiceProvider();

        // 获取 LoggerProvider，如果没有则使用 NoopLoggerProvider
        this.LoggerProvider = (LoggerProvider?)this.serviceProvider.GetService(typeof(LoggerProvider))
            ?? new NoopLoggerProvider();
        // 获取 MeterProvider，如果没有则使用 NoopMeterProvider
        this.MeterProvider = (MeterProvider?)this.serviceProvider.GetService(typeof(MeterProvider))
            ?? new NoopMeterProvider();
        // 获取 TracerProvider，如果没有则使用 NoopTracerProvider
        this.TracerProvider = (TracerProvider?)this.serviceProvider.GetService(typeof(TracerProvider))
            ?? new NoopTracerProvider();
    }

    /// <summary>
    /// 获取 <see cref="Logs.LoggerProvider"/>。
    /// </summary>
    /// <remarks>
    /// 注意：默认的 <see cref="LoggerProvider"/> 将是一个 no-op 实例。
    /// 调用 <see
    /// cref="OpenTelemetryBuilderSdkExtensions.WithLogging(IOpenTelemetryBuilder)"/> 以启用日志记录。
    /// </remarks>
    public LoggerProvider LoggerProvider { get; }

    /// <summary>
    /// 获取 <see cref="Metrics.MeterProvider"/>。
    /// </summary>
    /// <remarks>
    /// 注意：默认的 <see cref="MeterProvider"/> 将是一个 no-op 实例。
    /// 调用 <see
    /// cref="OpenTelemetryBuilderSdkExtensions.WithMetrics(IOpenTelemetryBuilder)"/>
    /// 以启用指标。
    /// </remarks>
    public MeterProvider MeterProvider { get; }

    /// <summary>
    /// 获取 <see cref="Trace.TracerProvider"/>。
    /// </summary>
    /// <remarks>
    /// 注意：默认的 <see cref="TracerProvider"/> 将是一个 no-op 实例。
    /// 调用 <see
    /// cref="OpenTelemetryBuilderSdkExtensions.WithTracing(IOpenTelemetryBuilder)"/>
    /// 以启用跟踪。
    /// </remarks>
    public TracerProvider TracerProvider { get; }

    /// <summary>
    /// 获取包含 SDK 服务的 <see cref="IServiceProvider"/>。
    /// </summary>
    internal IServiceProvider Services => this.serviceProvider;

    /// <summary>
    /// 创建一个 <see cref="OpenTelemetrySdk"/> 实例。
    /// </summary>
    /// <param name="configure"><see cref="IOpenTelemetryBuilder"/> 配置委托。</param>
    /// <returns>创建的 <see cref="OpenTelemetrySdk"/>。</returns>
    public static OpenTelemetrySdk Create(
        Action<IOpenTelemetryBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        return new(configure);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.serviceProvider.Dispose();
    }

    // NoopLoggerProvider 类，继承自 LoggerProvider
    internal sealed class NoopLoggerProvider : LoggerProvider
    {
    }

    // NoopMeterProvider 类，继承自 MeterProvider
    internal sealed class NoopMeterProvider : MeterProvider
    {
    }

    // NoopTracerProvider 类，继承自 TracerProvider
    internal sealed class NoopTracerProvider : TracerProvider
    {
    }

    // OpenTelemetrySdkBuilder 类，实现 IOpenTelemetryBuilder 接口
    private sealed class OpenTelemetrySdkBuilder : IOpenTelemetryBuilder
    {
        public OpenTelemetrySdkBuilder(IServiceCollection services)
        {
            Debug.Assert(services != null, "services was null");

            services!.AddOpenTelemetrySharedProviderBuilderServices();

            this.Services = services!;
        }

        // 获取服务集合
        public IServiceCollection Services { get; }
    }
}
