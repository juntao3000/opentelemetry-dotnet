// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// 实现处理器，在每次 OnEnd 调用时导出遥测数据。
/// </summary>
/// <typeparam name="T">要导出的遥测对象的类型。</typeparam>
public abstract class SimpleExportProcessor<T> : BaseExportProcessor<T>
    where T : class
{
    // 同步对象，用于锁定导出操作
    private readonly Lock syncObject = new();

    /// <summary>
    /// 初始化 <see cref="SimpleExportProcessor{T}"/> 类的新实例。
    /// </summary>
    /// <param name="exporter">导出器实例。</param>
    protected SimpleExportProcessor(BaseExporter<T> exporter)
        : base(exporter)
    {
    }

    /// <inheritdoc />
    /// <summary>
    /// 导出遥测数据。
    /// </summary>
    /// <param name="data">要导出的遥测数据。</param>
    protected override void OnExport(T data)
    {
        // 锁定同步对象，确保线程安全
        lock (this.syncObject)
        {
            try
            {
                // 导出遥测数据
                this.exporter.Export(new Batch<T>(data));
            }
            catch (Exception ex)
            {
                // 记录导出异常
                OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.OnExport), ex);
            }
        }
    }
}
