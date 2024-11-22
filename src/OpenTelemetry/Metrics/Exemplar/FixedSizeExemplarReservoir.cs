// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES && NET
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// 一个 <see cref="ExemplarReservoir"/> 的实现，包含固定数量的 <see cref="Exemplar"/>。
/// </summary>
/// <remarks><inheritdoc cref="ExemplarReservoir" path="/remarks/para[@experimental-warning='true']"/></remarks>
#if NET
[Experimental(DiagnosticDefinitions.ExemplarReservoirExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
public
#else
    internal
#endif
    abstract class FixedSizeExemplarReservoir : ExemplarReservoir // FixedSizeExemplarReservoir 类表示一个包含固定数量 Exemplar 的水库
{
    private readonly Exemplar[] runningExemplars; // 运行中的 Exemplar 数组
    private readonly Exemplar[] snapshotExemplars; // 快照中的 Exemplar 数组

    /// <summary>
    /// 初始化 <see cref="FixedSizeExemplarReservoir"/> 类的新实例。
    /// </summary>
    /// <param name="capacity">水库中包含的 <see cref="Exemplar"/> 的容量（数量）。</param>
#pragma warning disable RS0022 // Constructor make noninheritable base class inheritable
    protected FixedSizeExemplarReservoir(int capacity) // 构造函数，初始化 FixedSizeExemplarReservoir 实例
#pragma warning restore RS0022 // Constructor make noninheritable base class inheritable
    {
        // 注意：RS0022 被抑制是因为我们确实希望允许通过从 FixedSizeExemplarReservoir 派生来创建自定义 ExemplarReservoir 实现。

        Guard.ThrowIfOutOfRange(capacity, min: 1); // 检查容量是否在有效范围内

        this.runningExemplars = new Exemplar[capacity]; // 初始化运行中的 Exemplar 数组
        this.snapshotExemplars = new Exemplar[capacity]; // 初始化快照中的 Exemplar 数组
        this.Capacity = capacity; // 设置容量
    }

    /// <summary>
    /// 获取水库中包含的 <see cref="Exemplar"/> 的容量（数量）。
    /// </summary>
    public int Capacity { get; } // 获取容量

    /// <summary>
    /// 收集水库中累积的所有示例。
    /// </summary>
    /// <returns><see cref="ReadOnlyExemplarCollection"/>。</returns>
    public sealed override ReadOnlyExemplarCollection Collect() // 收集所有累积的示例
    {
        var runningExemplars = this.runningExemplars; // 获取运行中的 Exemplar 数组

        for (int i = 0; i < runningExemplars.Length; i++) // 遍历运行中的 Exemplar 数组
        {
            ref var running = ref runningExemplars[i]; // 获取当前运行中的 Exemplar 的引用

            running.Collect(
                ref this.snapshotExemplars[i], // 收集当前 Exemplar 的快照
                reset: this.ResetOnCollect); // 根据 ResetOnCollect 标志重置
        }

        this.OnCollected(); // 调用 OnCollected 方法

        return new(this.snapshotExemplars); // 返回快照中的 Exemplar 集合
    }

    internal sealed override void Initialize(AggregatorStore aggregatorStore) // 初始化方法
    {
        var viewDefinedTagKeys = aggregatorStore.TagKeysInteresting; // 获取视图定义的标签键

        for (int i = 0; i < this.runningExemplars.Length; i++) // 遍历运行中的 Exemplar 数组
        {
            this.runningExemplars[i].ViewDefinedTagKeys = viewDefinedTagKeys; // 设置视图定义的标签键
            this.snapshotExemplars[i].ViewDefinedTagKeys = viewDefinedTagKeys; // 设置视图定义的标签键
        }

        base.Initialize(aggregatorStore); // 调用基类的 Initialize 方法
    }

    internal void UpdateExemplar<T>(
        int exemplarIndex,
        in ExemplarMeasurement<T> measurement) // 更新指定索引的 Exemplar
        where T : struct
    {
        this.runningExemplars[exemplarIndex].Update(in measurement); // 更新运行中的 Exemplar
    }

    /// <summary>
    /// 在 <see cref="Collect"/> 完成后，在返回 <see cref="ReadOnlyExemplarCollection"/> 之前触发。
    /// </summary>
    /// <remarks>
    /// 注意：此方法通常用于重置水库的状态，并且无论 <see cref="ExemplarReservoir.ResetOnCollect"/> 的值如何，都会调用此方法。
    /// </remarks>
    protected virtual void OnCollected() // 收集完成后触发的方法
    {
    }

    /// <summary>
    /// 使用 <see cref="ExemplarMeasurement{T}"/> 更新存储在水库中指定索引处的 <see cref="Exemplar"/>。
    /// </summary>
    /// <param name="exemplarIndex">要更新的 <see cref="Exemplar"/> 的索引。</param>
    /// <param name="measurement"><see cref="ExemplarMeasurement{T}"/>。</param>
    protected void UpdateExemplar(
        int exemplarIndex,
        in ExemplarMeasurement<long> measurement) // 更新指定索引的 Exemplar
    {
        this.runningExemplars[exemplarIndex].Update(in measurement); // 更新运行中的 Exemplar
    }

    /// <summary>
    /// 使用 <see cref="ExemplarMeasurement{T}"/> 更新存储在水库中指定索引处的 <see cref="Exemplar"/>。
    /// </summary>
    /// <param name="exemplarIndex">要更新的 <see cref="Exemplar"/> 的索引。</param>
    /// <param name="measurement"><see cref="ExemplarMeasurement{T}"/>。</param>
    protected void UpdateExemplar(
        int exemplarIndex,
        in ExemplarMeasurement<double> measurement) // 更新指定索引的 Exemplar
    {
        this.runningExemplars[exemplarIndex].Update(in measurement); // 更新运行中的 Exemplar
    }
}
