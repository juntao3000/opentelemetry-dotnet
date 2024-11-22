// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;

namespace OpenTelemetry.Context;

/// <summary>
/// .NET Remoting 实现的上下文槽。
/// </summary>
/// <typeparam name="T">底层值的类型。</typeparam>
public class RemotingRuntimeContextSlot<T> : RuntimeContextSlot<T>, IRuntimeContextSlotValueAccessor
{
    // 一个特殊的解决方法，用于抑制跨 AppDomain 的上下文传播。
    //
    // 默认情况下，添加到 System.Runtime.Remoting.Messaging.CallContext 的值
    // 将在 AppDomain 边界之间进行编组/解组。如果目标 AppDomain 没有相应的类型来解组数据，
    // 这将导致严重问题。
    // 最糟糕的情况是 AppDomain 崩溃并抛出 ReflectionLoadTypeException。
    //
    // 解决方法是使用一个在所有 AppDomain 中都存在的已知类型，并将实际的有效负载作为非公共字段，
    // 以便在编组期间忽略该字段。
    private static readonly FieldInfo WrapperField = typeof(BitArray).GetField("_syncRoot", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 初始化 <see cref="RemotingRuntimeContextSlot{T}"/> 类的新实例。
    /// </summary>
    /// <param name="name">上下文槽的名称。</param>
    public RemotingRuntimeContextSlot(string name)
        : base(name)
    {
    }

    /// <inheritdoc/>
    public object? Value
    {
        get => this.Get();
        set
        {
            if (typeof(T).IsValueType && value is null)
            {
                this.Set(default!);
            }
            else
            {
                this.Set((T)value!);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override T? Get()
    {
        // 从 CallContext 中获取数据，如果不存在则返回默认值
        if (CallContext.LogicalGetData(this.Name) is not BitArray wrapper)
        {
            return default;
        }

        // 从包装器中获取实际值
        var value = WrapperField.GetValue(wrapper);

        // 如果 T 是值类型且值为 null，则返回默认值
        if (typeof(T).IsValueType && value is null)
        {
            return default;
        }

        // 返回实际值
        return (T)value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Set(T value)
    {
        // 创建一个新的 BitArray 包装器
        var wrapper = new BitArray(0);
        // 将实际值设置到包装器的非公共字段中
        WrapperField.SetValue(wrapper, value);
        // 将包装器设置到 CallContext 中
        CallContext.LogicalSetData(this.Name, wrapper);
    }
}
#endif
