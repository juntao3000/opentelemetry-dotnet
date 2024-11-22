// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// 实现导出遥测对象的处理器。
/// </summary>
/// <typeparam name="T">要导出的遥测对象的类型。</typeparam>
public abstract class BatchExportProcessor<T> : BaseExportProcessor<T>
    where T : class
{
    // 默认最大队列大小
    internal const int DefaultMaxQueueSize = 2048;
    // 默认计划延迟时间（毫秒）
    internal const int DefaultScheduledDelayMilliseconds = 5000;
    // 默认导出超时时间（毫秒）
    internal const int DefaultExporterTimeoutMilliseconds = 30000;
    // 默认最大导出批次大小
    internal const int DefaultMaxExportBatchSize = 512;

    // 最大导出批次大小
    internal readonly int MaxExportBatchSize;
    // 计划延迟时间（毫秒）
    internal readonly int ScheduledDelayMilliseconds;
    // 导出超时时间（毫秒）
    internal readonly int ExporterTimeoutMilliseconds;

    // 环形缓冲区
    private readonly CircularBuffer<T> circularBuffer;
    // 导出线程
    private readonly Thread exporterThread;
    // 导出触发器
    private readonly AutoResetEvent exportTrigger = new(false);
    // 数据导出通知
    private readonly ManualResetEvent dataExportedNotification = new(false);
    // 关闭触发器
    private readonly ManualResetEvent shutdownTrigger = new(false);
    // 关闭排空目标
    private long shutdownDrainTarget = long.MaxValue;
    // 丢弃计数
    private long droppedCount;
    // 是否已释放
    private bool disposed;

    /// <summary>
    /// 初始化 <see cref="BatchExportProcessor{T}"/> 类的新实例。
    /// </summary>
    /// <param name="exporter">导出器实例。</param>
    /// <param name="maxQueueSize">最大队列大小。达到此大小后数据将被丢弃。默认值为 2048。</param>
    /// <param name="scheduledDelayMilliseconds">两次连续导出之间的延迟间隔（毫秒）。默认值为 5000。</param>
    /// <param name="exporterTimeoutMilliseconds">导出运行的最长时间（毫秒），超过此时间将被取消。默认值为 30000。</param>
    /// <param name="maxExportBatchSize">每次导出的最大批次大小。必须小于或等于 maxQueueSize。默认值为 512。</param>
    protected BatchExportProcessor(
        BaseExporter<T> exporter,
        int maxQueueSize = DefaultMaxQueueSize,
        int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds,
        int maxExportBatchSize = DefaultMaxExportBatchSize)
        : base(exporter)
    {
        Guard.ThrowIfOutOfRange(maxQueueSize, min: 1);
        Guard.ThrowIfOutOfRange(maxExportBatchSize, min: 1, max: maxQueueSize, maxName: nameof(maxQueueSize));
        Guard.ThrowIfOutOfRange(scheduledDelayMilliseconds, min: 1);
        Guard.ThrowIfOutOfRange(exporterTimeoutMilliseconds, min: 0);

        this.circularBuffer = new CircularBuffer<T>(maxQueueSize);
        this.ScheduledDelayMilliseconds = scheduledDelayMilliseconds;
        this.ExporterTimeoutMilliseconds = exporterTimeoutMilliseconds;
        this.MaxExportBatchSize = maxExportBatchSize;
        this.exporterThread = new Thread(this.ExporterProc)
        {
            IsBackground = true,
            Name = $"OpenTelemetry-{nameof(BatchExportProcessor<T>)}-{exporter.GetType().Name}",
        };
        this.exporterThread.Start();
    }

    /// <summary>
    /// 获取处理器丢弃的遥测对象数量。
    /// </summary>
    internal long DroppedCount => Volatile.Read(ref this.droppedCount);

    /// <summary>
    /// 获取处理器接收的遥测对象数量。
    /// </summary>
    internal long ReceivedCount => this.circularBuffer.AddedCount + this.DroppedCount;

    /// <summary>
    /// 获取底层导出器处理的遥测对象数量。
    /// </summary>
    internal long ProcessedCount => this.circularBuffer.RemovedCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryExport(T data)
    {
        if (this.circularBuffer.TryAdd(data, maxSpinCount: 50000))
        {
            if (this.circularBuffer.Count >= this.MaxExportBatchSize)
            {
                try
                {
                    this.exportTrigger.Set();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            return true; // 入队成功
        }

        // 队列已满或超过自旋限制，丢弃该项
        Interlocked.Increment(ref this.droppedCount);

        return false;
    }

    /// <inheritdoc/>
    protected override void OnExport(T data)
    {
        this.TryExport(data);
    }

    /// <inheritdoc/>
    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        var tail = this.circularBuffer.RemovedCount;
        var head = this.circularBuffer.AddedCount;

        if (head == tail)
        {
            return true; // 无需刷新
        }

        try
        {
            this.exportTrigger.Set();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        if (timeoutMilliseconds == 0)
        {
            return false;
        }

        var triggers = new WaitHandle[] { this.dataExportedNotification, this.shutdownTrigger };

        var sw = timeoutMilliseconds == Timeout.Infinite
            ? null
            : Stopwatch.StartNew();

        // 存在导出线程已处理完队列中所有数据并发出信号的可能性，
        // 使用轮询防止无限期阻塞。
        const int pollingMilliseconds = 1000;

        while (true)
        {
            if (sw == null)
            {
                try
                {
                    WaitHandle.WaitAny(triggers, pollingMilliseconds);
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }
            else
            {
                var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                if (timeout <= 0)
                {
                    return this.circularBuffer.RemovedCount >= head;
                }

                try
                {
                    WaitHandle.WaitAny(triggers, Math.Min((int)timeout, pollingMilliseconds));
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }

            if (this.circularBuffer.RemovedCount >= head)
            {
                return true;
            }

            if (Volatile.Read(ref this.shutdownDrainTarget) != long.MaxValue)
            {
                return false;
            }
        }
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        Volatile.Write(ref this.shutdownDrainTarget, this.circularBuffer.AddedCount);

        try
        {
            this.shutdownTrigger.Set();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        OpenTelemetrySdkEventSource.Log.DroppedExportProcessorItems(this.GetType().Name, this.exporter.GetType().Name, this.DroppedCount);

        if (timeoutMilliseconds == Timeout.Infinite)
        {
            this.exporterThread.Join();
            return this.exporter.Shutdown();
        }

        if (timeoutMilliseconds == 0)
        {
            return this.exporter.Shutdown(0);
        }

        var sw = Stopwatch.StartNew();
        this.exporterThread.Join(timeoutMilliseconds);
        var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;
        return this.exporter.Shutdown((int)Math.Max(timeout, 0));
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.exportTrigger.Dispose();
                this.dataExportedNotification.Dispose();
                this.shutdownTrigger.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    private void ExporterProc()
    {
        var triggers = new WaitHandle[] { this.exportTrigger, this.shutdownTrigger };

        while (true)
        {
            // 仅在队列中没有足够的项时等待，否则保持忙碌并连续发送数据
            if (this.circularBuffer.Count < this.MaxExportBatchSize)
            {
                try
                {
                    WaitHandle.WaitAny(triggers, this.ScheduledDelayMilliseconds);
                }
                catch (ObjectDisposedException)
                {
                    // 导出器在工作线程完成其工作之前已被释放
                    return;
                }
            }

            if (this.circularBuffer.Count > 0)
            {
                using (var batch = new Batch<T>(this.circularBuffer, this.MaxExportBatchSize))
                {
                    this.exporter.Export(batch);
                }

                try
                {
                    this.dataExportedNotification.Set();
                    this.dataExportedNotification.Reset();
                }
                catch (ObjectDisposedException)
                {
                    // 导出器在工作线程完成其工作之前已被释放
                    return;
                }
            }

            if (this.circularBuffer.RemovedCount >= Volatile.Read(ref this.shutdownDrainTarget))
            {
                return;
            }
        }
    }
}
