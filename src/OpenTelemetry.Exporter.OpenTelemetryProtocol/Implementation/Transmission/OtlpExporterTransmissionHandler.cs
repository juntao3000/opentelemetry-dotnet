// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

// OtlpExporterTransmissionHandler类用于处理OTLP导出器的传输逻辑
internal class OtlpExporterTransmissionHandler<TRequest> : IDisposable
{
    // 构造函数，初始化导出客户端和超时时间
    public OtlpExporterTransmissionHandler(IExportClient<TRequest> exportClient, double timeoutMilliseconds)
    {
        Guard.ThrowIfNull(exportClient); // 检查exportClient是否为null

        this.ExportClient = exportClient; // 导出客户端
        this.TimeoutMilliseconds = timeoutMilliseconds; // 超时时间（毫秒）
    }

    internal IExportClient<TRequest> ExportClient { get; } // 导出客户端

    internal double TimeoutMilliseconds { get; } // 超时时间（毫秒）

    /// <summary>
    /// 尝试向服务器发送导出请求。
    /// </summary>
    /// <param name="request">要发送到服务器的请求。</param>
    /// <returns> 如果请求成功发送，则返回<see langword="true" />；否则，返回<see langword="false" />。</returns>
    public bool TrySubmitRequest(TRequest request)
    {
        try
        {
            var deadlineUtc = DateTime.UtcNow.AddMilliseconds(this.TimeoutMilliseconds); // 计算请求的截止时间
            var response = this.ExportClient.SendExportRequest(request, deadlineUtc); // 发送导出请求
            if (response.Success)
            {
                return true; // 请求成功
            }

            return this.OnSubmitRequestFailure(request, response); // 请求失败，调用失败处理方法
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.TrySubmitRequestException(ex); // 记录异常
            return false; // 请求失败
        }
    }

    /// <summary>
    /// 尝试关闭传输处理程序，阻塞当前线程直到关闭完成或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或<c>Timeout.Infinite</c>表示无限等待。
    /// </param>
    /// <returns>
    /// 如果关闭成功，则返回<see langword="true" />；否则，返回<see langword="false" />。
    /// </returns>
    public bool Shutdown(int timeoutMilliseconds)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds); // 检查超时时间是否有效

        var sw = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.StartNew(); // 启动计时器

        this.OnShutdown(timeoutMilliseconds); // 调用关闭处理方法

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
    /// 等待的毫秒数（非负），或<c>Timeout.Infinite</c>表示无限等待。
    /// </param>
    protected virtual void OnShutdown(int timeoutMilliseconds)
    {
    }

    /// <summary>
    /// 请求无法提交时触发。
    /// </summary>
    /// <param name="request">尝试发送到服务器的请求。</param>
    /// <param name="response"><see cref="ExportClientResponse" />。</param>
    /// <returns>如果请求重新提交并成功，则返回<see langword="true" />；否则，返回<see langword="false" />。</returns>
    protected virtual bool OnSubmitRequestFailure(TRequest request, ExportClientResponse response)
    {
        return false; // 默认处理失败
    }

    /// <summary>
    /// 重新发送请求到服务器时触发。
    /// </summary>
    /// <param name="request">要重新发送到服务器的请求。</param>
    /// <param name="deadlineUtc">导出请求完成的截止时间（UTC）。</param>
    /// <param name="response"><see cref="ExportClientResponse" />。</param>
    /// <returns>如果重试成功，则返回<see langword="true" />；否则，返回<see langword="false" />。</returns>
    protected bool TryRetryRequest(TRequest request, DateTime deadlineUtc, out ExportClientResponse response)
    {
        response = this.ExportClient.SendExportRequest(request, deadlineUtc); // 重新发送导出请求
        if (!response.Success)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(response.Exception, isRetry: true); // 记录异常
            return false; // 重试失败
        }

        return true; // 重试成功
    }

    /// <summary>
    /// 释放此类使用的非托管资源，并可选地释放托管资源。
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/>表示释放托管和非托管资源；<see langword="false"/>表示仅释放非托管资源。
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
