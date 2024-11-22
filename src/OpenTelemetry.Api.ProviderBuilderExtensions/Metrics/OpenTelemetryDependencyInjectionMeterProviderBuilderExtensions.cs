// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 包含 <see cref="MeterProviderBuilder"/> 类的扩展方法。
/// </summary>
public static class OpenTelemetryDependencyInjectionMeterProviderBuilderExtensions
{
    /// <summary>
    /// 向提供程序添加仪器。
    /// </summary>
    /// <remarks>
    /// 注意：由 <typeparamref name="T"/> 指定的类型将作为单例服务注册到应用程序服务中。
    /// </remarks>
    /// <typeparam name="T">仪器类型。</typeparam>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder AddInstrumentation<
#if NET
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
    T>(this MeterProviderBuilder meterProviderBuilder)
        where T : class
    {
        // 将仪器类型 T 注册为单例服务
        meterProviderBuilder.ConfigureServices(services => services.TryAddSingleton<T>());

        // 配置构建器以添加仪器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(() => sp.GetRequiredService<T>());
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 向提供程序添加仪器。
    /// </summary>
    /// <typeparam name="T">仪器类型。</typeparam>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>。</param>
    /// <param name="instrumentation">仪器实例。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder AddInstrumentation<T>(this MeterProviderBuilder meterProviderBuilder, T instrumentation)
        where T : class
    {
        // 检查仪器实例是否为 null
        Guard.ThrowIfNull(instrumentation);

        // 配置构建器以添加仪器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(() => instrumentation);
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 向提供程序添加仪器。
    /// </summary>
    /// <typeparam name="T">仪器类型。</typeparam>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>。</param>
    /// <param name="instrumentationFactory">仪器工厂。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder AddInstrumentation<T>(
        this MeterProviderBuilder meterProviderBuilder,
        Func<IServiceProvider, T> instrumentationFactory)
        where T : class
    {
        // 检查仪器工厂是否为 null
        Guard.ThrowIfNull(instrumentationFactory);

        // 配置构建器以添加仪器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(() => instrumentationFactory(sp));
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 向提供程序添加仪器。
    /// </summary>
    /// <typeparam name="T">仪器类型。</typeparam>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>。</param>
    /// <param name="instrumentationFactory">仪器工厂。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder AddInstrumentation<T>(
        this MeterProviderBuilder meterProviderBuilder,
        Func<IServiceProvider, MeterProvider, T> instrumentationFactory)
        where T : class
    {
        // 检查仪器工厂是否为 null
        Guard.ThrowIfNull(instrumentationFactory);

        // 配置构建器以添加仪器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is IMeterProviderBuilder iMeterProviderBuilder
                && iMeterProviderBuilder.Provider != null)
            {
                builder.AddInstrumentation(() => instrumentationFactory(sp, iMeterProviderBuilder.Provider));
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 注册回调操作以配置度量服务配置的 <see cref="IServiceCollection"/>。
    /// </summary>
    /// <remarks>
    /// 注意：度量服务仅在应用程序配置阶段可用。
    /// </remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>。</param>
    /// <param name="configure">配置回调。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder ConfigureServices(
        this MeterProviderBuilder meterProviderBuilder,
        Action<IServiceCollection> configure)
    {
        if (meterProviderBuilder is IMeterProviderBuilder iMeterProviderBuilder)
        {
            iMeterProviderBuilder.ConfigureServices(configure);
        }

        return meterProviderBuilder;
    }

    /// <summary>
    /// 注册回调操作以在应用程序 <see cref="IServiceProvider"/> 可用后配置 <see cref="MeterProviderBuilder"/>。
    /// </summary>
    /// <remarks>
    /// <para><see cref="ConfigureBuilder"/> 是一个高级 API，主要供库作者使用。</para>
    /// 注意事项：
    /// <list type="bullet">
    /// <item>在 <see cref="ConfigureBuilder"/> 内部不能向 <see cref="IServiceCollection"/> 添加服务（通过 <see cref="ConfigureServices"/>），因为 <see cref="IServiceProvider"/> 已经创建。如果访问服务，将抛出 <see cref="NotSupportedException"/>。</item>
    /// <item>库扩展方法（例如 <c>AddOtlpExporter</c> 在 <c>OpenTelemetry.Exporter.OpenTelemetryProtocol</c> 内）可能依赖于服务在当前或未来的任何时间可用。不建议在 <see cref="ConfigureBuilder"/> 内部调用库扩展方法。</item>
    /// </list>
    /// 有关更多信息，请参见：<see href="https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/metrics/customizing-the-sdk/README.md#dependency-injection-support">依赖注入支持</see>。
    /// </remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>。</param>
    /// <param name="configure">配置回调。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    internal static MeterProviderBuilder ConfigureBuilder(
        this MeterProviderBuilder meterProviderBuilder,
        Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        if (meterProviderBuilder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
        {
            deferredMeterProviderBuilder.Configure(configure);
        }

        return meterProviderBuilder;
    }
}
