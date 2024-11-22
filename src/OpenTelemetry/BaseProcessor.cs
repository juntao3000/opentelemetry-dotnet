// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// 基础处理器基类。
/// </summary>
/// <typeparam name="T">要处理的对象类型。</typeparam>
public abstract class BaseProcessor<T> : IDisposable
{
    // 处理器类型名称
    private readonly string typeName;
    // 关闭计数
    private int shutdownCount;

    /// <summary>
    /// 初始化 <see cref="BaseProcessor{T}"/> 类的新实例。
    /// </summary>
    public BaseProcessor()
    {
        this.typeName = this.GetType().Name;
    }

    /// <summary>
    /// 获取父 <see cref="BaseProvider"/>。
    /// </summary>
    public BaseProvider? ParentProvider { get; private set; }

    /// <summary>
    /// 获取或设置处理器在添加到提供程序管道时的权重。默认值：<c>0</c>。
    /// </summary>
    /// <remarks>
    /// 注意：权重用于在构建提供程序管道时对处理器进行排序。权重较低的处理器排在权重较高的处理器之前。在构建管道后更改权重没有效果。
    /// </remarks>
    internal int PipelineWeight { get; set; }

    /// <summary>
    /// 当一个遥测对象开始时同步调用。
    /// </summary>
    /// <param name="data">
    /// 开始的遥测对象。
    /// </param>
    /// <remarks>
    /// 此函数在启动遥测对象的线程上同步调用。此函数应是线程安全的，不应无限期阻塞或抛出异常。
    /// </remarks>
    public virtual void OnStart(T data)
    {
    }

    /// <summary>
    /// 当一个遥测对象结束时同步调用。
    /// </summary>
    /// <param name="data">
    /// 结束的遥测对象。
    /// </param>
    /// <remarks>
    /// 此函数在结束遥测对象的线程上同步调用。此函数应是线程安全的，不应无限期阻塞或抛出异常。
    /// </remarks>
    public virtual void OnEnd(T data)
    {
    }

    /// <summary>
    /// 刷新处理器，阻塞当前线程直到刷新完成、关闭信号或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或 <c>Timeout.Infinite</c> 表示无限期等待。
    /// </param>
    /// <returns>
    /// 刷新成功时返回 <c>true</c>；否则返回 <c>false</c>。
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 当 <c>timeoutMilliseconds</c> 小于 -1 时抛出。
    /// </exception>
    /// <remarks>
    /// 此函数保证线程安全。
    /// </remarks>
    public bool ForceFlush(int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        try
        {
            bool result = this.OnForceFlush(timeoutMilliseconds);

            OpenTelemetrySdkEventSource.Log.ProcessorForceFlushInvoked(this.typeName, result);

            return result;
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.ForceFlush), ex);
            return false;
        }
    }

    /// <summary>
    /// 尝试关闭处理器，阻塞当前线程直到关闭完成或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或 <c>Timeout.Infinite</c> 表示无限期等待。
    /// </param>
    /// <returns>
    /// 关闭成功时返回 <c>true</c>；否则返回 <c>false</c>。
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 当 <c>timeoutMilliseconds</c> 小于 -1 时抛出。
    /// </exception>
    /// <remarks>
    /// 此函数保证线程安全。只有第一次调用会生效，后续调用将无效。
    /// </remarks>
    public bool Shutdown(int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        if (Interlocked.CompareExchange(ref this.shutdownCount, 1, 0) != 0)
        {
            return false; // shutdown already called
        }

        try
        {
            return this.OnShutdown(timeoutMilliseconds);
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Shutdown), ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public override string ToString()
        => this.typeName;

    internal virtual void SetParentProvider(BaseProvider parentProvider)
    {
        this.ParentProvider = parentProvider;
    }

    /// <summary>
    /// 由 <c>ForceFlush</c> 调用。此函数应阻塞当前线程直到刷新完成、关闭信号或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或 <c>Timeout.Infinite</c> 表示无限期等待。
    /// </param>
    /// <returns>
    /// 刷新成功时返回 <c>true</c>；否则返回 <c>false</c>。
    /// </returns>
    /// <remarks>
    /// 此函数在调用 <c>ForceFlush</c> 的线程上同步调用。此函数应是线程安全的，不应抛出异常。
    /// </remarks>
    protected virtual bool OnForceFlush(int timeoutMilliseconds)
    {
        return true;
    }

    /// <summary>
    /// 由 <c>Shutdown</c> 调用。此函数应阻塞当前线程直到关闭完成或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或 <c>Timeout.Infinite</c> 表示无限期等待。
    /// </param>
    /// <returns>
    /// 关闭成功时返回 <c>true</c>；否则返回 <c>false</c>。
    /// </returns>
    /// <remarks>
    /// 此函数在第一次调用 <c>Shutdown</c> 的线程上同步调用。此函数不应抛出异常。
    /// </remarks>
    protected virtual bool OnShutdown(int timeoutMilliseconds)
    {
        return true;
    }

    /// <summary>
    /// 释放该类使用的非托管资源，并可选择性地释放托管资源。
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> 释放托管和非托管资源；<see langword="false"/> 仅释放非托管资源。
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
