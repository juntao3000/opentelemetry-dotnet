// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 扩展方法，用于在 <see cref="IServiceCollection"/> 中设置 OpenTelemetry 服务。
/// </summary>
public static class OpenTelemetryServicesExtensions
{
    /// <summary>
    /// 将 OpenTelemetry SDK 服务添加到提供的 <see cref="IServiceCollection"/> 中。
    /// </summary>
    /// <remarks>
    /// 注意事项：
    /// <list type="bullet">
    /// <item>这可以安全地被多次调用，并且可以由库作者调用。对于给定的 <see cref="IServiceCollection"/>，只会创建一个 <see cref="TracerProvider"/> 和/或 <see cref="MeterProvider"/>。</item>
    /// <item>OpenTelemetry SDK 服务会插入到 <see cref="IServiceCollection"/> 的开头，并随主机一起启动。有关事件顺序和在 <see cref="IHostedService"/> 中捕获遥测的详细信息，请参见：<see href="https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Extensions.Hosting/README.md#hosted-service-ordering-and-telemetry-capture" />。</item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection"/>。</param>
    /// <returns>返回提供的 <see cref="OpenTelemetryBuilder"/> 以便链式调用。</returns>
    public static OpenTelemetryBuilder AddOpenTelemetry(this IServiceCollection services)
    {
        // 检查 services 是否为 null，如果是则抛出异常
        Guard.ThrowIfNull(services);

        // 检查 services 中是否已经存在 TelemetryHostedService，如果不存在则插入
        if (!services.Any((ServiceDescriptor d) => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(TelemetryHostedService)))
        {
            // 将 TelemetryHostedService 插入到 services 的开头
            services.Insert(0, ServiceDescriptor.Singleton<IHostedService, TelemetryHostedService>());
        }

        // 返回新的 OpenTelemetryBuilder 实例
        return new(services);
    }
}
