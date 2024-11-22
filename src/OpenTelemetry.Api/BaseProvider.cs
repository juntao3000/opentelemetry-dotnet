// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>
/// 包含所有 OpenTelemetry 提供程序共享的逻辑。
/// </summary>
public abstract class BaseProvider : IDisposable // 抽象类，包含所有 OpenTelemetry 提供程序共享的逻辑
{
    /// <summary>
    /// 终结 <see cref="BaseProvider"/> 类的实例。
    /// </summary>
    ~BaseProvider() // 析构函数，在垃圾回收时调用
    {
        this.Dispose(false); // 调用 Dispose 方法，释放非托管资源
    }

    /// <inheritdoc/>
    public void Dispose() // 实现 IDisposable 接口的 Dispose 方法
    {
        this.Dispose(true); // 调用 Dispose 方法，释放托管和非托管资源
        GC.SuppressFinalize(this); // 阻止垃圾回收器调用对象的终结器
    }

    /// <summary>
    /// 释放该类使用的非托管资源，并可选择性地释放托管资源。
    /// </summary>
    /// <param name="disposing"><see langword="true"/> 释放托管和非托管资源；<see langword="false"/> 仅释放非托管资源。</param>
    protected virtual void Dispose(bool disposing) // 受保护的虚方法，释放资源
    {
    }
}
