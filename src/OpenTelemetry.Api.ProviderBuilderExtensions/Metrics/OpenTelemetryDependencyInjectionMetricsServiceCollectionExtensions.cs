// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 扩展方法，用于在 <see cref="IServiceCollection" /> 中设置 OpenTelemetry Metrics 服务。
/// </summary>
public static class OpenTelemetryDependencyInjectionMetricsServiceCollectionExtensions
{
    /// <summary>
    /// 注册一个用于配置 OpenTelemetry <see cref="MeterProviderBuilder"/> 的操作。
    /// </summary>
    /// <remarks>
    /// 注意事项:
    /// <list type="bullet">
    /// <item>这可以安全地被多次调用，并且由库作者调用。每个注册的配置操作将按顺序应用。</item>
    /// <item>使用此方法不会自动创建 <see cref="MeterProvider"/>。要开始收集指标，请使用 <c>IServiceCollection.AddOpenTelemetry</c> 扩展，在 <c>OpenTelemetry.Extensions.Hosting</c> 包中。</item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection" />。</param>
    /// <param name="configure">回调操作，用于配置 <see cref="MeterProviderBuilder"/>。</param>
    /// <returns>返回 <see cref="IServiceCollection"/> 以便链式调用。</returns>
    public static IServiceCollection ConfigureOpenTelemetryMeterProvider(
        this IServiceCollection services,
        Action<MeterProviderBuilder> configure)
    {
        // 检查 services 是否为 null
        Guard.ThrowIfNull(services);
        // 检查 configure 是否为 null
        Guard.ThrowIfNull(configure);

        // 配置 MeterProviderBuilder
        configure(new MeterProviderServiceCollectionBuilder(services));

        return services;
    }

    /// <summary>
    /// 注册一个用于在 <see cref="IServiceProvider"/> 可用时配置 OpenTelemetry <see cref="MeterProviderBuilder"/> 的操作。
    /// </summary>
    /// <remarks>
    /// 注意事项:
    /// <list type="bullet">
    /// <item>这可以安全地被多次调用，并且由库作者调用。每个注册的配置操作将按顺序应用。</item>
    /// <item>使用此方法不会自动创建 <see cref="MeterProvider"/>。要开始收集指标，请使用 <c>IServiceCollection.AddOpenTelemetry</c> 扩展，在 <c>OpenTelemetry.Extensions.Hosting</c> 包中。</item>
    /// <item>提供的配置委托在 <see cref="IServiceProvider"/> 可用时调用。一旦创建了 <see cref="IServiceProvider"/>，就不能向 <see cref="MeterProviderBuilder"/> 添加服务。许多辅助扩展注册服务，如果在配置委托内调用，可能会抛出异常。如果不需要访问 <see cref="IServiceProvider"/>，请调用 <see cref="ConfigureOpenTelemetryMeterProvider(IServiceCollection, Action{MeterProviderBuilder})"/>，这可以安全地与辅助扩展一起使用。</item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection" />。</param>
    /// <param name="configure">回调操作，用于配置 <see cref="MeterProviderBuilder"/>。</param>
    /// <returns>返回 <see cref="IServiceCollection"/> 以便链式调用。</returns>
    public static IServiceCollection ConfigureOpenTelemetryMeterProvider(
        this IServiceCollection services,
        Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        // 检查 services 是否为 null
        Guard.ThrowIfNull(services);
        // 检查 configure 是否为 null
        Guard.ThrowIfNull(configure);

        // 注册 IConfigureMeterProviderBuilder 实现
        services.AddSingleton<IConfigureMeterProviderBuilder>(
            new ConfigureMeterProviderBuilderCallbackWrapper(configure));

        return services;
    }

    /// <summary>
    /// 内部类，用于包装配置回调。
    /// </summary>
    private sealed class ConfigureMeterProviderBuilderCallbackWrapper : IConfigureMeterProviderBuilder
    {
        // 配置回调操作
        private readonly Action<IServiceProvider, MeterProviderBuilder> configure;

        /// <summary>
        /// 初始化 <see cref="ConfigureMeterProviderBuilderCallbackWrapper"/> 类的新实例。
        /// </summary>
        /// <param name="configure">配置回调操作。</param>
        public ConfigureMeterProviderBuilderCallbackWrapper(Action<IServiceProvider, MeterProviderBuilder> configure)
        {
            // 检查 configure 是否为 null
            Guard.ThrowIfNull(configure);

            this.configure = configure;
        }

        /// <summary>
        /// 配置 MeterProviderBuilder。
        /// </summary>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/>。</param>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>。</param>
        public void ConfigureBuilder(IServiceProvider serviceProvider, MeterProviderBuilder meterProviderBuilder)
        {
            this.configure(serviceProvider, meterProviderBuilder);
        }
    }
}
