// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

// RetryHelper类：用于帮助判断请求是否需要重试
internal static class RetryHelper
{
    // ShouldRetryRequest方法：根据响应和重试延迟判断请求是否需要重试
    internal static bool ShouldRetryRequest(ExportClientResponse response, int retryDelayMilliseconds, out OtlpRetry.RetryResult retryResult)
    {
        // 如果响应是ExportClientGrpcResponse类型
        if (response is ExportClientGrpcResponse grpcResponse)
        {
            // 尝试获取gRPC重试结果
            if (OtlpRetry.TryGetGrpcRetryResult(grpcResponse, retryDelayMilliseconds, out retryResult))
            {
                return true; // 如果获取成功，则需要重试
            }
        }
        // 如果响应是ExportClientHttpResponse类型
        else if (response is ExportClientHttpResponse httpResponse)
        {
            // 尝试获取HTTP重试结果
            if (OtlpRetry.TryGetHttpRetryResult(httpResponse, retryDelayMilliseconds, out retryResult))
            {
                return true; // 如果获取成功，则需要重试
            }
        }

        retryResult = default; // 默认重试结果
        return false; // 不需要重试
    }
}
