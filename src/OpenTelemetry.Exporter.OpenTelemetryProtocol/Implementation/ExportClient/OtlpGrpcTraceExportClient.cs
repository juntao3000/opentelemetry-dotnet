// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>
/// 通过 gRPC 发送 OTLP 跟踪导出请求的类。
/// </summary>
internal sealed class OtlpGrpcTraceExportClient : BaseOtlpGrpcExportClient<OtlpCollector.ExportTraceServiceRequest>
{
    // gRPC 跟踪服务客户端
    private readonly OtlpCollector.TraceService.TraceServiceClient traceClient;

    /// <summary>
    /// 构造函数，初始化 OtlpGrpcTraceExportClient 类的新实例。
    /// </summary>
    /// <param name="options">OTLP 导出选项。</param>
    /// <param name="traceServiceClient">可选的跟踪服务客户端。</param>
    public OtlpGrpcTraceExportClient(OtlpExporterOptions options, OtlpCollector.TraceService.TraceServiceClient? traceServiceClient = null)
        : base(options)
    {
        if (traceServiceClient != null)
        {
            this.traceClient = traceServiceClient;
        }
        else
        {
            // 创建 gRPC 通道
            this.Channel = options.CreateChannel();
            // 初始化跟踪服务客户端
            this.traceClient = new OtlpCollector.TraceService.TraceServiceClient(this.Channel);
        }
    }

    /// <inheritdoc/>
    /// <summary>
    /// 发送导出请求到服务器。
    /// </summary>
    /// <param name="request">要发送到服务器的请求。</param>
    /// <param name="deadlineUtc">导出请求完成的截止时间（UTC）。</param>
    /// <param name="cancellationToken">可选的取消令牌。</param>
    /// <returns>导出客户端响应。</returns>
    public override ExportClientResponse SendExportRequest(OtlpCollector.ExportTraceServiceRequest request, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            // 发送导出请求
            this.traceClient.Export(request, headers: this.Headers, deadline: deadlineUtc, cancellationToken: cancellationToken);

            // 对于成功的响应，我们不需要返回响应和截止时间，因此使用缓存值。
            return SuccessExportResponse;
        }
        catch (RpcException ex)
        {
            // 记录无法到达收集器的错误
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);

            // 返回失败的响应
            return new ExportClientGrpcResponse(success: false, deadlineUtc: deadlineUtc, exception: ex);
        }
    }
}
