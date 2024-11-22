// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO.MemoryMappedFiles;

namespace OpenTelemetry.Internal;

/// <summary>
/// SelfDiagnosticsConfigRefresher 类检查配置文件的位置，并在配置的文件路径上打开一个配置大小的 MemoryMappedFile。
/// 如果配置文件存在且有效，该类提供一个具有正确写入位置的流对象。否则，流对象将不可用，任何文件都不会记录日志。
/// </summary>
internal class SelfDiagnosticsConfigRefresher : IDisposable
{
    // 当创建新文件时写入的消息
    public static readonly byte[] MessageOnNewFile = "If you are seeing this message, it means that the OpenTelemetry SDK has successfully created the log file used to write self-diagnostic logs. This file will be appended with logs as they appear. If you do not see any logs following this line, it means no logs of the configured LogLevel is occurring. You may change the LogLevel to show lower log levels, so that logs of lower severities will be shown.\n"u8.ToArray();

    // 配置更新周期（毫秒）
    private const int ConfigurationUpdatePeriodMilliSeconds = 10000;

    // 取消令牌源
    private readonly CancellationTokenSource cancellationTokenSource;
    // 工作任务
    private readonly Task worker;
    // 配置解析器
    private readonly SelfDiagnosticsConfigParser configParser;

    /// <summary>
    /// memoryMappedFileCache 是一个保存在线程本地存储中的句柄，用作缓存，以指示缓存的 viewStream 是否是从当前的 m_memoryMappedFile 创建的。
    /// </summary>
    private readonly ThreadLocal<MemoryMappedFile> memoryMappedFileCache = new(true);
    private readonly ThreadLocal<MemoryMappedViewStream> viewStream = new(true);
    private bool disposedValue;

    // 一旦配置文件有效，将创建一个 eventListener 对象。
    private SelfDiagnosticsEventListener? eventListener;
    private volatile FileStream? underlyingFileStreamForMemoryMappedFile;
    private volatile MemoryMappedFile? memoryMappedFile;
    private string? logDirectory;  // 日志文件的日志目录
    private int logFileSize;  // 日志文件大小（字节）
    private long logFilePosition;  // 日志记录器将写入该位置的字节
    private EventLevel logEventLevel = (EventLevel)(-1);

    public SelfDiagnosticsConfigRefresher()
    {
        this.configParser = new SelfDiagnosticsConfigParser();
        this.UpdateMemoryMappedFileFromConfiguration();
        this.cancellationTokenSource = new CancellationTokenSource();
        this.worker = Task.Run(() => this.Worker(this.cancellationTokenSource.Token), this.cancellationTokenSource.Token);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 尝试获取日志流，该流已定位到应写入下一行日志的位置。
    /// </summary>
    /// <param name="byteCount">需要写入的字节数。</param>
    /// <param name="stream">当此方法返回时，包含可以写入 `byteCount` 字节的 Stream 对象。</param>
    /// <param name="availableByteCount">流结束前剩余的字节数。</param>
    /// <returns>记录器是否应在流中记录日志。</returns>
    public virtual bool TryGetLogStream(
        int byteCount,
        [NotNullWhen(true)]
        out Stream? stream,
        out int availableByteCount)
    {
        if (this.memoryMappedFile == null)
        {
            stream = null;
            availableByteCount = 0;
            return false;
        }

        try
        {
            var cachedViewStream = this.viewStream.Value;

            // 每个线程都有自己的 MemoryMappedViewStream，从唯一的 MemoryMappedFile 创建。
            // 一旦工作线程更新 MemoryMappedFile，所有缓存的 ViewStream 对象都将变为过时。
            // 每个线程在下次尝试检索时创建一个新的 MemoryMappedViewStream。
            // MemoryMappedViewStream 是否过时是通过将当前 MemoryMappedFile 对象与创建 MemoryMappedViewStream 时缓存的 MemoryMappedFile 对象进行比较来确定的。
            if (cachedViewStream == null || this.memoryMappedFileCache.Value != this.memoryMappedFile)
            {
                // 竞争条件：代码可能在工作线程将 memoryMappedFile 设置为 null 之后立即到达此处。
                // 在这种情况下，让 NullReferenceException 被捕获并静默失败。
                // 根据设计，在配置文件刷新期间捕获的所有事件都将被丢弃，无论文件是被删除还是更新。
                cachedViewStream = this.memoryMappedFile.CreateViewStream();
                this.viewStream.Value = cachedViewStream;
                this.memoryMappedFileCache.Value = this.memoryMappedFile;
            }

            long beginPosition, endPosition;
            do
            {
                beginPosition = this.logFilePosition;
                endPosition = beginPosition + byteCount;
                if (endPosition >= this.logFileSize)
                {
                    endPosition %= this.logFileSize;
                }
            }
            while (beginPosition != Interlocked.CompareExchange(ref this.logFilePosition, endPosition, beginPosition));
            availableByteCount = (int)(this.logFileSize - beginPosition);
            cachedViewStream.Seek(beginPosition, SeekOrigin.Begin);
            stream = cachedViewStream;
            return true;
        }
        catch (Exception)
        {
            stream = null;
            availableByteCount = 0;
            return false;
        }
    }

    private async Task Worker(CancellationToken cancellationToken)
    {
        await Task.Delay(ConfigurationUpdatePeriodMilliSeconds, cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            this.UpdateMemoryMappedFileFromConfiguration();
            await Task.Delay(ConfigurationUpdatePeriodMilliSeconds, cancellationToken).ConfigureAwait(false);
        }
    }

    private void UpdateMemoryMappedFileFromConfiguration()
    {
        if (this.configParser.TryGetConfiguration(out string? newLogDirectory, out int fileSizeInKB, out EventLevel newEventLevel))
        {
            int newFileSize = fileSizeInKB * 1024;
            if (!newLogDirectory.Equals(this.logDirectory) || this.logFileSize != newFileSize)
            {
                this.CloseLogFile();
                this.OpenLogFile(newLogDirectory, newFileSize);
            }

            if (!newEventLevel.Equals(this.logEventLevel))
            {
                if (this.eventListener != null)
                {
                    this.eventListener.Dispose();
                }

                this.eventListener = new SelfDiagnosticsEventListener(newEventLevel, this);
                this.logEventLevel = newEventLevel;
            }
        }
        else
        {
            this.CloseLogFile();
        }
    }

    private void CloseLogFile()
    {
        MemoryMappedFile? mmf = Interlocked.CompareExchange(ref this.memoryMappedFile, null, this.memoryMappedFile);
        if (mmf != null)
        {
            // 每个线程都有自己的 MemoryMappedViewStream，从唯一的 MemoryMappedFile 创建。
            // 一旦工作线程关闭 MemoryMappedFile，所有 ViewStream 对象都应正确处理。
            foreach (MemoryMappedViewStream stream in this.viewStream.Values)
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }

            mmf.Dispose();
        }

        FileStream? fs = Interlocked.CompareExchange(
            ref this.underlyingFileStreamForMemoryMappedFile,
            null,
            this.underlyingFileStreamForMemoryMappedFile);
        fs?.Dispose();
    }

    private void OpenLogFile(string newLogDirectory, int newFileSize)
    {
        try
        {
            Directory.CreateDirectory(newLogDirectory);
            var fileName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? "OpenTelemetrySdk") + "."
                + Process.GetCurrentProcess().Id + ".log";
            var filePath = Path.Combine(newLogDirectory, fileName);

            // 因为 MemoryMappedFile.CreateFromFile API（字符串版本）在 .NET Framework 和 .NET Core 上的行为不同，这里我使用 FileStream 版本。
            // 从 .NET Framework 和 .NET Core 中获取最后四个参数值。
            // FileAccess 参数类型不同但规则相同，都是读写。
            // FileShare 参数值和行为不同。 .NET Framework 不允许共享，但 .NET Core 允许其他程序读取。
            // 最后两个参数在两个框架中都是相同的值。
            this.underlyingFileStreamForMemoryMappedFile =
                new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 0x1000, FileOptions.None);

            // MemoryMappedFileSecurity、HandleInheritability 和 leaveOpen 的参数值在 .NET Framework 和 .NET Core 中相同。
            this.memoryMappedFile = MemoryMappedFile.CreateFromFile(
                this.underlyingFileStreamForMemoryMappedFile,
                null,
                newFileSize,
                MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None,
                false);
            this.logDirectory = newLogDirectory;
            this.logFileSize = newFileSize;
            this.logFilePosition = MessageOnNewFile.Length;
            using var stream = this.memoryMappedFile.CreateViewStream();
            stream.Write(MessageOnNewFile, 0, MessageOnNewFile.Length);
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.SelfDiagnosticsFileCreateException(newLogDirectory, ex);
        }
    }

    private void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this.cancellationTokenSource.Cancel(false);
                try
                {
                    this.worker.Wait();
                }
                catch (AggregateException)
                {
                }
                finally
                {
                    this.cancellationTokenSource.Dispose();
                }

                // 在文件之前处理 EventListener，因为 EventListener 写入文件。
                if (this.eventListener != null)
                {
                    this.eventListener.Dispose();
                }

                // 确保工作线程正确完成。否则它可能在调用下面的 CloseLogFile() 之后在该线程中创建另一个 MemoryMappedFile。
                this.CloseLogFile();

                // 在文件句柄处理后处理 ThreadLocal 变量。
                this.viewStream.Dispose();
                this.memoryMappedFileCache.Dispose();
            }

            this.disposedValue = true;
        }
    }
}
