// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context;

// OpenTelemetry 上下文管理 API
public static class RuntimeContext
{
    // 用于存储上下文槽的并发字典
    private static readonly ConcurrentDictionary<string, object> Slots = new();

    // 上下文槽类型，默认为 AsyncLocalRuntimeContextSlot<>
    private static Type contextSlotType = typeof(AsyncLocalRuntimeContextSlot<>);

    /// <summary>
    /// 获取或设置实际的上下文载体实现。
    /// </summary>
    public static Type ContextSlotType
    {
        get => contextSlotType;
        set
        {
            Guard.ThrowIfNull(value, nameof(value));

            if (value == typeof(AsyncLocalRuntimeContextSlot<>))
            {
                contextSlotType = value;
            }
            else if (value == typeof(ThreadLocalRuntimeContextSlot<>))
            {
                contextSlotType = value;
            }
#if NETFRAMEWORK
            else if (value == typeof(RemotingRuntimeContextSlot<>))
            {
                contextSlotType = value;
            }
#endif
            else
            {
                throw new NotSupportedException($"{value} is not a supported type.");
            }
        }
    }

    /// <summary>
    /// 注册一个命名的上下文槽。
    /// </summary>
    /// <param name="slotName">上下文槽的名称。</param>
    /// <typeparam name="T">底层值的类型。</typeparam>
    /// <returns>注册的上下文槽。</returns>
    public static RuntimeContextSlot<T> RegisterSlot<T>(string slotName)
    {
        Guard.ThrowIfNullOrEmpty(slotName);

        RuntimeContextSlot<T>? slot = null;

        lock (Slots)
        {
            if (Slots.ContainsKey(slotName))
            {
                throw new InvalidOperationException($"Context slot already registered: '{slotName}'");
            }

            if (ContextSlotType == typeof(AsyncLocalRuntimeContextSlot<>))
            {
                slot = new AsyncLocalRuntimeContextSlot<T>(slotName);
            }
            else if (ContextSlotType == typeof(ThreadLocalRuntimeContextSlot<>))
            {
                slot = new ThreadLocalRuntimeContextSlot<T>(slotName);
            }

#if NETFRAMEWORK
            else if (ContextSlotType == typeof(RemotingRuntimeContextSlot<>))
            {
                slot = new RemotingRuntimeContextSlot<T>(slotName);
            }
#endif
            else
            {
                throw new NotSupportedException($"ContextSlotType '{ContextSlotType}' is not supported");
            }

            Slots[slotName] = slot;
            return slot;
        }
    }

    /// <summary>
    /// 从给定名称获取已注册的上下文槽。
    /// </summary>
    /// <param name="slotName">上下文槽的名称。</param>
    /// <typeparam name="T">底层值的类型。</typeparam>
    /// <returns>先前注册的上下文槽。</returns>
    public static RuntimeContextSlot<T> GetSlot<T>(string slotName)
    {
        Guard.ThrowIfNullOrEmpty(slotName);

        var slot = GuardNotFound(slotName);

        return Guard.ThrowIfNotOfType<RuntimeContextSlot<T>>(slot);
    }

    /// <summary>
    /// 将值设置到已注册的上下文槽中。
    /// </summary>
    /// <param name="slotName">上下文槽的名称。</param>
    /// <param name="value">要设置的值。</param>
    /// <typeparam name="T">值的类型。</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetValue<T>(string slotName, T value)
    {
        GetSlot<T>(slotName).Set(value);
    }

    /// <summary>
    /// 从已注册的上下文槽中获取值。
    /// </summary>
    /// <param name="slotName">上下文槽的名称。</param>
    /// <typeparam name="T">值的类型。</typeparam>
    /// <returns>从上下文槽中检索到的值。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? GetValue<T>(string slotName)
    {
        return GetSlot<T>(slotName).Get();
    }

    /// <summary>
    /// 将值设置到已注册的上下文槽中。
    /// </summary>
    /// <param name="slotName">上下文槽的名称。</param>
    /// <param name="value">要设置的值。</param>
    public static void SetValue(string slotName, object? value)
    {
        Guard.ThrowIfNullOrEmpty(slotName);

        var slot = GuardNotFound(slotName);

        Guard.ThrowIfNotOfType<IRuntimeContextSlotValueAccessor>(slot).Value = value;
    }

    /// <summary>
    /// 从已注册的上下文槽中获取值。
    /// </summary>
    /// <param name="slotName">上下文槽的名称。</param>
    /// <returns>从上下文槽中检索到的值。</returns>
    public static object? GetValue(string slotName)
    {
        Guard.ThrowIfNullOrEmpty(slotName);

        var slot = GuardNotFound(slotName);

        return Guard.ThrowIfNotOfType<IRuntimeContextSlotValueAccessor>(slot).Value;
    }

    // 用于测试目的
    internal static void Clear()
    {
        Slots.Clear();
    }

    // 检查上下文槽是否存在
    private static object GuardNotFound(string slotName)
    {
        if (!Slots.TryGetValue(slotName, out var slot))
        {
            throw new ArgumentException($"Context slot not found: '{slotName}'");
        }

        return slot;
    }
}
