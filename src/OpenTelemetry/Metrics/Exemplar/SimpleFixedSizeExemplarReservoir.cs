// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// SimpleFixedSizeExemplarReservoir 实现。
/// </summary>
/// <remarks>
/// 规范: <see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#simplefixedsizeexemplarreservoir"/>。
/// </remarks>
internal sealed class SimpleFixedSizeExemplarReservoir : FixedSizeExemplarReservoir // 表示一个简单的固定大小的 Exemplar 水库
{
    private const int DefaultMeasurementState = -1; // 默认的测量状态
    private int measurementState = DefaultMeasurementState; // 当前的测量状态

    public SimpleFixedSizeExemplarReservoir(int poolSize) // 构造函数，初始化 SimpleFixedSizeExemplarReservoir 实例
        : base(poolSize)
    {
    }

    public override void Offer(in ExemplarMeasurement<long> measurement) // 提供一个 long 类型的测量值
    {
        this.Offer(in measurement);
    }

    public override void Offer(in ExemplarMeasurement<double> measurement) // 提供一个 double 类型的测量值
    {
        this.Offer(in measurement);
    }

    protected override void OnCollected() // 收集完成后触发的方法
    {
        // 重置内部状态，无论时间类型如何。
        // 这确保了传入的测量值有公平的机会进入水库。
        this.measurementState = DefaultMeasurementState;
    }

    private void Offer<T>(in ExemplarMeasurement<T> measurement) // 提供一个测量值
        where T : struct
    {
        var measurementState = Interlocked.Increment(ref this.measurementState); // 增加测量状态

        if (measurementState < this.Capacity) // 如果测量状态小于容量
        {
            this.UpdateExemplar(measurementState, in measurement); // 更新指定索引的 Exemplar
        }
        else
        {
            int index = ThreadSafeRandom.Next(0, measurementState); // 生成一个随机索引
            if (index < this.Capacity) // 如果随机索引小于容量
            {
                this.UpdateExemplar(index, in measurement); // 更新指定索引的 Exemplar
            }
        }
    }
}
