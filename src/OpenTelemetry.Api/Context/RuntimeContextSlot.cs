// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Context;

/// <summary>
/// 抽象的上下文槽。
/// </summary>
/// <typeparam name="T">底层值的类型。</typeparam>
public abstract class RuntimeContextSlot<T> : IDisposable
{
    /// <summary>
    /// 初始化 <see cref="RuntimeContextSlot{T}"/> 类的新实例。
    /// </summary>
    /// <param name="name">上下文槽的名称。</param>
    protected RuntimeContextSlot(string name)
    {
        // 检查名称是否为 null 或空字符串
        Guard.ThrowIfNullOrEmpty(name);

        // 设置上下文槽的名称
        this.Name = name;
    }

    /// <summary>
    /// 获取上下文槽的名称。
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// 从上下文槽中获取值。
    /// </summary>
    /// <returns>从上下文槽中检索到的值。</returns>
    public abstract T? Get();

    /// <summary>
    /// 将值设置到上下文槽中。
    /// </summary>
    /// <param name="value">要设置的值。</param>
    public abstract void Set(T value);

    /// <inheritdoc/>
    public void Dispose()
    {
        // 释放资源
        this.Dispose(disposing: true);
        // 通知垃圾回收器不再调用终结器
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放该类使用的非托管资源，并可选择性地释放托管资源。
    /// </summary>
    /// <param name="disposing"><see langword="true"/> 表示释放托管和非托管资源；<see langword="false"/> 表示仅释放非托管资源。</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
