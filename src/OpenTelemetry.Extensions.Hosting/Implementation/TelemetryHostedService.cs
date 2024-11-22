// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// Warning: Do not change the namespace or class name in this file! Azure
// Functions has taken a dependency on the specific details:
// https://github.com/Azure/azure-functions-host/blob/d4655cc4fbb34fc14e6861731991118a9acd02ed/src/WebJobs.Script.WebHost/DependencyInjection/DependencyValidator/DependencyValidator.cs#L57

namespace OpenTelemetry.Extensions.Hosting.Implementation;

// TelemetryHostedService 类实现了 IHostedService 接口，用于确保所有的仪表化、导出器等被创建和启动
internal sealed class TelemetryHostedService : IHostedService
{
    // serviceProvider 是一个依赖注入容器，用于获取服务实例
    private readonly IServiceProvider serviceProvider;

    // 构造函数，接收一个 IServiceProvider 实例
    public TelemetryHostedService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    // StartAsync 方法在服务启动时被调用
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 这个 HostedService 的唯一目的是确保所有的仪表化、导出器等被创建和启动
        Initialize(this.serviceProvider);

        return Task.CompletedTask;
    }

    // StopAsync 方法在服务停止时被调用
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    // Initialize 方法用于初始化所有的仪表化、导出器等
    internal static void Initialize(IServiceProvider serviceProvider)
    {
        Debug.Assert(serviceProvider != null, "serviceProvider 为空");

        // 获取 MeterProvider 实例，如果未注册则记录日志
        var meterProvider = serviceProvider!.GetService<MeterProvider>();
        if (meterProvider == null)
        {
            HostingExtensionsEventSource.Log.MeterProviderNotRegistered();
        }

        // 获取 TracerProvider 实例，如果未注册则记录日志
        var tracerProvider = serviceProvider!.GetService<TracerProvider>();
        if (tracerProvider == null)
        {
            HostingExtensionsEventSource.Log.TracerProviderNotRegistered();
        }

        // 获取 LoggerProvider 实例，如果未注册则记录日志
        var loggerProvider = serviceProvider!.GetService<LoggerProvider>();
        if (loggerProvider == null)
        {
            HostingExtensionsEventSource.Log.LoggerProviderNotRegistered();
        }
    }
}
