// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Context;

namespace OpenTelemetry.Metrics;

internal sealed class PullMetricScope : IDisposable
{
    // 定义一个静态的上下文槽，用于存储布尔值，表示是否允许拉取指标
    private static readonly RuntimeContextSlot<bool> Slot = RuntimeContext.RegisterSlot<bool>("otel.pull_metric");

    // 存储之前的槽值
    private readonly bool previousValue;
    private bool disposed;

    // 构造函数，设置新的槽值，并保存之前的槽值
    internal PullMetricScope(bool value = true)
    {
        this.previousValue = Slot.Get(); // 获取之前的槽值
        Slot.Set(value); // 设置新的槽值
    }

    // 静态属性，获取当前槽值，表示是否允许拉取指标
    internal static bool IsPullAllowed => Slot.Get();

    // 静态方法，开始一个新的 PullMetricScope 实例
    public static IDisposable Begin(bool value = true)
    {
        return new PullMetricScope(value);
    }

    /// <inheritdoc/>
    // 实现 IDisposable 接口，恢复之前的槽值
    public void Dispose()
    {
        if (!this.disposed)
        {
            Slot.Set(this.previousValue); // 恢复之前的槽值
            this.disposed = true; // 标记为已释放
        }
    }
}
