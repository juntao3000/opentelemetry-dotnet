// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

/// <summary>
/// OTLP导出器协议类型，根据规范https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md。
/// </summary>
public enum OtlpExportProtocol : byte
{
    /// <summary>
    /// OTLP通过gRPC（对应于'grpc'协议配置选项）。用作默认值。
    /// </summary>
    Grpc = 0,

    /// <summary>
    /// OTLP通过HTTP和protobuf负载（对应于'http/protobuf'协议配置选项）。
    /// </summary>
    HttpProtobuf = 1,
}
