// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES && NET
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// ExemplarReservoir 基类实现和契约。
/// </summary>
/// <remarks>
/// <para experimental-warning="true"><b>警告</b>: 这是一个实验性 API，可能会在未来发生变化或被移除。使用风险自负。</para>
/// 规范: <see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplarreservoir"/>.
/// </remarks>
#if NET
[Experimental(DiagnosticDefinitions.ExemplarReservoirExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
public
#else
internal
#endif
    abstract class ExemplarReservoir // ExemplarReservoir 抽象类，表示示例库的基类
{
    // 注意: 这个构造函数是 internal 的，因为我们不允许自定义的 ExemplarReservoir 实现直接基于基类，只能基于 FixedSizeExemplarReservoir。
    internal ExemplarReservoir() // ExemplarReservoir 构造函数
    {
    }

    /// <summary>
    /// 获取一个值，指示 <see cref="ExemplarReservoir"/> 在执行收集时是否应重置其状态。
    /// </summary>
    /// <remarks>
    /// 注意: 对于使用增量聚合时间性的 <see cref="MetricPoint"/>，<see cref="ResetOnCollect"/> 设置为 <see langword="true"/>，对于使用累积聚合时间性的 <see cref="MetricPoint"/>，设置为 <see langword="false"/>。
    /// </remarks>
    public bool ResetOnCollect { get; private set; } // 指示是否在收集时重置状态的属性

    /// <summary>
    /// 向库中提供一个测量值。
    /// </summary>
    /// <param name="measurement"><see cref="ExemplarMeasurement{T}"/>.</param>
    public abstract void Offer(in ExemplarMeasurement<long> measurement); // 提供一个长整型测量值的抽象方法

    /// <summary>
    /// 向库中提供一个测量值。
    /// </summary>
    /// <param name="measurement"><see cref="ExemplarMeasurement{T}"/>.</param>
    public abstract void Offer(in ExemplarMeasurement<double> measurement); // 提供一个双精度型测量值的抽象方法

    /// <summary>
    /// 收集库中累积的所有示例。
    /// </summary>
    /// <returns><see cref="ReadOnlyExemplarCollection"/>.</returns>
    public abstract ReadOnlyExemplarCollection Collect(); // 收集所有累积示例的抽象方法

    internal virtual void Initialize(AggregatorStore aggregatorStore) // 初始化方法
    {
        this.ResetOnCollect = aggregatorStore.OutputDelta; // 根据 AggregatorStore 的 OutputDelta 属性设置 ResetOnCollect
    }
}
