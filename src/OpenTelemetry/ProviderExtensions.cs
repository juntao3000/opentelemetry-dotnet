// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

/// <summary>
/// 包含提供程序扩展方法。
/// </summary>
public static class ProviderExtensions
{
    /// <summary>
    /// 获取与 <see cref="BaseProvider"/> 关联的 <see cref="Resource"/>。
    /// </summary>
    /// <param name="baseProvider"><see cref="BaseProvider"/>。</param>
    /// <returns>如果找到则返回 <see cref="Resource"/>，否则返回 <see cref="Resource.Empty"/>。</returns>
    public static Resource GetResource([AllowNull] this BaseProvider baseProvider)
    {
        // 检查 baseProvider 是否是 TracerProviderSdk 类型
        if (baseProvider is TracerProviderSdk tracerProviderSdk)
        {
            // 返回 TracerProviderSdk 的 Resource
            return tracerProviderSdk.Resource;
        }
        // 检查 baseProvider 是否是 MeterProviderSdk 类型
        else if (baseProvider is MeterProviderSdk meterProviderSdk)
        {
            // 返回 MeterProviderSdk 的 Resource
            return meterProviderSdk.Resource;
        }
        // 检查 baseProvider 是否是 LoggerProviderSdk 类型
        else if (baseProvider is LoggerProviderSdk loggerProviderSdk)
        {
            // 返回 LoggerProviderSdk 的 Resource
            return loggerProviderSdk.Resource;
        }
        // 检查 baseProvider 是否是 OpenTelemetryLoggerProvider 类型
        else if (baseProvider is OpenTelemetryLoggerProvider openTelemetryLoggerProvider)
        {
            // 返回 OpenTelemetryLoggerProvider 的 Resource
            return openTelemetryLoggerProvider.Provider.GetResource();
        }

        // 如果没有匹配的类型，返回 Resource.Empty
        return Resource.Empty;
    }

    /// <summary>
    /// 获取与 <see cref="BaseProvider"/> 关联的 <see cref="Resource"/>。
    /// </summary>
    /// <param name="baseProvider"><see cref="BaseProvider"/>。</param>
    /// <returns>如果找到则返回 <see cref="Resource"/>，否则返回 <see cref="Resource.Empty"/>。</returns>
    public static Resource GetDefaultResource([AllowNull] this BaseProvider baseProvider)
    {
        // 创建默认的 ResourceBuilder
        var builder = ResourceBuilder.CreateDefault();
        // 设置 ServiceProvider
        builder.ServiceProvider = GetServiceProvider(baseProvider);
        // 构建并返回 Resource
        return builder.Build();
    }

    /// <summary>
    /// 获取与 <see cref="BaseProvider"/> 关联的 <see cref="IServiceProvider"/>。
    /// </summary>
    /// <param name="baseProvider"><see cref="BaseProvider"/>。</param>
    /// <returns>如果找到则返回 <see cref="IServiceProvider"/>，否则返回 null。</returns>
    internal static IServiceProvider? GetServiceProvider(this BaseProvider? baseProvider)
    {
        // 检查 baseProvider 是否是 TracerProviderSdk 类型
        if (baseProvider is TracerProviderSdk tracerProviderSdk)
        {
            // 返回 TracerProviderSdk 的 ServiceProvider
            return tracerProviderSdk.ServiceProvider;
        }
        // 检查 baseProvider 是否是 MeterProviderSdk 类型
        else if (baseProvider is MeterProviderSdk meterProviderSdk)
        {
            // 返回 MeterProviderSdk 的 ServiceProvider
            return meterProviderSdk.ServiceProvider;
        }
        // 检查 baseProvider 是否是 LoggerProviderSdk 类型
        else if (baseProvider is LoggerProviderSdk loggerProviderSdk)
        {
            // 返回 LoggerProviderSdk 的 ServiceProvider
            return loggerProviderSdk.ServiceProvider;
        }
        // 检查 baseProvider 是否是 OpenTelemetryLoggerProvider 类型
        else if (baseProvider is OpenTelemetryLoggerProvider openTelemetryLoggerProvider)
        {
            // 返回 OpenTelemetryLoggerProvider 的 ServiceProvider
            return openTelemetryLoggerProvider.Provider.GetServiceProvider();
        }

        // 如果没有匹配的类型，返回 null
        return null;
    }
}
