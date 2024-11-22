// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace Microsoft.Extensions.Diagnostics.Metrics;

/// <summary>
/// 包含用于向 <see cref="IMetricsBuilder"/> 实例注册 OpenTelemetry 指标的扩展方法。
/// </summary>
internal static class OpenTelemetryMetricsBuilderExtensions
{
    /// <summary>
    /// 向 <see cref="IMetricsBuilder"/> 添加一个名为 'OpenTelemetry' 的 OpenTelemetry <see cref="IMetricsListener"/>。
    /// </summary>
    /// <remarks>
    /// 注意：这可以安全地被多次调用，并且可以由库作者调用。
    /// 对于给定的 <see cref="IServiceCollection"/>，只会创建一个 <see cref="MeterProvider"/>。
    /// </remarks>
    /// <param name="metricsBuilder"><see cref="IMetricsBuilder"/>。</param>
    /// <returns>返回提供的 <see cref="IMetricsBuilder"/> 以便链式调用。</returns>
    public static IMetricsBuilder UseOpenTelemetry(
        this IMetricsBuilder metricsBuilder)
        => UseOpenTelemetry(metricsBuilder, b => { });

    /// <summary>
    /// 向 <see cref="IMetricsBuilder"/> 添加一个名为 'OpenTelemetry' 的 OpenTelemetry <see cref="IMetricsListener"/>。
    /// </summary>
    /// <remarks><inheritdoc cref="UseOpenTelemetry(IMetricsBuilder)" path="/remarks"/></remarks>
    /// <param name="metricsBuilder"><see cref="IMetricsBuilder"/>。</param>
    /// <param name="configure"><see cref="MeterProviderBuilder"/> 配置回调。</param>
    /// <returns>返回提供的 <see cref="IMetricsBuilder"/> 以便链式调用。</returns>
    public static IMetricsBuilder UseOpenTelemetry(
        this IMetricsBuilder metricsBuilder,
        Action<MeterProviderBuilder> configure)
    {
        // 检查 metricsBuilder 是否为 null
        Guard.ThrowIfNull(metricsBuilder);

        // 注册指标监听器
        RegisterMetricsListener(metricsBuilder.Services, configure);

        return metricsBuilder;
    }

    /// <summary>
    /// 注册指标监听器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configure">配置回调。</param>
    internal static void RegisterMetricsListener(
        IServiceCollection services,
        Action<MeterProviderBuilder> configure)
    {
        // 确保 services 不为 null
        Debug.Assert(services != null, "services was null");

        // 检查 configure 是否为 null
        Guard.ThrowIfNull(configure);

        // 创建 MeterProviderBuilderBase 实例
        var builder = new MeterProviderBuilderBase(services!);

        // 尝试添加 OpenTelemetryMetricsListener 单例服务
        services!.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMetricsListener, OpenTelemetryMetricsListener>());

        // 调用配置回调
        configure(builder);
    }
}
