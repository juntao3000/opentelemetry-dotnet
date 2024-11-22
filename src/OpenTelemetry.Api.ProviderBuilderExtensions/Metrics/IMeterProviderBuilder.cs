// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 描述一个由 <see cref="IServiceCollection"/> 支持的 <see cref="MeterProviderBuilder"/>。
/// </summary>
// 注意：如果有需要，这个 API 可能会公开。
internal interface IMeterProviderBuilder : IDeferredMeterProviderBuilder
{
    /// <summary>
    /// 获取由构建器构建的 <see cref="MeterProvider"/>。
    /// </summary>
    /// <remarks>
    /// 注意：在构建开始并且 <see cref="IServiceCollection"/> 已关闭之前，<see cref="Provider"/> 应返回 <see langword="null"/>。
    /// </remarks>
    MeterProvider? Provider { get; }

    /// <summary>
    /// 注册一个回调操作来配置度量服务配置的 <see cref="IServiceCollection"/>。
    /// </summary>
    /// <remarks>
    /// 注意：度量服务仅在应用程序配置阶段可用。如果在应用程序 <see cref="IServiceProvider"/> 创建后配置服务，则此方法应抛出 <see cref="NotSupportedException"/>。
    /// </remarks>
    /// <param name="configure">配置回调。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    MeterProviderBuilder ConfigureServices(Action<IServiceCollection> configure);
}
