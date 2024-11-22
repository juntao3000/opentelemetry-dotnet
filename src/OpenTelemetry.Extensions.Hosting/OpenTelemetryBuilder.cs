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

/// <summary>
/// 包含在 <see cref="IServiceCollection"/> 中配置 OpenTelemetry SDK 的方法。
/// </summary>
public sealed class OpenTelemetryBuilder : IOpenTelemetryBuilder
{
    // 构造函数，初始化 OpenTelemetryBuilder 实例
    internal OpenTelemetryBuilder(IServiceCollection services)
    {
        // 检查 services 是否为 null
        Guard.ThrowIfNull(services);

        // 添加 OpenTelemetry 共享提供程序构建器服务
        services.AddOpenTelemetrySharedProviderBuilderServices();

        // 设置服务集合
        this.Services = services;
    }

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <summary>
    /// 注册一个操作来配置跟踪、指标和日志使用的 <see cref="ResourceBuilder"/>。
    /// </summary>
    /// <remarks>
    /// 注意：这可以安全地被多次调用，并且可以由库作者调用。每个注册的配置操作将按顺序应用。
    /// </remarks>
    /// <param name="configure"><see cref="ResourceBuilder"/> 配置操作。</param>
    /// <returns>返回 <see cref="OpenTelemetryBuilder"/> 以便链式调用。</returns>
    public OpenTelemetryBuilder ConfigureResource(
        Action<ResourceBuilder> configure)
    {
        // 调用扩展方法配置资源
        OpenTelemetryBuilderSdkExtensions.ConfigureResource(this, configure);
        return this;
    }

    /// <summary>
    /// 向构建器中添加指标服务。
    /// </summary>
    /// <remarks>
    /// 注意：
    /// <list type="bullet">
    /// <item>这可以安全地被多次调用，并且可以由库作者调用。对于给定的 <see cref="IServiceCollection"/>，只会创建一个 <see cref="MeterProvider"/>。</item>
    /// <item>此方法会自动在 <see cref="IServiceCollection"/> 中注册一个名为 'OpenTelemetry' 的 <see cref="IMetricsListener"/>。</item>
    /// </list>
    /// </remarks>
    /// <returns>返回 <see cref="OpenTelemetryBuilder"/> 以便链式调用。</returns>
    public OpenTelemetryBuilder WithMetrics()
        => this.WithMetrics(b => { });

    /// <summary>
    /// 向构建器中添加指标服务。
    /// </summary>
    /// <remarks><inheritdoc cref="WithMetrics()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="MeterProviderBuilder"/> 配置回调。</param>
    /// <returns>返回 <see cref="OpenTelemetryBuilder"/> 以便链式调用。</returns>
    public OpenTelemetryBuilder WithMetrics(Action<MeterProviderBuilder> configure)
    {
        // 调用扩展方法配置指标
        OpenTelemetryBuilderSdkExtensions.WithMetrics(this, configure);
        return this;
    }

    /// <summary>
    /// 向构建器中添加跟踪服务。
    /// </summary>
    /// <remarks>
    /// 注意：这可以安全地被多次调用，并且可以由库作者调用。对于给定的 <see cref="IServiceCollection"/>，只会创建一个 <see cref="TracerProvider"/>。
    /// </remarks>
    /// <returns>返回 <see cref="OpenTelemetryBuilder"/> 以便链式调用。</returns>
    public OpenTelemetryBuilder WithTracing()
        => this.WithTracing(b => { });

    /// <summary>
    /// 向构建器中添加跟踪服务。
    /// </summary>
    /// <remarks><inheritdoc cref="WithTracing()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="TracerProviderBuilder"/> 配置回调。</param>
    /// <returns>返回 <see cref="OpenTelemetryBuilder"/> 以便链式调用。</returns>
    public OpenTelemetryBuilder WithTracing(Action<TracerProviderBuilder> configure)
    {
        // 调用扩展方法配置跟踪
        OpenTelemetryBuilderSdkExtensions.WithTracing(this, configure);
        return this;
    }

    /// <summary>
    /// 向构建器中添加日志服务。
    /// </summary>
    /// <remarks>
    /// 注意：
    /// <list type="bullet">
    /// <item>这可以安全地被多次调用，并且可以由库作者调用。对于给定的 <see cref="IServiceCollection"/>，只会创建一个 <see cref="LoggerProvider"/>。</item>
    /// <item>此方法会自动在 <see cref="IServiceCollection"/> 中注册一个名为 'OpenTelemetry' 的 <see cref="ILoggerProvider"/>。</item>
    /// </list>
    /// </remarks>
    /// <returns>返回 <see cref="OpenTelemetryBuilder"/> 以便链式调用。</returns>
    public OpenTelemetryBuilder WithLogging()
        => this.WithLogging(configureBuilder: null, configureOptions: null);

    /// <summary>
    /// 向构建器中添加日志服务。
    /// </summary>
    /// <remarks><inheritdoc cref="WithLogging()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="LoggerProviderBuilder"/> 配置回调。</param>
    /// <returns>返回 <see cref="OpenTelemetryBuilder"/> 以便链式调用。</returns>
    public OpenTelemetryBuilder WithLogging(Action<LoggerProviderBuilder> configure)
    {
        // 检查 configure 是否为 null
        Guard.ThrowIfNull(configure);

        return this.WithLogging(configureBuilder: configure, configureOptions: null);
    }

    /// <summary>
    /// 向构建器中添加日志服务。
    /// </summary>
    /// <remarks><inheritdoc cref="WithLogging()" path="/remarks"/></remarks>
    /// <param name="configureBuilder">可选的 <see cref="LoggerProviderBuilder"/> 配置回调。</param>
    /// <param name="configureOptions">可选的 <see cref="OpenTelemetryLoggerOptions"/> 配置回调。<see cref="OpenTelemetryLoggerOptions"/> 由此方法自动注册的名为 'OpenTelemetry' 的 <see cref="ILoggerProvider"/> 使用。</param>
    /// <returns>返回 <see cref="OpenTelemetryBuilder"/> 以便链式调用。</returns>
    public OpenTelemetryBuilder WithLogging(
        Action<LoggerProviderBuilder>? configureBuilder,
        Action<OpenTelemetryLoggerOptions>? configureOptions)
    {
        // 调用扩展方法配置日志
        OpenTelemetryBuilderSdkExtensions.WithLogging(this, configureBuilder, configureOptions);

        return this;
    }
}
