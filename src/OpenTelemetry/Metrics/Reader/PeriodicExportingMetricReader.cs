// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// MetricReader 实现类，根据用户配置的时间间隔收集度量，并将度量传递给配置的 MetricExporter。
/// </summary>
public class PeriodicExportingMetricReader : BaseExportingMetricReader
{
    // 默认导出间隔（毫秒）
    internal const int DefaultExportIntervalMilliseconds = 60000;
    // 默认导出超时时间（毫秒）
    internal const int DefaultExportTimeoutMilliseconds = 30000;

    // 导出间隔（毫秒）
    internal readonly int ExportIntervalMilliseconds;
    // 导出超时时间（毫秒）
    internal readonly int ExportTimeoutMilliseconds;
    // 导出线程
    private readonly Thread exporterThread;
    // 导出触发器
    private readonly AutoResetEvent exportTrigger = new(false);
    // 关闭触发器
    private readonly ManualResetEvent shutdownTrigger = new(false);
    // 是否已释放
    private bool disposed;

    /// <summary>
    /// 初始化 <see cref="PeriodicExportingMetricReader"/> 类的新实例。
    /// </summary>
    /// <param name="exporter">用于导出度量的导出器实例。</param>
    /// <param name="exportIntervalMilliseconds">两次连续导出之间的间隔（毫秒）。默认值为 60000。</param>
    /// <param name="exportTimeoutMilliseconds">导出运行的最长时间（毫秒）。默认值为 30000。</param>
    public PeriodicExportingMetricReader(
        BaseExporter<Metric> exporter,
        int exportIntervalMilliseconds = DefaultExportIntervalMilliseconds,
        int exportTimeoutMilliseconds = DefaultExportTimeoutMilliseconds)
        : base(exporter)
    {
        Guard.ThrowIfInvalidTimeout(exportIntervalMilliseconds);
        Guard.ThrowIfZero(exportIntervalMilliseconds);
        Guard.ThrowIfInvalidTimeout(exportTimeoutMilliseconds);

        if ((this.SupportedExportModes & ExportModes.Push) != ExportModes.Push)
        {
            throw new InvalidOperationException($"The '{nameof(exporter)}' does not support '{nameof(ExportModes)}.{nameof(ExportModes.Push)}'");
        }

        this.ExportIntervalMilliseconds = exportIntervalMilliseconds;
        this.ExportTimeoutMilliseconds = exportTimeoutMilliseconds;

        this.exporterThread = new Thread(new ThreadStart(this.ExporterProc))
        {
            IsBackground = true,
            Name = $"OpenTelemetry-{nameof(PeriodicExportingMetricReader)}-{exporter.GetType().Name}",
        };
        this.exporterThread.Start();
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var result = true;

        try
        {
            this.shutdownTrigger.Set();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        if (timeoutMilliseconds == Timeout.Infinite)
        {
            this.exporterThread.Join();
            result = this.exporter.Shutdown() && result;
        }
        else
        {
            var sw = Stopwatch.StartNew();
            result = this.exporterThread.Join(timeoutMilliseconds) && result;
            var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;
            result = this.exporter.Shutdown((int)Math.Max(timeout, 0)) && result;
        }

        return result;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.exportTrigger.Dispose();
                this.shutdownTrigger.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    // 导出处理过程
    private void ExporterProc()
    {
        int index;
        int timeout;
        var triggers = new WaitHandle[] { this.exportTrigger, this.shutdownTrigger };
        var sw = Stopwatch.StartNew();

        while (true)
        {
            timeout = (int)(this.ExportIntervalMilliseconds - (sw.ElapsedMilliseconds % this.ExportIntervalMilliseconds));

            try
            {
                index = WaitHandle.WaitAny(triggers, timeout);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            switch (index)
            {
                case 0: // 导出
                    OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader 调用 MetricReader.Collect 因为导出被触发。");
                    this.Collect(this.ExportTimeoutMilliseconds);
                    break;
                case 1: // 关闭
                    OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader 调用 MetricReader.Collect 因为关闭被触发。");
                    this.Collect(this.ExportTimeoutMilliseconds); // TODO: 我们是否希望在这里使用关闭超时？
                    return;
                case WaitHandle.WaitTimeout: // 定时器
                    OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader 调用 MetricReader.Collect 因为导出间隔已过。");
                    this.Collect(this.ExportTimeoutMilliseconds);
                    break;
            }
        }
    }
}
