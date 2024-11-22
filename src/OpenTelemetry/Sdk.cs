// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if EXPOSE_EXPERIMENTAL_FEATURES && NET
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

/// <summary>
/// OpenTelemetry 帮助类。
/// </summary>
public static class Sdk // OpenTelemetry 帮助类
{
    static Sdk() // 静态构造函数，用于初始化静态成员
    {
        // 设置默认的 TextMapPropagator
        Propagators.DefaultTextMapPropagator = new CompositeTextMapPropagator(new TextMapPropagator[]
        {
                new TraceContextPropagator(),
                new BaggagePropagator(),
        });

        // 设置默认的 ActivityId 格式为 W3C
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
        SelfDiagnostics.EnsureInitialized();

        // 获取 SDK 程序集的信息版本
        var sdkAssembly = typeof(Sdk).Assembly;
        InformationalVersion = sdkAssembly.GetPackageVersion();
    }

    /// <summary>
    /// 获取一个值，该值指示是否抑制（禁用）检测。
    /// </summary>
    public static bool SuppressInstrumentation => SuppressInstrumentationScope.IsSuppressed; // 是否抑制检测

    internal static string InformationalVersion { get; } // SDK 信息版本

    /// <summary>
    /// 设置默认的 TextMapPropagator。
    /// </summary>
    /// <param name="textMapPropagator">要设置为默认的 TextMapPropagator。</param>
    public static void SetDefaultTextMapPropagator(TextMapPropagator textMapPropagator) // 设置默认的 TextMapPropagator
    {
        Guard.ThrowIfNull(textMapPropagator);

        Propagators.DefaultTextMapPropagator = textMapPropagator;
    }

    /// <summary>
    /// 创建一个 <see cref="MeterProviderBuilder"/>，用于构建 <see cref="MeterProvider"/>。
    /// 在典型的应用程序中，单个 <see cref="MeterProvider"/> 在应用程序启动时创建，并在应用程序关闭时释放。
    /// 确保提供程序不会过早释放非常重要。
    /// </summary>
    /// <returns><see cref="MeterProviderBuilder"/> 实例，用于构建 <see cref="MeterProvider"/>。</returns>
    public static MeterProviderBuilder CreateMeterProviderBuilder() // 创建 MeterProviderBuilder
    {
        return new MeterProviderBuilderBase();
    }

    /// <summary>
    /// 创建一个 <see cref="TracerProviderBuilder"/>，用于构建 <see cref="TracerProvider"/>。
    /// 在典型的应用程序中，单个 <see cref="TracerProvider"/> 在应用程序启动时创建，并在应用程序关闭时释放。
    /// 确保提供程序不会过早释放非常重要。
    /// </summary>
    /// <returns><see cref="TracerProviderBuilder"/> 实例，用于构建 <see cref="TracerProvider"/>。</returns>
    public static TracerProviderBuilder CreateTracerProviderBuilder() // 创建 TracerProviderBuilder
    {
        return new TracerProviderBuilderBase();
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// 创建一个 <see cref="LoggerProviderBuilder"/>，用于构建 <see cref="LoggerProvider"/>。
    /// 在典型的应用程序中，单个 <see cref="LoggerProvider"/> 在应用程序启动时创建，并在应用程序关闭时释放。
    /// 确保提供程序不会过早释放非常重要。
    /// </summary>
    /// <remarks><b>警告</b>：这是一个实验性 API，可能会在将来更改或删除。使用风险自负。</remarks>
    /// <returns><see cref="LoggerProviderBuilder"/> 实例，用于构建 <see cref="LoggerProvider"/>。</returns>
#if NET
    [Experimental(DiagnosticDefinitions.LogsBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
    public
#else
        /// <summary>
        /// 创建一个 <see cref="LoggerProviderBuilder"/>，用于构建 <see cref="LoggerProvider"/>。
        /// 在典型的应用程序中，单个 <see cref="LoggerProvider"/> 在应用程序启动时创建，并在应用程序关闭时释放。
        /// 确保提供程序不会过早释放非常重要。
        /// </summary>
        /// <returns><see cref="LoggerProviderBuilder"/> 实例，用于构建 <see cref="LoggerProvider"/>。</returns>
        internal
#endif
            static LoggerProviderBuilder CreateLoggerProviderBuilder() // 创建 LoggerProviderBuilder
    {
        return new LoggerProviderBuilderBase();
    }
}
