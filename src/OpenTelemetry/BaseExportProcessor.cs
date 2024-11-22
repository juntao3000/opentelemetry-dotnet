// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// 要使用的导出处理器类型。
/// </summary>
public enum ExportProcessorType
{
    /// <summary>
    /// 使用 SimpleExportProcessor。
    /// 有关详细信息，请参阅 <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#simple-processor">
    /// 规范</a>。
    /// </summary>
    Simple,

    /// <summary>
    /// 使用 BatchExportProcessor。
    /// 有关详细信息，请参阅 <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#batching-processor">
    /// 规范</a>。
    /// </summary>
    Batch,
}

/// <summary>
/// 实现导出遥测对象的处理器。
/// </summary>
/// <typeparam name="T">要导出的遥测对象的类型。</typeparam>
public abstract class BaseExportProcessor<T> : BaseProcessor<T>
    where T : class
{
    /// <summary>
    /// 获取处理器使用的导出器。
    /// </summary>
    protected readonly BaseExporter<T> exporter;

    // 友好类型名称
    private readonly string friendlyTypeName;
    // 是否已释放
    private bool disposed;

    /// <summary>
    /// 初始化 <see cref="BaseExportProcessor{T}"/> 类的新实例。
    /// </summary>
    /// <param name="exporter">导出器实例。</param>
    protected BaseExportProcessor(BaseExporter<T> exporter)
    {
        // 检查导出器是否为 null
        Guard.ThrowIfNull(exporter);

        // 设置友好类型名称
        this.friendlyTypeName = $"{this.GetType().Name}{{{exporter.GetType().Name}}}";
        // 设置导出器
        this.exporter = exporter;
    }

    // 获取导出器
    internal BaseExporter<T> Exporter => this.exporter;

    /// <inheritdoc />
    public override string ToString()
        => this.friendlyTypeName;

    /// <inheritdoc />
    public sealed override void OnStart(T data)
    {
        // 在开始时不执行任何操作
    }

    /// <inheritdoc />
    public override void OnEnd(T data)
    {
        // 在结束时调用导出方法
        this.OnExport(data);
    }

    internal override void SetParentProvider(BaseProvider parentProvider)
    {
        base.SetParentProvider(parentProvider);

        // 设置导出器的父提供程序
        this.exporter.ParentProvider = parentProvider;
    }

    /// <summary>
    /// 当导出遥测对象时同步调用。
    /// </summary>
    /// <param name="data">
    /// 导出的遥测对象。
    /// </param>
    /// <remarks>
    /// 此函数在结束遥测对象的线程上同步调用。此函数应是线程安全的，
    /// 不应无限期阻塞或抛出异常。
    /// </remarks>
    protected abstract void OnExport(T data);

    /// <inheritdoc />
    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        // 强制刷新导出器
        return this.exporter.ForceFlush(timeoutMilliseconds);
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        // 关闭导出器
        return this.exporter.Shutdown(timeoutMilliseconds);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                try
                {
                    // 释放导出器
                    this.exporter.Dispose();
                }
                catch (Exception ex)
                {
                    // 记录异常
                    OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                }
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }
}
