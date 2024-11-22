// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 包含构建 <see cref="MeterProvider"/> 实例的方法。
/// </summary>
public class MeterProviderBuilderBase : MeterProviderBuilder, IMeterProviderBuilder
{
    // 指示是否允许构建 MeterProvider 的标志
    private readonly bool allowBuild;
    // 内部构建器，用于管理服务集合
    private readonly MeterProviderServiceCollectionBuilder innerBuilder;

    /// <summary>
    /// 初始化 <see cref="MeterProviderBuilderBase"/> 类的新实例。
    /// </summary>
    public MeterProviderBuilderBase()
    {
        // 创建一个新的服务集合
        var services = new ServiceCollection();

        // 添加 OpenTelemetry 共享提供程序构建器服务和 MeterProvider 构建器服务
        services
            .AddOpenTelemetrySharedProviderBuilderServices()
            .AddOpenTelemetryMeterProviderBuilderServices()
            .TryAddSingleton<MeterProvider>(
                sp => throw new NotSupportedException("Self-contained MeterProvider cannot be accessed using the application IServiceProvider call Build instead."));

        // 初始化内部构建器
        this.innerBuilder = new MeterProviderServiceCollectionBuilder(services);

        // 允许构建
        this.allowBuild = true;
    }

    /// <summary>
    /// 使用指定的服务集合初始化 <see cref="MeterProviderBuilderBase"/> 类的新实例。
    /// </summary>
    /// <param name="services">服务集合。</param>
    internal MeterProviderBuilderBase(IServiceCollection services)
    {
        // 检查服务集合是否为 null
        Guard.ThrowIfNull(services);

        // 添加 OpenTelemetry MeterProvider 构建器服务
        services
            .AddOpenTelemetryMeterProviderBuilderServices()
            .TryAddSingleton<MeterProvider>(sp => new MeterProviderSdk(sp, ownsServiceProvider: false));

        // 初始化内部构建器
        this.innerBuilder = new MeterProviderServiceCollectionBuilder(services);

        // 不允许构建
        this.allowBuild = false;
    }

    /// <inheritdoc />
    MeterProvider? IMeterProviderBuilder.Provider => null;

    /// <inheritdoc />
    public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        // 添加仪器
        this.innerBuilder.AddInstrumentation(instrumentationFactory);

        return this;
    }

    /// <inheritdoc />
    public override MeterProviderBuilder AddMeter(params string[] names)
    {
        // 添加 Meter 名称
        this.innerBuilder.AddMeter(names);

        return this;
    }

    /// <inheritdoc />
    MeterProviderBuilder IMeterProviderBuilder.ConfigureServices(Action<IServiceCollection> configure)
    {
        // 配置服务集合
        this.innerBuilder.ConfigureServices(configure);

        return this;
    }

    /// <inheritdoc />
    MeterProviderBuilder IDeferredMeterProviderBuilder.Configure(Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        // 配置构建器
        this.innerBuilder.ConfigureBuilder(configure);

        return this;
    }

    /// <summary>
    /// 调用构建方法以初始化 <see cref="MeterProvider"/>。
    /// </summary>
    /// <returns><see cref="MeterProvider"/> 实例。</returns>
    internal MeterProvider InvokeBuild()
        => this.Build();

    /// <summary>
    /// 运行配置的操作以初始化 <see cref="MeterProvider"/>。
    /// </summary>
    /// <returns><see cref="MeterProvider"/> 实例。</returns>
    protected MeterProvider Build()
    {
        // 检查是否允许构建
        if (!this.allowBuild)
        {
            throw new NotSupportedException("A MeterProviderBuilder bound to external service cannot be built directly. Access the MeterProvider using the application IServiceProvider instead.");
        }

        // 获取服务集合
        var services = this.innerBuilder.Services
            ?? throw new NotSupportedException("MeterProviderBuilder build method cannot be called multiple times.");

        // 清空服务集合
        this.innerBuilder.Services = null;

#if DEBUG
        // 在调试模式下验证作用域
        bool validateScopes = true;
#else
        // 在非调试模式下不验证作用域
        bool validateScopes = false;
#endif
        // 构建服务提供程序
        var serviceProvider = services.BuildServiceProvider(validateScopes);

        // 返回新的 MeterProvider 实例
        return new MeterProviderSdk(serviceProvider, ownsServiceProvider: true);
    }
}
