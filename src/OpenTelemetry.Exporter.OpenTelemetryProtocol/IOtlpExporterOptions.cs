// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace OpenTelemetry.Exporter;

/// <summary>
/// 描述所有信号共享的OpenTelemetry协议（OTLP）导出器选项。
/// </summary>
internal interface IOtlpExporterOptions
{
    /// <summary>
    /// 获取或设置OTLP传输协议。
    /// </summary>
    OtlpExportProtocol Protocol { get; set; }

    /// <summary>
    /// 获取或设置导出器将发送遥测数据的目标。
    /// </summary>
    /// <remarks>
    /// 注意事项:
    /// <list type="bullet">
    /// <item>设置<see cref="Endpoint"/>时，值必须是带有方案（http或https）和主机的有效<see cref="Uri"/>，并且可以包含端口和路径。</item>
    /// <item>未设置时的默认值基于<see cref="Protocol"/>属性:
    /// <list type="bullet">
    /// <item><see cref="OtlpExportProtocol.Grpc"/>的默认值为<c>http://localhost:4317</c>。</item>
    /// <item><see cref="OtlpExportProtocol.HttpProtobuf"/>的默认值为<c>http://localhost:4318</c>。</item>
    /// </list>
    /// <item>当<see cref="Protocol"/>设置为<see cref="OtlpExportProtocol.HttpProtobuf"/>且<see cref="Endpoint"/>未设置时，默认值（<c>http://localhost:4318</c>）将附加信号特定路径。最终默认端点值将构建为:
    /// <list type="bullet">
    /// <item>日志: <c>http://localhost:4318/v1/logs</c></item>
    /// <item>指标: <c>http://localhost:4318/v1/metrics</c></item>
    /// <item>跟踪: <c>http://localhost:4318/v1/traces</c></item>
    /// </list>
    /// </item>
    /// </item>
    /// </list>
    /// </remarks>
    Uri Endpoint { get; set; }

    /// <summary>
    /// 获取或设置连接的可选头信息。
    /// </summary>
    /// <remarks>
    /// 注意: 有关<see cref="Headers"/>格式的详细信息，请参阅<see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#specifying-headers-via-environment-variables">OpenTelemetry规范</see>。
    /// </remarks>
    string? Headers { get; set; }

    /// <summary>
    /// 获取或设置后端处理每个批次的最大等待时间（以毫秒为单位）。默认值: <c>10000</c>。
    /// </summary>
    int TimeoutMilliseconds { get; set; }

    /// <summary>
    /// 获取或设置用于创建<see cref="HttpClient"/>实例的工厂函数，该实例将在运行时用于通过HTTP传输遥测数据。返回的实例将用于所有导出调用。
    /// </summary>
    /// <remarks>
    /// 注意事项:
    /// <list type="bullet">
    /// <item>这仅在<see cref="OtlpExportProtocol.HttpProtobuf"/>协议中调用。</item>
    /// <item>使用跟踪注册扩展时的默认行为是，如果可以通过应用程序<see cref="IServiceProvider"/>解析<a href="https://docs.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory">IHttpClientFactory</a>实例，则将通过工厂使用名称"OtlpTraceExporter"创建<see cref="HttpClient"/>，否则将直接实例化<see cref="HttpClient"/>。</item>
    /// <item>使用指标注册扩展时的默认行为是，如果可以通过应用程序<see cref="IServiceProvider"/>解析<a href="https://docs.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory">IHttpClientFactory</a>实例，则将通过工厂使用名称"OtlpMetricExporter"创建<see cref="HttpClient"/>，否则将直接实例化<see cref="HttpClient"/>。</item>
    /// <item>使用日志注册扩展时的默认行为是直接实例化<see cref="HttpClient"/>。目前不支持<a href="https://docs.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory">IHttpClientFactory</a>用于日志记录。</item>
    /// </list>
    /// </remarks>
    Func<HttpClient> HttpClientFactory { get; set; }
}
