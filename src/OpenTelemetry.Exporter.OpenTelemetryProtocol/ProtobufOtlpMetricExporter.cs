// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

// OpenTelemetry 协议 (OTLP) 导出器，用于消费 <see cref="Metric"/> 并导出数据。
internal sealed class ProtobufOtlpMetricExporter : BaseExporter<Metric>
{
    // 传输处理程序，用于处理导出请求的传输。
    private readonly ProtobufOtlpExporterTransmissionHandler transmissionHandler;
    // 起始写入位置，根据协议不同而不同。
    private readonly int startWritePosition;

    // 资源信息，可能为空。
    private Resource? resource;

    // 初始缓冲区大小设置为约 732KB。
    // 这个选择允许我们逐步增加缓冲区大小，目标是最终容量约为 100 MB，
    // 通过第 7 次加倍来保持高效分配而不频繁调整大小。
    private byte[] buffer = new byte[750000];

    /// <summary>
    /// 初始化 <see cref="ProtobufOtlpMetricExporter"/> 类的新实例。
    /// </summary>
    /// <param name="options">导出器的配置选项。</param>
    public ProtobufOtlpMetricExporter(OtlpExporterOptions options)
        : this(options, experimentalOptions: new(), transmissionHandler: null)
    {
    }

    /// <summary>
    /// 初始化 <see cref="ProtobufOtlpMetricExporter"/> 类的新实例。
    /// </summary>
    /// <param name="exporterOptions"><see cref="OtlpExporterOptions"/>。</param>
    /// <param name="experimentalOptions"><see cref="ExperimentalOptions"/>。</param>
    /// <param name="transmissionHandler"><see cref="OtlpExporterTransmissionHandler{T}"/>。</param>
    internal ProtobufOtlpMetricExporter(
        OtlpExporterOptions exporterOptions,
        ExperimentalOptions experimentalOptions,
        ProtobufOtlpExporterTransmissionHandler? transmissionHandler = null)
    {
        Debug.Assert(exporterOptions != null, "exporterOptions 为空");
        Debug.Assert(experimentalOptions != null, "experimentalOptions 为空");

        // 根据协议设置起始写入位置。
        this.startWritePosition = exporterOptions!.Protocol == OtlpExportProtocol.Grpc ? 5 : 0;
        // 初始化传输处理程序。
        this.transmissionHandler = transmissionHandler ?? exporterOptions!.GetProtobufExportTransmissionHandler(experimentalOptions!);
    }

    // 获取资源信息，如果为空则从父提供程序获取。
    internal Resource Resource => this.resource ??= this.ParentProvider.GetResource();

    /// <inheritdoc />
    public override ExportResult Export(in Batch<Metric> metrics)
    {
        // 防止导出器的 gRPC 和 HTTP 操作被检测。
        using var scope = SuppressInstrumentationScope.Begin();

        try
        {
            // 序列化度量数据到缓冲区。
            int writePosition = ProtobufOtlpMetricSerializer.WriteMetricsData(this.buffer, this.startWritePosition, this.Resource, metrics);

            if (this.startWritePosition == 5)
            {
                // Grpc 负载由 3 部分组成
                // 字节 0 - 指定负载是否压缩。
                // 字节 1-4 - 以大端格式指定负载长度。
                // 字节 5 及以上 - Protobuf 序列化数据。
                Span<byte> data = new Span<byte>(this.buffer, 1, 4);
                var dataLength = writePosition - 5;
                BinaryPrimitives.WriteUInt32BigEndian(data, (uint)dataLength);
            }

            // 尝试提交请求。
            if (!this.transmissionHandler.TrySubmitRequest(this.buffer, writePosition))
            {
                return ExportResult.Failure;
            }
        }
        catch (IndexOutOfRangeException)
        {
            // 缓冲区大小不足时增加缓冲区大小。
            if (!this.IncreaseBufferSize())
            {
                throw;
            }
        }
        catch (Exception ex)
        {
            // 捕获异常并记录。
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            return ExportResult.Failure;
        }

        return ExportResult.Success;
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds) => this.transmissionHandler.Shutdown(timeoutMilliseconds);

    // 增加缓冲区大小，返回是否成功。
    private bool IncreaseBufferSize()
    {
        var newBufferSize = this.buffer.Length * 2;

        if (newBufferSize > 100 * 1024 * 1024)
        {
            return false;
        }

        var newBuffer = new byte[newBufferSize];
        this.buffer.CopyTo(newBuffer, 0);
        this.buffer = newBuffer;

        return true;
    }
}
