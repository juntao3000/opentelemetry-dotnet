// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Context;

/// <summary>
/// 上下文槽的线程本地 (TLS) 实现。
/// </summary>
/// <typeparam name="T">底层值的类型。</typeparam>
public class ThreadLocalRuntimeContextSlot<T> : RuntimeContextSlot<T>, IRuntimeContextSlotValueAccessor
{
    // 线程本地存储的槽
    private readonly ThreadLocal<T> slot;
    // 标识对象是否已被释放
    private bool disposed;

    /// <summary>
    /// 初始化 <see cref="ThreadLocalRuntimeContextSlot{T}"/> 类的新实例。
    /// </summary>
    /// <param name="name">上下文槽的名称。</param>
    public ThreadLocalRuntimeContextSlot(string name)
        : base(name)
    {
        // 初始化线程本地存储槽
        this.slot = new ThreadLocal<T>();
    }

    /// <inheritdoc/>
    public object? Value
    {
        get => this.slot.Value;
        set
        {
            // 如果 T 是值类型且 value 为 null，则将槽值设置为默认值
            if (typeof(T).IsValueType && value is null)
            {
                this.slot.Value = default!;
            }
            else
            {
                // 否则将槽值设置为 value
                this.slot.Value = (T)value!;
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override T? Get()
    {
        // 获取槽中的值
        return this.slot.Value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Set(T value)
    {
        // 设置槽中的值
        this.slot.Value = value;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                // 释放槽资源
                this.slot.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }
}
