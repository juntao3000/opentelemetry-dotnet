// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Google.Protobuf;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

// OTLP导出器持久化存储传输处理程序类
internal sealed class OtlpExporterPersistentStorageTransmissionHandler<TRequest> : OtlpExporterTransmissionHandler<TRequest>, IDisposable
{
    // 重试间隔时间（毫秒）
    private const int RetryIntervalInMilliseconds = 60000;
    // 关闭事件
    private readonly ManualResetEvent shutdownEvent = new(false);
    // 数据导出通知事件
    private readonly ManualResetEvent dataExportNotification = new(false);
    // 导出事件
    private readonly AutoResetEvent exportEvent = new(false);
    // 处理重试的线程
    private readonly Thread thread;
    // 持久化Blob提供者
    private readonly PersistentBlobProvider persistentBlobProvider;
    // 请求工厂方法
    private readonly Func<byte[], TRequest> requestFactory;
    // 是否已释放
    private bool disposed;

    // 构造函数，初始化导出客户端、超时时间、请求工厂和存储路径
    public OtlpExporterPersistentStorageTransmissionHandler(IExportClient<TRequest> exportClient, double timeoutMilliseconds, Func<byte[], TRequest> requestFactory, string storagePath)
        : this(new FileBlobProvider(storagePath), exportClient, timeoutMilliseconds, requestFactory)
    {
    }

    // 内部构造函数，初始化持久化Blob提供者、导出客户端、超时时间和请求工厂
    internal OtlpExporterPersistentStorageTransmissionHandler(PersistentBlobProvider persistentBlobProvider, IExportClient<TRequest> exportClient, double timeoutMilliseconds, Func<byte[], TRequest> requestFactory)
        : base(exportClient, timeoutMilliseconds)
    {
        Debug.Assert(persistentBlobProvider != null, "persistentBlobProvider was null");
        Debug.Assert(requestFactory != null, "requestFactory was null");

        this.persistentBlobProvider = persistentBlobProvider!;
        this.requestFactory = requestFactory!;

        this.thread = new Thread(this.RetryStoredRequests)
        {
            Name = $"OtlpExporter Persistent Retry Storage - {typeof(TRequest)}",
            IsBackground = true,
        };

        this.thread.Start();
    }

    // 用于测试，启动并等待重试过程
    internal bool InitiateAndWaitForRetryProcess(int timeOutMilliseconds)
    {
        this.exportEvent.Set();

        return this.dataExportNotification.WaitOne(timeOutMilliseconds);
    }

    // 提交请求失败时触发
    protected override bool OnSubmitRequestFailure(TRequest request, ExportClientResponse response)
    {
        if (RetryHelper.ShouldRetryRequest(response, OtlpRetry.InitialBackoffMilliseconds, out _))
        {
            byte[]? data = null;
            if (request is ExportTraceServiceRequest traceRequest)
            {
                data = traceRequest.ToByteArray();
            }
            else if (request is ExportMetricsServiceRequest metricsRequest)
            {
                data = metricsRequest.ToByteArray();
            }
            else if (request is ExportLogsServiceRequest logsRequest)
            {
                data = logsRequest.ToByteArray();
            }
            else
            {
                Debug.Fail("Unexpected request type encountered");
                data = null;
            }

            if (data != null)
            {
                return this.persistentBlobProvider.TryCreateBlob(data, out _);
            }
        }

        return false;
    }

    // 关闭传输处理程序时触发
    protected override void OnShutdown(int timeoutMilliseconds)
    {
        var sw = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.StartNew();

        try
        {
            this.shutdownEvent.Set();
        }
        catch (ObjectDisposedException)
        {
            // Dispose was called before shutdown.
        }

        this.thread.Join(timeoutMilliseconds);

        if (sw != null)
        {
            var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

            base.OnShutdown((int)Math.Max(timeout, 0));
        }
        else
        {
            base.OnShutdown(timeoutMilliseconds);
        }
    }

    // 释放资源
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.shutdownEvent.Dispose();
                this.exportEvent.Dispose();
                this.dataExportNotification.Dispose();
                (this.persistentBlobProvider as IDisposable)?.Dispose();
            }

            this.disposed = true;
        }
    }

    // 重试存储的请求
    private void RetryStoredRequests()
    {
        var handles = new WaitHandle[] { this.shutdownEvent, this.exportEvent };
        while (true)
        {
            try
            {
                var index = WaitHandle.WaitAny(handles, RetryIntervalInMilliseconds);
                if (index == 0)
                {
                    // Shutdown signaled
                    break;
                }

                int fileCount = 0;

                // TODO: 运行维护作业。
                // 每次传输10个文件。
                while (fileCount < 10 && !this.shutdownEvent.WaitOne(0))
                {
                    if (!this.persistentBlobProvider.TryGetBlob(out var blob))
                    {
                        break;
                    }

                    if (blob.TryLease((int)this.TimeoutMilliseconds) && blob.TryRead(out var data))
                    {
                        var deadlineUtc = DateTime.UtcNow.AddMilliseconds(this.TimeoutMilliseconds);
                        var request = this.requestFactory.Invoke(data);
                        if (this.TryRetryRequest(request, deadlineUtc, out var response)
                            || !RetryHelper.ShouldRetryRequest(response, OtlpRetry.InitialBackoffMilliseconds, out var retryInfo))
                        {
                            blob.TryDelete();
                        }

                        // TODO: 根据服务器的retryAfter响应扩展租赁期。
                    }

                    fileCount++;
                }

                // 设置并重置句柄以通知导出并等待下一个信号。
                // 这用于InitiateAndWaitForRetryProcess。
                this.dataExportNotification.Set();
                this.dataExportNotification.Reset();
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.RetryStoredRequestException(ex);
                return;
            }
        }
    }
}
