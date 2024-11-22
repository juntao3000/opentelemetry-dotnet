// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Context;

/// <summary>
/// 描述一种 <see cref="RuntimeContextSlot{T}"/> 类型，可以将其值公开为 <see cref="object"/>。
/// </summary>
public interface IRuntimeContextSlotValueAccessor
{
    /// <summary>
    /// 获取或设置槽的值为 <see cref="object"/>。
    /// </summary>
    object? Value { get; set; } // 定义一个可空的对象类型属性，用于获取或设置槽的值
}
