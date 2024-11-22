// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

// ProtobufOtlpExporterTransmissionHandler类用于处理Protobuf格式的OTLP导出传输
internal class ProtobufOtlpExporterTransmissionHandler : IDisposable
{
    // 构造函数，初始化导出客户端和超时时间
    public ProtobufOtlpExporterTransmissionHandler(IProtobufExportClient exportClient, double timeoutMilliseconds)
    {
        Guard.ThrowIfNull(exportClient);

        this.ExportClient = exportClient; // 导出客户端
        this.TimeoutMilliseconds = timeoutMilliseconds; // 超时时间（毫秒）
    }

    internal IProtobufExportClient ExportClient { get; } // 导出客户端

    internal double TimeoutMilliseconds { get; } // 超时时间（毫秒）

    /// <summary>
    /// 尝试向服务器发送导出请求。
    /// </summary>
    /// <param name="request">要发送到服务器的请求。</param>
    /// <param name="contentLength">内容长度。</param>
    /// <returns>如果请求发送成功，则返回<see langword="true" />；否则，返回<see langword="false" />。</returns>
    public bool TrySubmitRequest(byte[] request, int contentLength)
    {
        try
        {
            var deadlineUtc = DateTime.UtcNow.AddMilliseconds(this.TimeoutMilliseconds); // 计算请求的截止时间
            var response = this.ExportClient.SendExportRequest(request, contentLength, deadlineUtc); // 发送导出请求
            if (response.Success)
            {
                return true; // 请求成功
            }

            return this.OnSubmitRequestFailure(request, contentLength, response); // 请求失败处理
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.TrySubmitRequestException(ex); // 记录异常
            return false;
        }
    }

    /// <summary>
    /// 尝试关闭传输处理程序，阻塞当前线程直到关闭完成或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负数），或<c>Timeout.Infinite</c>表示无限等待。
    /// </param>
    /// <returns>
    /// 如果关闭成功，则返回<see langword="true" />；否则，返回<see langword="false" />。
    /// </returns>
    public bool Shutdown(int timeoutMilliseconds)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        var sw = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.StartNew(); // 启动计时器

        this.OnShutdown(timeoutMilliseconds); // 执行关闭操作

        if (sw != null)
        {
            var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds; // 计算剩余时间

            return this.ExportClient.Shutdown((int)Math.Max(timeout, 0)); // 关闭导出客户端
        }

        return this.ExportClient.Shutdown(timeoutMilliseconds); // 关闭导出客户端
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true); // 释放资源
        GC.SuppressFinalize(this); // 阻止终结器调用
    }

    /// <summary>
    /// 传输处理程序关闭时触发。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负数），或<c>Timeout.Infinite</c>表示无限等待。
    /// </param>
    protected virtual void OnShutdown(int timeoutMilliseconds)
    {
    }

    /// <summary>
    /// 请求无法提交时触发。
    /// </summary>
    /// <param name="request">尝试发送到服务器的请求。</param>
    /// <param name="contentLength">内容长度。</param>
    /// <param name="response"><see cref="ExportClientResponse" />。</param>
    /// <returns>如果请求重新提交并成功，则返回<see langword="true" />；否则，返回<see langword="false" />。</returns>
    protected virtual bool OnSubmitRequestFailure(byte[] request, int contentLength, ExportClientResponse response) => false;

    /// <summary>
    /// 重新发送请求到服务器时触发。
    /// </summary>
    /// <param name="request">要重新发送到服务器的请求。</param>
    /// <param name="contentLength">内容长度。</param>
    /// <param name="deadlineUtc">导出请求完成的截止时间（UTC）。</param>
    /// <param name="response"><see cref="ExportClientResponse" />。</param>
    /// <returns>如果重试成功，则返回<see langword="true" />；否则，返回<see langword="false" />。</returns>
    protected bool TryRetryRequest(byte[] request, int contentLength, DateTime deadlineUtc, out ExportClientResponse response)
    {
        response = this.ExportClient.SendExportRequest(request, contentLength, deadlineUtc); // 重新发送请求
        if (!response.Success)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(response.Exception, isRetry: true); // 记录异常
            return false;
        }

        return true;
    }

    /// <summary>
    /// 释放该类使用的非托管资源，并可选地释放托管资源。
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/>表示释放托管和非托管资源；
    /// <see langword="false"/>表示仅释放非托管资源。
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
