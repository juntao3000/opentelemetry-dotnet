// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Internal;

/// <summary>
/// OpenTelemetry API 的 EventSource 实现。
/// 这是用于该库的内部日志记录。
/// </summary>
[EventSource(Name = "OpenTelemetry-Api")]
internal sealed class OpenTelemetryApiEventSource : EventSource
{
    // 定义一个静态实例，用于记录日志
    public static OpenTelemetryApiEventSource Log = new();

    // 记录提取 ActivityContext 失败的异常
    [NonEvent]
    public void ActivityContextExtractException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.FailedToExtractActivityContext(format, ex.ToInvariantString());
        }
    }

    // 记录提取 Baggage 失败的异常
    [NonEvent]
    public void BaggageExtractException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.FailedToExtractBaggage(format, ex.ToInvariantString());
        }
    }

    // 记录提取 Tracestate 失败的异常
    [NonEvent]
    public void TracestateExtractException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.TracestateExtractError(ex.ToInvariantString());
        }
    }

    // 记录 Tracestate 的 key 无效
    [NonEvent]
    public void TracestateKeyIsInvalid(ReadOnlySpan<char> key)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.TracestateKeyIsInvalid(key.ToString());
        }
    }

    // 记录 Tracestate 的 value 无效
    [NonEvent]
    public void TracestateValueIsInvalid(ReadOnlySpan<char> value)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.TracestateValueIsInvalid(value.ToString());
        }
    }

    // 记录 Tracestate 项目过多的警告
    [Event(3, Message = "Failed to parse tracestate: too many items", Level = EventLevel.Warning)]
    public void TooManyItemsInTracestate()
    {
        this.WriteEvent(3);
    }

    // 记录 Tracestate key 无效的警告
    [Event(4, Message = "Tracestate key is invalid, key = '{0}'", Level = EventLevel.Warning)]
    public void TracestateKeyIsInvalid(string key)
    {
        this.WriteEvent(4, key);
    }

    // 记录 Tracestate value 无效的警告
    [Event(5, Message = "Tracestate value is invalid, value = '{0}'", Level = EventLevel.Warning)]
    public void TracestateValueIsInvalid(string value)
    {
        this.WriteEvent(5, value);
    }

    // 记录 Tracestate 解析错误的警告
    [Event(6, Message = "Tracestate parse error: '{0}'", Level = EventLevel.Warning)]
    public void TracestateExtractError(string error)
    {
        this.WriteEvent(6, error);
    }

    // 记录提取 ActivityContext 失败的警告
    [Event(8, Message = "Failed to extract activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
    public void FailedToExtractActivityContext(string format, string exception)
    {
        this.WriteEvent(8, format, exception);
    }

    // 记录注入 ActivityContext 失败的警告
    [Event(9, Message = "Failed to inject activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
    public void FailedToInjectActivityContext(string format, string error)
    {
        this.WriteEvent(9, format, error);
    }

    // 记录提取 Baggage 失败的警告
    [Event(10, Message = "Failed to extract baggage in format: '{0}', baggage: '{1}'.", Level = EventLevel.Warning)]
    public void FailedToExtractBaggage(string format, string exception)
    {
        this.WriteEvent(10, format, exception);
    }

    // 记录注入 Baggage 失败的警告
    [Event(11, Message = "Failed to inject baggage in format: '{0}', baggage: '{1}'.", Level = EventLevel.Warning)]
    public void FailedToInjectBaggage(string format, string error)
    {
        this.WriteEvent(11, format, error);
    }
}
