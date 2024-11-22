// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

// OtlpExporterRetryTransmissionHandler类用于处理OTLP导出器的重试传输逻辑
internal sealed class OtlpExporterRetryTransmissionHandler<TRequest> : OtlpExporterTransmissionHandler<TRequest>
{
    // 构造函数，初始化导出客户端和超时时间
    internal OtlpExporterRetryTransmissionHandler(IExportClient<TRequest> exportClient, double timeoutMilliseconds)
        : base(exportClient, timeoutMilliseconds)
    {
    }

    // 当请求提交失败时触发
    protected override bool OnSubmitRequestFailure(TRequest request, ExportClientResponse response)
    {
        // 下次重试的延迟时间（毫秒）
        var nextRetryDelayMilliseconds = OtlpRetry.InitialBackoffMilliseconds;
        // 当需要重试请求时
        while (RetryHelper.ShouldRetryRequest(response, nextRetryDelayMilliseconds, out var retryResult))
        {
            // 注意：此延迟不能超过为otlp导出器配置的超时时间。
            // 如果后端响应的`RetryAfter`持续时间会导致超过配置的超时时间
            // 我们会快速失败并丢弃数据。
            Thread.Sleep(retryResult.RetryDelay);

            // 尝试重新发送请求
            if (this.TryRetryRequest(request, response.DeadlineUtc, out response))
            {
                return true;
            }

            // 更新下次重试的延迟时间（毫秒）
            nextRetryDelayMilliseconds = retryResult.NextRetryDelayMilliseconds;
        }

        return false;
    }
}
