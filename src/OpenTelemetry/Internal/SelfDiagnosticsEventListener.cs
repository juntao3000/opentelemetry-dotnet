// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.Text;

namespace OpenTelemetry.Internal;

/// <summary>
/// SelfDiagnosticsEventListener 类启用来自 OpenTelemetry 事件源的事件
/// 并以循环方式将事件写入本地文件。
/// </summary>
internal sealed class SelfDiagnosticsEventListener : EventListener
{
    // 日志行的缓冲区大小。C# 中的 UTF-16 编码字符在 UTF-8 编码中最多可以占用 4 个字节。
    private const int BUFFERSIZE = 4 * 5120;
    // 事件源名称前缀
    private const string EventSourceNamePrefix = "OpenTelemetry-";
    // 锁对象
    private readonly Lock lockObj = new();
    // 日志级别
    private readonly EventLevel logLevel;
    // 配置刷新器
    private readonly SelfDiagnosticsConfigRefresher configRefresher;
    // 写缓冲区
    private readonly ThreadLocal<byte[]?> writeBuffer = new(() => null);
    // 构造函数之前的事件源列表
    private readonly List<EventSource>? eventSourcesBeforeConstructor = new();

    // 释放标志
    private bool disposedValue = false;

    /// <summary>
    /// SelfDiagnosticsEventListener 构造函数
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <param name="configRefresher">配置刷新器</param>
    public SelfDiagnosticsEventListener(EventLevel logLevel, SelfDiagnosticsConfigRefresher configRefresher)
    {
        Guard.ThrowIfNull(configRefresher);

        this.logLevel = logLevel;
        this.configRefresher = configRefresher;

        List<EventSource> eventSources;
        lock (this.lockObj)
        {
            eventSources = this.eventSourcesBeforeConstructor;
            this.eventSourcesBeforeConstructor = null;
        }

        foreach (var eventSource in eventSources)
        {
            this.EnableEvents(eventSource, this.logLevel, EventKeywords.All);
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        this.Dispose(true);
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 将字符串编码到字节缓冲区的指定位置，该缓冲区将作为日志写入。
    /// 如果 isParameter 为 true，则在字符串周围加上 "{}"。
    /// 缓冲区不应填满，至少留一个字节的空位以便稍后填充 '\n'。
    /// 如果缓冲区无法容纳所有字符，则截断字符串并用 "..." 替换多余的内容。
    /// 由于 UTF-8 的可变编码长度，缓冲区不保证填满最后一个字节，以优先考虑速度而非空间。
    /// </summary>
    /// <param name="str">要编码的字符串。</param>
    /// <param name="isParameter">字符串是否为参数。如果为 true，则在字符串周围加上 "{}"。</param>
    /// <param name="buffer">包含结果字节序列的字节数组。</param>
    /// <param name="position">开始写入结果字节序列的位置。</param>
    /// <returns>缓冲区中最后一个结果字节之后的位置。</returns>
    internal static int EncodeInBuffer(string? str, bool isParameter, byte[] buffer, int position)
    {
        if (string.IsNullOrEmpty(str))
        {
            return position;
        }

        int charCount = str!.Length;
        int ellipses = isParameter ? "{...}\n".Length : "...\n".Length;

        // 确保有空间放置 "{...}\n" 或 "...\n"。
        if (buffer.Length - position - ellipses < 0)
        {
            return position;
        }

        int estimateOfCharacters = (buffer.Length - position - ellipses) / 2;

        // 确保 UTF-16 编码的字符串可以适应缓冲区的 UTF-8 编码。
        // 并为 "{...}\n" 或 "...\n" 留出空间。
        if (charCount > estimateOfCharacters)
        {
            charCount = estimateOfCharacters;
        }

        if (isParameter)
        {
            buffer[position++] = (byte)'{';
        }

        position += Encoding.UTF8.GetBytes(str, 0, charCount, buffer, position);
        if (charCount != str.Length)
        {
            buffer[position++] = (byte)'.';
            buffer[position++] = (byte)'.';
            buffer[position++] = (byte)'.';
        }

        if (isParameter)
        {
            buffer[position++] = (byte)'}';
        }

        return position;
    }

    /// <summary>
    /// 写入事件
    /// </summary>
    /// <param name="eventMessage">事件消息</param>
    /// <param name="payload">负载</param>
    internal void WriteEvent(string? eventMessage, ReadOnlyCollection<object?>? payload)
    {
        try
        {
            var buffer = this.writeBuffer.Value;
            if (buffer == null)
            {
                buffer = new byte[BUFFERSIZE];
                this.writeBuffer.Value = buffer;
            }

            var pos = this.DateTimeGetBytes(DateTime.UtcNow, buffer, 0);
            buffer[pos++] = (byte)':';
            pos = EncodeInBuffer(eventMessage, false, buffer, pos);
            if (payload != null)
            {
                // 不使用 foreach 因为它会导致分配
                for (int i = 0; i < payload.Count; ++i)
                {
                    object? obj = payload[i];
                    if (obj != null)
                    {
                        pos = EncodeInBuffer(obj.ToString() ?? "null", true, buffer, pos);
                    }
                    else
                    {
                        pos = EncodeInBuffer("null", true, buffer, pos);
                    }
                }
            }

            buffer[pos++] = (byte)'\n';
            int byteCount = pos - 0;
            if (this.configRefresher.TryGetLogStream(byteCount, out Stream? stream, out int availableByteCount))
            {
                if (availableByteCount >= byteCount)
                {
                    stream.Write(buffer, 0, byteCount);
                }
                else
                {
                    stream.Write(buffer, 0, availableByteCount);
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.Write(buffer, availableByteCount, byteCount - availableByteCount);
                }
            }
        }
        catch (Exception)
        {
            // 无法为缓冲区分配内存，或
            // 并发条件：在 TryGetLogStream() 完成后，内存映射文件在其他线程中被释放。
            // 在这种情况下，静默失败。
        }
    }

    /// <summary>
    /// 将 <c>datetime</c> 格式化字符串写入 <c>bytes</c> 字节数组，从 <c>byteIndex</c> 位置开始。
    /// <para>
    /// [DateTimeKind.Utc]
    /// 格式: yyyy - MM - dd T HH : mm : ss . fffffff Z (例如 2020-12-09T10:20:50.4659412Z)。
    /// </para>
    /// <para>
    /// [DateTimeKind.Local]
    /// 格式: yyyy - MM - dd T HH : mm : ss . fffffff +|- HH : mm (例如 2020-12-09T10:20:50.4659412-08:00)。
    /// </para>
    /// <para>
    /// [DateTimeKind.Unspecified]
    /// 格式: yyyy - MM - dd T HH : mm : ss . fffffff (例如 2020-12-09T10:20:50.4659412)。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 字节数组必须足够大，从 byteIndex 开始写入 27-33 个字符。
    /// </remarks>
    /// <param name="datetime">DateTime。</param>
    /// <param name="bytes">要写入的字节数组。</param>
    /// <param name="byteIndex">字节数组的起始索引。</param>
    /// <returns>写入的字节数。</returns>
    internal int DateTimeGetBytes(DateTime datetime, byte[] bytes, int byteIndex)
    {
        int num;
        int pos = byteIndex;

        num = datetime.Year;
        bytes[pos++] = (byte)('0' + ((num / 1000) % 10));
        bytes[pos++] = (byte)('0' + ((num / 100) % 10));
        bytes[pos++] = (byte)('0' + ((num / 10) % 10));
        bytes[pos++] = (byte)('0' + (num % 10));

        bytes[pos++] = (byte)'-';

        num = datetime.Month;
        bytes[pos++] = (byte)('0' + ((num / 10) % 10));
        bytes[pos++] = (byte)('0' + (num % 10));

        bytes[pos++] = (byte)'-';

        num = datetime.Day;
        bytes[pos++] = (byte)('0' + ((num / 10) % 10));
        bytes[pos++] = (byte)('0' + (num % 10));

        bytes[pos++] = (byte)'T';

        num = datetime.Hour;
        bytes[pos++] = (byte)('0' + ((num / 10) % 10));
        bytes[pos++] = (byte)('0' + (num % 10));

        bytes[pos++] = (byte)':';

        num = datetime.Minute;
        bytes[pos++] = (byte)('0' + ((num / 10) % 10));
        bytes[pos++] = (byte)('0' + (num % 10));

        bytes[pos++] = (byte)':';

        num = datetime.Second;
        bytes[pos++] = (byte)('0' + ((num / 10) % 10));
        bytes[pos++] = (byte)('0' + (num % 10));

        bytes[pos++] = (byte)'.';

        num = (int)(Math.Round(datetime.TimeOfDay.TotalMilliseconds * 10000) % 10000000);
        bytes[pos++] = (byte)('0' + ((num / 1000000) % 10));
        bytes[pos++] = (byte)('0' + ((num / 100000) % 10));
        bytes[pos++] = (byte)('0' + ((num / 10000) % 10));
        bytes[pos++] = (byte)('0' + ((num / 1000) % 10));
        bytes[pos++] = (byte)('0' + ((num / 100) % 10));
        bytes[pos++] = (byte)('0' + ((num / 10) % 10));
        bytes[pos++] = (byte)('0' + (num % 10));

        switch (datetime.Kind)
        {
            case DateTimeKind.Utc:
                bytes[pos++] = (byte)'Z';
                break;

            case DateTimeKind.Local:
                TimeSpan ts = TimeZoneInfo.Local.GetUtcOffset(datetime);

                bytes[pos++] = (byte)(ts.Hours >= 0 ? '+' : '-');

                num = Math.Abs(ts.Hours);
                bytes[pos++] = (byte)('0' + ((num / 10) % 10));
                bytes[pos++] = (byte)('0' + (num % 10));

                bytes[pos++] = (byte)':';

                num = ts.Minutes;
                bytes[pos++] = (byte)('0' + ((num / 10) % 10));
                bytes[pos++] = (byte)('0' + (num % 10));
                break;

            case DateTimeKind.Unspecified:
            default:
                // 跳过
                break;
        }

        return pos - byteIndex;
    }

    /// <summary>
    /// 当事件源创建时调用
    /// </summary>
    /// <param name="eventSource">事件源</param>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name.StartsWith(EventSourceNamePrefix, StringComparison.Ordinal))
        {
            // 如果现在已经初始化了 EventSource 类，则此方法将在 SelfDiagnosticsEventListener 构造函数中的第一行代码之前从基类构造函数调用。
            // 在这种情况下，logLevel 始终是其默认值 "LogAlways"。
            // 因此，我们应该保存事件源并在构造函数中运行代码时启用它们。
            if (this.eventSourcesBeforeConstructor != null)
            {
                lock (this.lockObj)
                {
                    if (this.eventSourcesBeforeConstructor != null)
                    {
                        this.eventSourcesBeforeConstructor.Add(eventSource);
                        return;
                    }
                }
            }

            this.EnableEvents(eventSource, this.logLevel, EventKeywords.All);
        }

        base.OnEventSourceCreated(eventSource);
    }

    /// <summary>
    /// 此方法将事件源的事件记录到本地文件，该文件由 SelfDiagnosticsConfigRefresher 类提供为流对象。文件大小有上限。一旦写入位置达到文件末尾，它将重置为文件的开头。
    /// </summary>
    /// <param name="eventData">EventSource 事件的数据。</param>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        // 注意：此处的 EventSource 检查解决了 EventListener 中的一个错误。
        // 参见：https://github.com/open-telemetry/opentelemetry-dotnet/pull/5046
        if (eventData.EventSource.Name.StartsWith(EventSourceNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            this.WriteEvent(eventData.Message, eventData.Payload);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    /// <param name="disposing">是否释放托管资源</param>
    private void Dispose(bool disposing)
    {
        if (this.disposedValue)
        {
            return;
        }

        if (disposing)
        {
            this.writeBuffer.Dispose();
        }

        this.disposedValue = true;
    }
}
