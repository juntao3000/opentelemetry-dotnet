// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Context;

/// <summary>
/// 异步本地上下文槽的实现。
/// </summary>
/// <typeparam name="T">底层值的类型。</typeparam>
public class AsyncLocalRuntimeContextSlot<T> : RuntimeContextSlot<T>, IRuntimeContextSlotValueAccessor
{
    // 异步本地存储槽
    private readonly AsyncLocal<T> slot;

    /// <summary>
    /// 初始化 <see cref="AsyncLocalRuntimeContextSlot{T}"/> 类的新实例。
    /// </summary>
    /// <param name="name">上下文槽的名称。</param>
    public AsyncLocalRuntimeContextSlot(string name)
        : base(name)
    {
        // 初始化异步本地存储槽
        this.slot = new AsyncLocal<T>();
    }

    /// <inheritdoc/>
    public object? Value
    {
        // 获取槽的值
        get => this.slot.Value;
        // 设置槽的值
        set
        {
            // 如果 T 是值类型且 value 为 null，则设置为默认值
            if (typeof(T).IsValueType && value is null)
            {
                this.slot.Value = default!;
            }
            else
            {
                // 否则将 value 转换为 T 类型并设置
                this.slot.Value = (T)value!;
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override T? Get()
    {
        // 从槽中获取值
        return this.slot.Value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Set(T value)
    {
        // 将值设置到槽中
        this.slot.Value = value;
    }
}
