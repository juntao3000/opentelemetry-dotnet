// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// MeterProviderBuilder 基类。
/// </summary>
public abstract class MeterProviderBuilder
{
    /// <summary>
    /// 初始化 <see cref="MeterProviderBuilder"/> 类的新实例。
    /// </summary>
    protected MeterProviderBuilder()
    {
    }

    /// <summary>
    /// 向提供程序添加仪器。
    /// </summary>
    /// <typeparam name="TInstrumentation">仪器类的类型。</typeparam>
    /// <param name="instrumentationFactory">构建仪器的函数。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public abstract MeterProviderBuilder AddInstrumentation<TInstrumentation>(
        Func<TInstrumentation> instrumentationFactory)
        where TInstrumentation : class?;

    /// <summary>
    /// 将给定的 Meter 名称添加到订阅的 meters 列表中。
    /// </summary>
    /// <param name="names">Meter 名称。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public abstract MeterProviderBuilder AddMeter(params string[] names);
}
