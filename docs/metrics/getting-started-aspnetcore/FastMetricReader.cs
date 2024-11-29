// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using System.Diagnostics.Tracing;

namespace AspNetCoreMetrics;


/// <summary>
/// 快捷 Metric 读取器
/// </summary>
public class FastMetricReader : BaseExportingMetricReader, IFastMetricReader
{
    internal const int DefaultExportIntervalMilliseconds = 1000;
    internal const int DefaultExportTimeoutMilliseconds = 30000;

    internal readonly int ExportIntervalMilliseconds;
    internal readonly int ExportTimeoutMilliseconds;

    private readonly Thread? exporterThread;
    private readonly AutoResetEvent exportTrigger = new(false);
    private readonly ManualResetEvent shutdownTrigger = new(false);
    private bool disposed;

    /// <summary>
    /// 初始化快捷 Metric 读取器
    /// </summary>
    /// <param name="exporter">Metric 导出器</param>
    /// <param name="exportIntervalMilliseconds">读取 Metric 的间隔(毫秒)，零表示不定时读取导出而手动调用 Collect 读取导出</param>
    /// <param name="exportTimeoutMilliseconds">导出 Metric 的超时值(毫秒)</param>
    public FastMetricReader(
        BaseExporter<Metric> exporter,
        int exportIntervalMilliseconds = DefaultExportIntervalMilliseconds,
        int exportTimeoutMilliseconds = DefaultExportTimeoutMilliseconds)
        : base(exporter)
    {
        if (exportIntervalMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exportIntervalMilliseconds));
        }

        if (exportTimeoutMilliseconds != Timeout.Infinite && exportTimeoutMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exportTimeoutMilliseconds));
        }

        if ((this.SupportedExportModes & ExportModes.Push) != ExportModes.Push)
        {
            throw new InvalidOperationException($"The '{nameof(exporter)}' does not support '{nameof(ExportModes)}.{nameof(ExportModes.Push)}'");
        }

        this.ExportIntervalMilliseconds = exportIntervalMilliseconds;
        this.ExportTimeoutMilliseconds = exportTimeoutMilliseconds;

        if (exportIntervalMilliseconds > 0)
        {
            this.exporterThread = new Thread(new ThreadStart(this.ExporterProc))
            {
                IsBackground = true,
                Name = $"OpenTelemetry-{nameof(FastMetricReader)}-{exporter.GetType().Name}",
            };
            this.exporterThread.Start();
        }
    }

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
            this.exporterThread?.Join();
            result = this.exporter.Shutdown() && result;
        }
        else
        {
            var sw = Stopwatch.StartNew();
            result = (this.exporterThread?.Join(timeoutMilliseconds) ?? true) && result;
            var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;
            result = this.exporter.Shutdown((int)Math.Max(timeout, 0)) && result;
        }

        return result;
    }

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
                case 0: // export
                    FastMetricReaderEventSource.Log.MetricReaderEvent("FastMetricReader calling MetricReader.Collect because Export was triggered.");
                    this.Collect(this.ExportTimeoutMilliseconds);
                    break;
                case 1: // shutdown
                    FastMetricReaderEventSource.Log.MetricReaderEvent("FastMetricReader calling MetricReader.Collect because Shutdown was triggered.");
                    this.Collect(this.ExportTimeoutMilliseconds); // TODO: do we want to use the shutdown timeout here?
                    return;
                case WaitHandle.WaitTimeout: // timer
                    FastMetricReaderEventSource.Log.MetricReaderEvent("FastMetricReader calling MetricReader.Collect because the export interval has elapsed.");
                    this.Collect(this.ExportTimeoutMilliseconds);
                    break;
            }
        }
    }
}

[EventSource(Name = "OpenTelemetry-Sdk-FastMetricReader")]
internal class FastMetricReaderEventSource : EventSource
{
    public static FastMetricReaderEventSource Log = new();

    [Event(40, Message = "MetricReader event: '{0}'", Level = EventLevel.Verbose)]
    public void MetricReaderEvent(string message)
    {
        WriteEvent(40, message);
    }
}
