// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// MeterProviderServiceCollectionBuilder 类，继承自 MeterProviderBuilder，实现了 IMeterProviderBuilder 接口。
/// </summary>
internal sealed class MeterProviderServiceCollectionBuilder : MeterProviderBuilder, IMeterProviderBuilder
{
    /// <summary>
    /// 构造函数，初始化 MeterProviderServiceCollectionBuilder 类的新实例。
    /// </summary>
    /// <param name="services">IServiceCollection 实例。</param>
    public MeterProviderServiceCollectionBuilder(IServiceCollection services)
    {
        // 配置 OpenTelemetry MeterProvider
        services.ConfigureOpenTelemetryMeterProvider((sp, builder) => this.Services = null);

        // 设置 Services 属性
        this.Services = services;
    }

    /// <summary>
    /// 获取或设置 IServiceCollection 实例。
    /// </summary>
    public IServiceCollection? Services { get; set; }

    /// <summary>
    /// 获取 MeterProvider 实例。
    /// </summary>
    public MeterProvider? Provider => null;

    /// <inheritdoc />
    /// <summary>
    /// 向提供程序添加仪器。
    /// </summary>
    /// <typeparam name="TInstrumentation">仪器类的类型。</typeparam>
    /// <param name="instrumentationFactory">构建仪器的函数。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        // 检查 instrumentationFactory 是否为 null
        Guard.ThrowIfNull(instrumentationFactory);

        // 配置内部构建器
        this.ConfigureBuilderInternal((sp, builder) =>
        {
            builder.AddInstrumentation(instrumentationFactory);
        });

        return this;
    }

    /// <inheritdoc />
    /// <summary>
    /// 将给定的 Meter 名称添加到订阅的 meters 列表中。
    /// </summary>
    /// <param name="names">Meter 名称。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public override MeterProviderBuilder AddMeter(params string[] names)
    {
        // 检查 names 是否为 null
        Guard.ThrowIfNull(names);

        // 配置内部构建器
        this.ConfigureBuilderInternal((sp, builder) =>
        {
            builder.AddMeter(names);
        });

        return this;
    }

    /// <inheritdoc />
    /// <summary>
    /// 注册一个回调操作来配置 IServiceCollection。
    /// </summary>
    /// <param name="configure">配置回调。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public MeterProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
        => this.ConfigureServicesInternal(configure);

    /// <inheritdoc cref="IDeferredMeterProviderBuilder.Configure" />
    /// <summary>
    /// 注册一个回调操作来配置 MeterProviderBuilder。
    /// </summary>
    /// <param name="configure">配置回调。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public MeterProviderBuilder ConfigureBuilder(Action<IServiceProvider, MeterProviderBuilder> configure)
        => this.ConfigureBuilderInternal(configure);

    /// <inheritdoc />
    /// <summary>
    /// 注册一个回调操作来配置 MeterProviderBuilder。
    /// </summary>
    /// <param name="configure">配置回调。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    MeterProviderBuilder IDeferredMeterProviderBuilder.Configure(Action<IServiceProvider, MeterProviderBuilder> configure)
        => this.ConfigureBuilderInternal(configure);

    /// <summary>
    /// 配置内部构建器。
    /// </summary>
    /// <param name="configure">配置回调。</param>
    /// <returns>返回 <see cref="MeterProviderServiceCollectionBuilder"/> 以便链式调用。</returns>
    private MeterProviderServiceCollectionBuilder ConfigureBuilderInternal(Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        // 获取 Services 实例，如果为 null 则抛出异常
        var services = this.Services
            ?? throw new NotSupportedException("Builder cannot be configured during MeterProvider construction.");

        // 配置 OpenTelemetry MeterProvider
        services.ConfigureOpenTelemetryMeterProvider(configure);

        return this;
    }

    /// <summary>
    /// 配置 IServiceCollection。
    /// </summary>
    /// <param name="configure">配置回调。</param>
    /// <returns>返回 <see cref="MeterProviderServiceCollectionBuilder"/> 以便链式调用。</returns>
    private MeterProviderServiceCollectionBuilder ConfigureServicesInternal(Action<IServiceCollection> configure)
    {
        // 检查 configure 是否为 null
        Guard.ThrowIfNull(configure);

        // 获取 Services 实例，如果为 null 则抛出异常
        var services = this.Services
            ?? throw new NotSupportedException("Services cannot be configured during MeterProvider construction.");

        // 配置 IServiceCollection
        configure(services);

        return this;
    }
}
