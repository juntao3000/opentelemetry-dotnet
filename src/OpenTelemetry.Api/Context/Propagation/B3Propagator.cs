// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// B3 的文本映射传播器。参见 https://github.com/openzipkin/b3-propagation。
/// 此类已弃用，建议使用 OpenTelemetry.Extensions.Propagators 包中的 B3Propagator 类。
/// </summary>
[Obsolete("Use B3Propagator class from OpenTelemetry.Extensions.Propagators namespace, shipped as part of OpenTelemetry.Extensions.Propagators package.")]
public sealed class B3Propagator : TextMapPropagator // B3Propagator 类：用于 B3 传播的文本映射传播器
{
    // 定义 B3 传播所需的常量
    internal const string XB3TraceId = "X-B3-TraceId"; // B3 TraceId 头
    internal const string XB3SpanId = "X-B3-SpanId"; // B3 SpanId 头
    internal const string XB3ParentSpanId = "X-B3-ParentSpanId"; // B3 ParentSpanId 头
    internal const string XB3Sampled = "X-B3-Sampled"; // B3 Sampled 头
    internal const string XB3Flags = "X-B3-Flags"; // B3 Flags 头
    internal const string XB3Combined = "b3"; // B3 组合头
    internal const char XB3CombinedDelimiter = '-'; // B3 组合头分隔符

    // 用作 traceID 的高位 ActivityTraceId.SIZE 十六进制字符。B3 传播过去曾发送 ActivityTraceId.SIZE 十六进制字符（8 字节 traceId）。
    internal const string UpperTraceId = "0000000000000000";

    // 通过 X_B3_SAMPLED 头采样的值。
    internal const string SampledValue = "1";

    // 一些旧的 zipkin 实现可能会为采样头发送 true/false。仅用于检查传入值。
    internal const string LegacySampledValue = "true";

    // "Debug" 采样值。
    internal const string FlagsValue = "1";

    // 定义所有 B3 头字段的集合
    private static readonly HashSet<string> AllFields = new() { XB3TraceId, XB3SpanId, XB3ParentSpanId, XB3Sampled, XB3Flags };

    // 定义采样值的集合
    private static readonly HashSet<string> SampledValues = new(StringComparer.Ordinal) { SampledValue, LegacySampledValue };

    private readonly bool singleHeader; // 是否使用单一头

    /// <summary>
    /// 初始化 <see cref="B3Propagator"/> 类的新实例。
    /// </summary>
    [Obsolete("Use B3Propagator class from OpenTelemetry.Extensions.Propagators namespace, shipped as part of OpenTelemetry.Extensions.Propagators package.")]
    public B3Propagator()
        : this(false)
    {
    }

    /// <summary>
    /// 初始化 <see cref="B3Propagator"/> 类的新实例。
    /// </summary>
    /// <param name="singleHeader">确定在提取或注入 span 上下文时是否使用单个或多个头。</param>
    [Obsolete("Use B3Propagator class from OpenTelemetry.Extensions.Propagators namespace, shipped as part of OpenTelemetry.Extensions.Propagators package.")]
    public B3Propagator(bool singleHeader)
    {
        this.singleHeader = singleHeader;
    }

    /// <inheritdoc/>
    public override ISet<string> Fields => AllFields;

    /// <inheritdoc/>
    [Obsolete("Use B3Propagator class from OpenTelemetry.Extensions.Propagators namespace, shipped as part of OpenTelemetry.Extensions.Propagators package.")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    {
        if (context.ActivityContext.IsValid())
        {
            // 如果已提取到有效的上下文，则执行 noop。
            return context;
        }

        if (carrier == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToExtractActivityContext(nameof(B3Propagator), "null carrier");
            return context;
        }

        if (getter == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToExtractActivityContext(nameof(B3Propagator), "null getter");
            return context;
        }

        if (this.singleHeader)
        {
            return ExtractFromSingleHeader(context, carrier, getter);
        }
        else
        {
            return ExtractFromMultipleHeaders(context, carrier, getter);
        }
    }

    /// <inheritdoc/>
    [Obsolete("Use B3Propagator class from OpenTelemetry.Extensions.Propagators namespace, shipped as part of OpenTelemetry.Extensions.Propagators package.")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    {
        if (context.ActivityContext.TraceId == default || context.ActivityContext.SpanId == default)
        {
            OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext(nameof(B3Propagator), "invalid context");
            return;
        }

        if (carrier == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext(nameof(B3Propagator), "null carrier");
            return;
        }

        if (setter == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext(nameof(B3Propagator), "null setter");
            return;
        }

        if (this.singleHeader)
        {
            var sb = new StringBuilder();
            sb.Append(context.ActivityContext.TraceId.ToHexString());
            sb.Append(XB3CombinedDelimiter);
            sb.Append(context.ActivityContext.SpanId.ToHexString());
            if ((context.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0)
            {
                sb.Append(XB3CombinedDelimiter);
                sb.Append(SampledValue);
            }

            setter(carrier, XB3Combined, sb.ToString());
        }
        else
        {
            setter(carrier, XB3TraceId, context.ActivityContext.TraceId.ToHexString());
            setter(carrier, XB3SpanId, context.ActivityContext.SpanId.ToHexString());
            if ((context.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0)
            {
                setter(carrier, XB3Sampled, SampledValue);
            }
        }
    }

    private static PropagationContext ExtractFromMultipleHeaders<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
    {
        try
        {
            ActivityTraceId traceId;
            var traceIdStr = getter(carrier, XB3TraceId)?.FirstOrDefault();
            if (traceIdStr != null)
            {
                if (traceIdStr.Length == 16)
                {
                    // 这是一个 8 字节的 traceID。
                    traceIdStr = UpperTraceId + traceIdStr;
                }

                traceId = ActivityTraceId.CreateFromString(traceIdStr.AsSpan());
            }
            else
            {
                return context;
            }

            ActivitySpanId spanId;
            var spanIdStr = getter(carrier, XB3SpanId)?.FirstOrDefault();
            if (spanIdStr != null)
            {
                spanId = ActivitySpanId.CreateFromString(spanIdStr.AsSpan());
            }
            else
            {
                return context;
            }

            var traceOptions = ActivityTraceFlags.None;
            var xb3Sampled = getter(carrier, XB3Sampled)?.FirstOrDefault();
            if ((xb3Sampled != null && SampledValues.Contains(xb3Sampled))
                || FlagsValue.Equals(getter(carrier, XB3Flags)?.FirstOrDefault(), StringComparison.Ordinal))
            {
                traceOptions |= ActivityTraceFlags.Recorded;
            }

            return new PropagationContext(
                new ActivityContext(traceId, spanId, traceOptions, isRemote: true),
                context.Baggage);
        }
        catch (Exception e)
        {
            OpenTelemetryApiEventSource.Log.ActivityContextExtractException(nameof(B3Propagator), e);
            return context;
        }
    }

    private static PropagationContext ExtractFromSingleHeader<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
    {
        try
        {
            var header = getter(carrier, XB3Combined)?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(header))
            {
                return context;
            }

            var parts = header!.Split(XB3CombinedDelimiter);
            if (parts.Length < 2 || parts.Length > 4)
            {
                return context;
            }

            var traceIdStr = parts[0];
            if (string.IsNullOrWhiteSpace(traceIdStr))
            {
                return context;
            }

            if (traceIdStr.Length == 16)
            {
                // 这是一个 8 字节的 traceID。
                traceIdStr = UpperTraceId + traceIdStr;
            }

            var traceId = ActivityTraceId.CreateFromString(traceIdStr.AsSpan());

            var spanIdStr = parts[1];
            if (string.IsNullOrWhiteSpace(spanIdStr))
            {
                return context;
            }

            var spanId = ActivitySpanId.CreateFromString(spanIdStr.AsSpan());

            var traceOptions = ActivityTraceFlags.None;
            if (parts.Length > 2)
            {
                var traceFlagsStr = parts[2];
                if (SampledValues.Contains(traceFlagsStr)
                    || FlagsValue.Equals(traceFlagsStr, StringComparison.Ordinal))
                {
                    traceOptions |= ActivityTraceFlags.Recorded;
                }
            }

            return new PropagationContext(
                new ActivityContext(traceId, spanId, traceOptions, isRemote: true),
                context.Baggage);
        }
        catch (Exception e)
        {
            OpenTelemetryApiEventSource.Log.ActivityContextExtractException(nameof(B3Propagator), e);
            return context;
        }
    }
}
