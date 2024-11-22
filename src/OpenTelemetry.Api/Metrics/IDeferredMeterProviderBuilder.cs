// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 描述一个支持使用 <see cref="IServiceProvider"/> 进行依赖注入的延迟初始化的 meter 提供程序构建器。
/// </summary>
public interface IDeferredMeterProviderBuilder
{
    /// <summary>
    /// 注册一个回调操作，以便在应用程序的 <see cref="IServiceProvider"/> 可用时配置 <see cref="MeterProviderBuilder"/>。
    /// </summary>
    /// <param name="configure">配置回调。</param>
    /// <returns>返回提供的 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    MeterProviderBuilder Configure(Action<IServiceProvider, MeterProviderBuilder> configure);
}
