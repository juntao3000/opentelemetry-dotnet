// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 表示配置 <see cref="MeterProviderBuilder"/> 类型的对象。
/// </summary>
// 注意：如果有需要，此 API 可能会公开。
internal interface IConfigureMeterProviderBuilder
{
    /// <summary>
    /// 调用以配置 <see cref="MeterProviderBuilder"/> 实例。
    /// </summary>
    /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    void ConfigureBuilder(IServiceProvider serviceProvider, MeterProviderBuilder meterProviderBuilder);
}
