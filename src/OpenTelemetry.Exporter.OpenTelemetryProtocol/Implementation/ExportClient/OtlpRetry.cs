// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Google.Rpc;
using Grpc.Core;
using Status = Google.Rpc.Status;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>
/// OTLP重试策略的实现，适用于OTLP/gRPC和OTLP/HTTP。
///
/// OTLP/gRPC
/// https://github.com/open-telemetry/opentelemetry-proto/blob/main/docs/specification.md#failures
///
/// OTLP/HTTP
/// https://github.com/open-telemetry/opentelemetry-proto/blob/main/docs/specification.md#failures-1
///
/// 规范要求重试使用指数退避策略，但没有提供具体的实现细节。因此，这个实现借鉴了
/// Grpc.Net.Client提供的重试策略，该策略实现了gRPC重试规范。
///
/// Grpc.Net.Client重试实现
/// https://github.com/grpc/grpc-dotnet/blob/83d12ea1cb628156c990243bc98699829b88738b/src/Grpc.Net.Client/Internal/Retry/RetryCall.cs#L94
///
/// gRPC重试规范
/// https://github.com/grpc/proposal/blob/master/A6-client-retries.md
///
/// gRPC重试规范概述了其指数退避策略中使用的可配置参数：初始退避时间、最大退避时间、退避倍数和最大重试次数。
/// OTLP规范没有声明类似的参数，因此这个实现使用固定设置。此外，由于OTLP规范没有指定最大尝试次数，
/// 这个实现将重试直到达到截止时间。
///
/// OTLP的限流机制与gRPC重试规范中描述的限流机制不同。见：
/// https://github.com/open-telemetry/opentelemetry-proto/blob/main/docs/specification.md#otlpgrpc-throttling。
/// </summary>
internal static class OtlpRetry // OTLP重试策略的实现类
{
    public const string GrpcStatusDetailsHeader = "grpc-status-details-bin"; // gRPC状态详情头
    public const int InitialBackoffMilliseconds = 1000; // 初始退避时间（毫秒）
    private const int MaxBackoffMilliseconds = 5000; // 最大退避时间（毫秒）
    private const double BackoffMultiplier = 1.5; // 退避倍数

#if !NET
    private static readonly Random Random = new Random(); // 随机数生成器
#endif

    // 尝试获取HTTP重试结果
    public static bool TryGetHttpRetryResult(ExportClientHttpResponse response, int retryDelayInMilliSeconds, out RetryResult retryResult)
    {
        if (response.StatusCode.HasValue)
        {
            return TryGetRetryResult(response.StatusCode.Value, IsHttpStatusCodeRetryable, response.DeadlineUtc, response.Headers, TryGetHttpRetryDelay, retryDelayInMilliSeconds, out retryResult);
        }
        else
        {
            if (ShouldHandleHttpRequestException(response.Exception))
            {
                var delay = TimeSpan.FromMilliseconds(GetRandomNumber(0, retryDelayInMilliSeconds));
                if (!IsDeadlineExceeded(response.DeadlineUtc + delay))
                {
                    retryResult = new RetryResult(false, delay, CalculateNextRetryDelay(retryDelayInMilliSeconds));
                    return true;
                }
            }

            retryResult = default;
            return false;
        }
    }

    // 判断是否应处理HttpRequestException
    public static bool ShouldHandleHttpRequestException(Exception? exception)
    {
        // TODO: 处理特定异常。
        return true;
    }

    // 尝试获取gRPC重试结果
    public static bool TryGetGrpcRetryResult(ExportClientGrpcResponse response, int retryDelayMilliseconds, out RetryResult retryResult)
    {
        if (response.Exception is RpcException rpcException)
        {
            return TryGetRetryResult(rpcException.StatusCode, IsGrpcStatusCodeRetryable, response.DeadlineUtc, rpcException.Trailers, TryGetGrpcRetryDelay, retryDelayMilliseconds, out retryResult);
        }

        retryResult = default;
        return false;
    }

    // 尝试获取重试结果
    private static bool TryGetRetryResult<TStatusCode, TCarrier>(TStatusCode statusCode, Func<TStatusCode, bool, bool> isRetryable, DateTime? deadline, TCarrier carrier, Func<TStatusCode, TCarrier, TimeSpan?> throttleGetter, int nextRetryDelayMilliseconds, out RetryResult retryResult)
    {
        retryResult = default;

        // TODO: 考虑引入固定的最大重试次数（例如最多5次重试）。
        // 规范没有指定最大重试次数，但不限制尝试次数可能会带来不好的影响。
        // 如果没有最大重试次数限制，重试将持续到截止时间。
        // 这可能是可以的？但是，这可能会导致意外的行为变化。例如：
        //    1) 使用批处理器时，由于重复重试导致的更长延迟可能会导致队列耗尽的可能性更高。
        //    2) 使用简单处理器时，由于重复重试导致的更长延迟将导致阻塞调用时间延长。
        // if (attemptCount >= MaxAttempts)
        // {
        //     return false
        // }

        if (IsDeadlineExceeded(deadline))
        {
            return false;
        }

        var throttleDelay = throttleGetter(statusCode, carrier);
        var retryable = isRetryable(statusCode, throttleDelay.HasValue);
        if (!retryable)
        {
            return false;
        }

        var delayDuration = throttleDelay.HasValue
            ? throttleDelay.Value
            : TimeSpan.FromMilliseconds(GetRandomNumber(0, nextRetryDelayMilliseconds));

        if (deadline.HasValue && IsDeadlineExceeded(deadline + delayDuration))
        {
            return false;
        }

        if (throttleDelay.HasValue)
        {
            try
            {
                // TODO: 考虑将nextRetryDelayMilliseconds改为double以避免转换/溢出处理
                nextRetryDelayMilliseconds = Convert.ToInt32(throttleDelay.Value.TotalMilliseconds);
            }
            catch (OverflowException)
            {
                nextRetryDelayMilliseconds = MaxBackoffMilliseconds;
            }
        }

        nextRetryDelayMilliseconds = CalculateNextRetryDelay(nextRetryDelayMilliseconds);
        retryResult = new RetryResult(throttleDelay.HasValue, delayDuration, nextRetryDelayMilliseconds);
        return true;
    }

    // 判断是否超过截止时间
    private static bool IsDeadlineExceeded(DateTime? deadline)
    {
        // 这个实现是内部的，保证deadline是UTC时间。
        return deadline.HasValue && deadline <= DateTime.UtcNow;
    }

    // 计算下一个重试延迟
    private static int CalculateNextRetryDelay(int nextRetryDelayMilliseconds)
    {
        var nextMilliseconds = nextRetryDelayMilliseconds * BackoffMultiplier;
        nextMilliseconds = Math.Min(nextMilliseconds, MaxBackoffMilliseconds);
        return Convert.ToInt32(nextMilliseconds);
    }

    // 尝试获取gRPC重试延迟
    private static TimeSpan? TryGetGrpcRetryDelay(StatusCode statusCode, Metadata trailers)
    {
        Debug.Assert(trailers != null, "trailers was null");

        if (statusCode != StatusCode.ResourceExhausted && statusCode != StatusCode.Unavailable)
        {
            return null;
        }

        var statusDetails = trailers!.Get(GrpcStatusDetailsHeader);
        if (statusDetails != null && statusDetails.IsBinary)
        {
            var status = Status.Parser.ParseFrom(statusDetails.ValueBytes);
            foreach (var item in status.Details)
            {
                var success = item.TryUnpack<RetryInfo>(out var retryInfo);
                if (success)
                {
                    return retryInfo.RetryDelay.ToTimeSpan();
                }
            }
        }

        return null;
    }

    // 尝试获取HTTP重试延迟
    private static TimeSpan? TryGetHttpRetryDelay(HttpStatusCode statusCode, HttpResponseHeaders? responseHeaders)
    {
#if NETSTANDARD2_1_OR_GREATER || NET
        return statusCode == HttpStatusCode.TooManyRequests || statusCode == HttpStatusCode.ServiceUnavailable
#else
        return statusCode == (HttpStatusCode)429 || statusCode == HttpStatusCode.ServiceUnavailable
#endif
            ? responseHeaders?.RetryAfter?.Delta
            : null;
    }

    // 判断gRPC状态码是否可重试
    private static bool IsGrpcStatusCodeRetryable(StatusCode statusCode, bool hasRetryDelay)
    {
        switch (statusCode)
        {
            case StatusCode.Cancelled:
            case StatusCode.DeadlineExceeded:
            case StatusCode.Aborted:
            case StatusCode.OutOfRange:
            case StatusCode.Unavailable:
            case StatusCode.DataLoss:
                return true;
            case StatusCode.ResourceExhausted:
                return hasRetryDelay;
            default:
                return false;
        }
    }

    // 判断HTTP状态码是否可重试
    private static bool IsHttpStatusCodeRetryable(HttpStatusCode statusCode, bool hasRetryDelay)
    {
        switch (statusCode)
        {
#if NETSTANDARD2_1_OR_GREATER || NET
            case HttpStatusCode.TooManyRequests:
#else
            case (HttpStatusCode)429:
#endif
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
                return true;
            default:
                return false;
        }
    }

    // 获取随机数
    private static int GetRandomNumber(int min, int max)
    {
#if NET
        return Random.Shared.Next(min, max);
#else
        // TODO: 更好地实现这一点以最小化锁争用。
        // 考虑引入Random.Shared实现。
        lock (Random)
        {
            return Random.Next(min, max);
        }
#endif
    }

    // 重试结果结构体
    public readonly struct RetryResult
    {
        public readonly bool Throttled; // 是否限流
        public readonly TimeSpan RetryDelay; // 重试延迟
        public readonly int NextRetryDelayMilliseconds; // 下一个重试延迟（毫秒）

        public RetryResult(bool throttled, TimeSpan retryDelay, int nextRetryDelayMilliseconds)
        {
            this.Throttled = throttled;
            this.RetryDelay = retryDelay;
            this.NextRetryDelayMilliseconds = nextRetryDelayMilliseconds;
        }
    }
}
