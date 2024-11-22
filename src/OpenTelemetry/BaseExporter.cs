// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// 枚举，用于定义导出操作的结果。
/// </summary>
public enum ExportResult
{
    /// <summary>
    /// 导出成功。
    /// </summary>
    Success = 0,

    /// <summary>
    /// 导出失败。
    /// </summary>
    Failure = 1,
}

/// <summary>
/// 导出器基类。
/// </summary>
/// <typeparam name="T">要导出的对象类型。</typeparam>
public abstract class BaseExporter<T> : IDisposable
    where T : class
{
    // 关闭计数
    private int shutdownCount;

    /// <summary>
    /// 获取父 <see cref="BaseProvider"/>。
    /// </summary>
    public BaseProvider? ParentProvider { get; internal set; }

    /// <summary>
    /// 导出一批遥测对象。
    /// </summary>
    /// <param name="batch">要导出的遥测对象批次。</param>
    /// <returns>导出操作的结果。</returns>
    public abstract ExportResult Export(in Batch<T> batch);

    /// <summary>
    /// 刷新导出器，阻塞当前线程直到刷新完成、关闭信号或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或 <c>Timeout.Infinite</c> 表示无限等待。
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
            return this.OnForceFlush(timeoutMilliseconds);
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.ForceFlush), ex);
            return false;
        }
    }

    /// <summary>
    /// 尝试关闭导出器，阻塞当前线程直到关闭完成或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或 <c>Timeout.Infinite</c> 表示无限等待。
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

        if (Interlocked.Increment(ref this.shutdownCount) > 1)
        {
            return false; // 已经调用过关闭
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

    /// <summary>
    /// 由 <c>ForceFlush</c> 调用。此函数应阻塞当前线程直到刷新完成、关闭信号或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或 <c>Timeout.Infinite</c> 表示无限等待。
    /// </param>
    /// <returns>
    /// 刷新成功时返回 <c>true</c>；否则返回 <c>false</c>。
    /// </returns>
    /// <remarks>
    /// 此函数在调用 <c>ForceFlush</c> 的线程上同步调用。此函数应是线程安全的，并且不应抛出异常。
    /// </remarks>
    protected virtual bool OnForceFlush(int timeoutMilliseconds)
    {
        return true;
    }

    /// <summary>
    /// 由 <c>Shutdown</c> 调用。此函数应阻塞当前线程直到关闭完成或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或 <c>Timeout.Infinite</c> 表示无限等待。
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
    /// 释放此类使用的非托管资源，并可选地释放托管资源。
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> 表示释放托管和非托管资源；
    /// <see langword="false"/> 表示仅释放非托管资源。
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
