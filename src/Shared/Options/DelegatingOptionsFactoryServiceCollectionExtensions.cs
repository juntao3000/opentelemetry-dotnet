// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

// 扩展方法类，用于注册和配置选项工厂
internal static class DelegatingOptionsFactoryServiceCollectionExtensions
{
#if NET
    public static IServiceCollection RegisterOptionsFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
#else
        public static IServiceCollection RegisterOptionsFactory<T>(
#endif
        this IServiceCollection services,
        Func<IConfiguration, T> optionsFactoryFunc)
        where T : class
    {
        // 确保 services 和 optionsFactoryFunc 不为 null
        Debug.Assert(services != null, "services was null");
        Debug.Assert(optionsFactoryFunc != null, "optionsFactoryFunc was null");

        // 注册 IOptionsFactory<T> 的单例服务
        services!.TryAddSingleton<IOptionsFactory<T>>(sp =>
        {
            return new DelegatingOptionsFactory<T>(
                (c, n) => optionsFactoryFunc!(c), // 使用 optionsFactoryFunc 创建选项实例
                sp.GetRequiredService<IConfiguration>(), // 获取 IConfiguration 服务
                sp.GetServices<IConfigureOptions<T>>(), // 获取 IConfigureOptions<T> 服务集合
                sp.GetServices<IPostConfigureOptions<T>>(), // 获取 IPostConfigureOptions<T> 服务集合
                sp.GetServices<IValidateOptions<T>>() // 获取 IValidateOptions<T> 服务集合
            );
        });

        return services!;
    }

#if NET
    public static IServiceCollection RegisterOptionsFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
#else
        public static IServiceCollection RegisterOptionsFactory<T>(
#endif
        this IServiceCollection services,
        Func<IServiceProvider, IConfiguration, string, T> optionsFactoryFunc)
        where T : class
    {
        // 确保 services 和 optionsFactoryFunc 不为 null
        Debug.Assert(services != null, "services was null");
        Debug.Assert(optionsFactoryFunc != null, "optionsFactoryFunc was null");

        // 注册 IOptionsFactory<T> 的单例服务
        services!.TryAddSingleton<IOptionsFactory<T>>(sp =>
        {
            return new DelegatingOptionsFactory<T>(
                (c, n) => optionsFactoryFunc!(sp, c, n), // 使用 optionsFactoryFunc 创建选项实例
                sp.GetRequiredService<IConfiguration>(), // 获取 IConfiguration 服务
                sp.GetServices<IConfigureOptions<T>>(), // 获取 IConfigureOptions<T> 服务集合
                sp.GetServices<IPostConfigureOptions<T>>(), // 获取 IPostConfigureOptions<T> 服务集合
                sp.GetServices<IValidateOptions<T>>() // 获取 IValidateOptions<T> 服务集合
            );
        });

        return services!;
    }

#if NET
    public static IServiceCollection DisableOptionsReloading<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
#else
        public static IServiceCollection DisableOptionsReloading<T>(
#endif
        this IServiceCollection services)
        where T : class
    {
        // 确保 services 不为 null
        Debug.Assert(services != null, "services was null");

        // 注册 IOptionsMonitor<T> 和 IOptionsSnapshot<T> 的服务
        services!.TryAddSingleton<IOptionsMonitor<T>, SingletonOptionsManager<T>>();
        services!.TryAddScoped<IOptionsSnapshot<T>, SingletonOptionsManager<T>>();

        return services!;
    }
}
