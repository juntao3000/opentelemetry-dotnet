// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Metrics;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter;

/// <summary>
/// 导出器，消费 <see cref="Metric"/> 并使用 OpenTelemetry 协议 (OTLP) 导出数据。
/// </summary>
public class OtlpMetricExporter : BaseExporter<Metric>
{
    // 传输处理程序
    private readonly OtlpExporterTransmissionHandler<OtlpCollector.ExportMetricsServiceRequest> transmissionHandler;

    // 进程资源
    private OtlpResource.Resource? processResource;

    /// <summary>
    /// 初始化 <see cref="OtlpMetricExporter"/> 类的新实例。
    /// </summary>
    /// <param name="options">导出器的配置选项。</param>
    public OtlpMetricExporter(OtlpExporterOptions options)
        : this(options, experimentalOptions: new(), transmissionHandler: null)
    {
    }

    /// <summary>
    /// 初始化 <see cref="OtlpMetricExporter"/> 类的新实例。
    /// </summary>
    /// <param name="exporterOptions"><see cref="OtlpExporterOptions"/>。</param>
    /// <param name="experimentalOptions"><see cref="ExperimentalOptions"/>。</param>
    /// <param name="transmissionHandler"><see cref="OtlpExporterTransmissionHandler{T}"/>。</param>
    internal OtlpMetricExporter(
        OtlpExporterOptions exporterOptions,
        ExperimentalOptions experimentalOptions,
        OtlpExporterTransmissionHandler<OtlpCollector.ExportMetricsServiceRequest>? transmissionHandler = null)
    {
        Debug.Assert(exporterOptions != null, "exporterOptions 为空");
        Debug.Assert(experimentalOptions != null, "experimentalOptions 为空");

        // 初始化传输处理程序
        this.transmissionHandler = transmissionHandler ?? exporterOptions!.GetMetricsExportTransmissionHandler(experimentalOptions!);
    }

    // 获取进程资源
    internal OtlpResource.Resource ProcessResource => this.processResource ??= this.ParentProvider.GetResource().ToOtlpResource();

    /// <inheritdoc />
    public override ExportResult Export(in Batch<Metric> metrics)
    {
        // 防止导出器的 gRPC 和 HTTP 操作被检测。
        using var scope = SuppressInstrumentationScope.Begin();

        // 创建导出请求
        var request = new OtlpCollector.ExportMetricsServiceRequest();

        try
        {
            // 添加度量数据到请求
            request.AddMetrics(this.ProcessResource, metrics);

            // 尝试提交请求
            if (!this.transmissionHandler.TrySubmitRequest(request))
            {
                return ExportResult.Failure;
            }
        }
        catch (Exception ex)
        {
            // 记录导出方法异常
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            return ExportResult.Failure;
        }
        finally
        {
            // 归还请求
            // Return 方法的目的是为了提高性能。具体来说，它通过对象池（ConcurrentBag<ScopeMetrics>）来重用 ScopeMetrics 对象，从而减少了频繁创建和销毁对象所带来的性能开销。
            request.Return();
        }

        return ExportResult.Success;
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        // 关闭传输处理程序
        return this.transmissionHandler.Shutdown(timeoutMilliseconds);
    }
}
