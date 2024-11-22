// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Reflection;
using Grpc.Core;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
#if NETSTANDARD2_1 || NET
using Grpc.Net.Client;
#endif
using System.Diagnostics;
using Google.Protobuf;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using LogOtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;
using MetricsOtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using TraceOtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter;

// OpenTelemetry Protocol (OTLP) 导出器选项扩展类
internal static class OtlpExporterOptionsExtensions
{
#if NETSTANDARD2_1 || NET
    // 创建 gRPC 通道
    public static GrpcChannel CreateChannel(this OtlpExporterOptions options)
#else
    // 创建 gRPC 通道
    public static Channel CreateChannel(this OtlpExporterOptions options)
#endif
    {
        // 检查 URI 协议是否为 http 或 https
        if (options.Endpoint.Scheme != Uri.UriSchemeHttp && options.Endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new NotSupportedException($"Endpoint URI scheme ({options.Endpoint.Scheme}) is not supported. Currently only \"http\" and \"https\" are supported.");
        }

#if NETSTANDARD2_1 || NET
        return GrpcChannel.ForAddress(options.Endpoint);
#else
        ChannelCredentials channelCredentials;
        if (options.Endpoint.Scheme == Uri.UriSchemeHttps)
        {
            channelCredentials = new SslCredentials();
        }
        else
        {
            channelCredentials = ChannelCredentials.Insecure;
        }

        return new Channel(options.Endpoint.Authority, channelCredentials);
#endif
    }

    // 从 Headers 获取 Metadata
    public static Metadata GetMetadataFromHeaders(this OtlpExporterOptions options)
    {
        return options.GetHeaders<Metadata>((m, k, v) => m.Add(k, v));
    }

    // 获取 Headers
    public static THeaders GetHeaders<THeaders>(this OtlpExporterOptions options, Action<THeaders, string, string> addHeader)
        where THeaders : new()
    {
        var optionHeaders = options.Headers;
        var headers = new THeaders();
        if (!string.IsNullOrEmpty(optionHeaders))
        {
            // 根据规范，必须支持 URL 编码的 Headers
            optionHeaders = Uri.UnescapeDataString(optionHeaders);

            Array.ForEach(
                optionHeaders.Split(','),
                (pair) =>
                {
                    // 指定返回的子字符串的最大数量为 2
                    // 这将把字符串中第一个 `=` 之后的所有内容视为要添加到元数据键的值
                    var keyValueData = pair.Split(new char[] { '=' }, 2);
                    if (keyValueData.Length != 2)
                    {
                        throw new ArgumentException("Headers provided in an invalid format.");
                    }

                    var key = keyValueData[0].Trim();
                    var value = keyValueData[1].Trim();
                    addHeader(headers, key, value);
                });
        }

        foreach (var header in OtlpExporterOptions.StandardHeaders)
        {
            addHeader(headers, header.Key, header.Value);
        }

        return headers;
    }

    // 获取 Trace 导出传输处理程序
    public static OtlpExporterTransmissionHandler<TraceOtlpCollector.ExportTraceServiceRequest> GetTraceExportTransmissionHandler(this OtlpExporterOptions options, ExperimentalOptions experimentalOptions)
    {
        // 获取 Trace 导出客户端
        var exportClient = GetTraceExportClient(options);

        // `HttpClient.Timeout.TotalMilliseconds` 将填充正确的超时值
        double timeoutMilliseconds = exportClient is OtlpHttpTraceExportClient httpTraceExportClient
            ? httpTraceExportClient.HttpClient.Timeout.TotalMilliseconds
            : options.TimeoutMilliseconds;

        // 如果启用了内存重试
        if (experimentalOptions.EnableInMemoryRetry)
        {
            // 返回带有重试功能的传输处理程序
            return new OtlpExporterRetryTransmissionHandler<TraceOtlpCollector.ExportTraceServiceRequest>(exportClient, timeoutMilliseconds);
        }
        // 如果启用了磁盘重试
        else if (experimentalOptions.EnableDiskRetry)
        {
            // 确保磁盘重试目录路径不为空
            Debug.Assert(!string.IsNullOrEmpty(experimentalOptions.DiskRetryDirectoryPath), $"{nameof(experimentalOptions.DiskRetryDirectoryPath)} is null or empty");

            // 返回带有持久化存储功能的传输处理程序
            return new OtlpExporterPersistentStorageTransmissionHandler<TraceOtlpCollector.ExportTraceServiceRequest>(
                exportClient,
                timeoutMilliseconds,
                (byte[] data) =>
                {
                    // 将数据合并到请求中
                    var request = new TraceOtlpCollector.ExportTraceServiceRequest();
                    request.MergeFrom(data);
                    return request;
                },
                // 将路径组合成磁盘重试目录路径
                Path.Combine(experimentalOptions.DiskRetryDirectoryPath, "traces"));
        }
        else
        {
            // 返回普通的传输处理程序
            return new OtlpExporterTransmissionHandler<TraceOtlpCollector.ExportTraceServiceRequest>(exportClient, timeoutMilliseconds);
        }
    }

    // 获取 Protobuf 导出传输处理程序
    public static ProtobufOtlpExporterTransmissionHandler GetProtobufExportTransmissionHandler(this OtlpExporterOptions options, ExperimentalOptions experimentalOptions)
    {
        var exportClient = GetProtobufExportClient(options);

        // `HttpClient.Timeout.TotalMilliseconds` 将填充正确的超时值
        double timeoutMilliseconds = exportClient is ProtobufOtlpHttpExportClient httpTraceExportClient
            ? httpTraceExportClient.HttpClient.Timeout.TotalMilliseconds
            : options.TimeoutMilliseconds;

        if (experimentalOptions.EnableInMemoryRetry)
        {
            return new ProtobufOtlpExporterRetryTransmissionHandler(exportClient, timeoutMilliseconds);
        }
        else if (experimentalOptions.EnableDiskRetry)
        {
            Debug.Assert(!string.IsNullOrEmpty(experimentalOptions.DiskRetryDirectoryPath), $"{nameof(experimentalOptions.DiskRetryDirectoryPath)} is null or empty");

            return new ProtobufOtlpExporterPersistentStorageTransmissionHandler(
                exportClient,
                timeoutMilliseconds,
                Path.Combine(experimentalOptions.DiskRetryDirectoryPath, "traces"));
        }
        else
        {
            return new ProtobufOtlpExporterTransmissionHandler(exportClient, timeoutMilliseconds);
        }
    }

    // 获取 Protobuf 导出客户端
    public static IProtobufExportClient GetProtobufExportClient(this OtlpExporterOptions options)
    {
        var httpClient = options.HttpClientFactory?.Invoke() ?? throw new InvalidOperationException("OtlpExporterOptions was missing HttpClientFactory or it returned null.");

        if (options.Protocol == OtlpExportProtocol.Grpc)
        {
            return new ProtobufOtlpGrpcExportClient(options, httpClient, "opentelemetry.proto.collector.trace.v1.TraceService/Export");
        }
        else
        {
            return new ProtobufOtlpHttpExportClient(options, httpClient, "v1/traces");
        }
    }

    // 获取 Metrics 导出传输处理程序
    public static OtlpExporterTransmissionHandler<MetricsOtlpCollector.ExportMetricsServiceRequest> GetMetricsExportTransmissionHandler(this OtlpExporterOptions options, ExperimentalOptions experimentalOptions)
    {
        // 获取 Metrics 导出客户端
        var exportClient = GetMetricsExportClient(options);

        // `HttpClient.Timeout.TotalMilliseconds` 将填充正确的超时值
        double timeoutMilliseconds = exportClient is OtlpHttpMetricsExportClient httpMetricsExportClient
            ? httpMetricsExportClient.HttpClient.Timeout.TotalMilliseconds
            : options.TimeoutMilliseconds;

        // 如果启用了内存重试
        if (experimentalOptions.EnableInMemoryRetry)
        {
            // 返回带有重试功能的传输处理程序
            return new OtlpExporterRetryTransmissionHandler<MetricsOtlpCollector.ExportMetricsServiceRequest>(exportClient, timeoutMilliseconds);
        }
        // 如果启用了磁盘重试
        else if (experimentalOptions.EnableDiskRetry)
        {
            // 确保磁盘重试目录路径不为空
            Debug.Assert(!string.IsNullOrEmpty(experimentalOptions.DiskRetryDirectoryPath), $"{nameof(experimentalOptions.DiskRetryDirectoryPath)} is null or empty");

            // 返回带有持久化存储功能的传输处理程序
            return new OtlpExporterPersistentStorageTransmissionHandler<MetricsOtlpCollector.ExportMetricsServiceRequest>(
                exportClient,
                timeoutMilliseconds,
                (byte[] data) =>
                {
                    // 将数据合并到请求中
                    var request = new MetricsOtlpCollector.ExportMetricsServiceRequest();
                    request.MergeFrom(data);
                    return request;
                },
                // 将路径组合成磁盘重试目录路径
                Path.Combine(experimentalOptions.DiskRetryDirectoryPath, "metrics"));
        }
        else
        {
            // 返回普通的传输处理程序
            return new OtlpExporterTransmissionHandler<MetricsOtlpCollector.ExportMetricsServiceRequest>(exportClient, timeoutMilliseconds);
        }
    }

    // 获取 Logs 导出传输处理程序
    public static OtlpExporterTransmissionHandler<LogOtlpCollector.ExportLogsServiceRequest> GetLogsExportTransmissionHandler(this OtlpExporterOptions options, ExperimentalOptions experimentalOptions)
    {
        var exportClient = GetLogExportClient(options);
        double timeoutMilliseconds = exportClient is OtlpHttpLogExportClient httpLogExportClient
            ? httpLogExportClient.HttpClient.Timeout.TotalMilliseconds
            : options.TimeoutMilliseconds;

        if (experimentalOptions.EnableInMemoryRetry)
        {
            return new OtlpExporterRetryTransmissionHandler<LogOtlpCollector.ExportLogsServiceRequest>(exportClient, timeoutMilliseconds);
        }
        else if (experimentalOptions.EnableDiskRetry)
        {
            Debug.Assert(!string.IsNullOrEmpty(experimentalOptions.DiskRetryDirectoryPath), $"{nameof(experimentalOptions.DiskRetryDirectoryPath)} is null or empty");

            return new OtlpExporterPersistentStorageTransmissionHandler<LogOtlpCollector.ExportLogsServiceRequest>(
                exportClient,
                timeoutMilliseconds,
                (byte[] data) =>
                {
                    var request = new LogOtlpCollector.ExportLogsServiceRequest();
                    request.MergeFrom(data);
                    return request;
                },
                Path.Combine(experimentalOptions.DiskRetryDirectoryPath, "logs"));
        }
        else
        {
            return new OtlpExporterTransmissionHandler<LogOtlpCollector.ExportLogsServiceRequest>(exportClient, timeoutMilliseconds);
        }
    }

    // 获取 Trace 导出客户端
    public static IExportClient<TraceOtlpCollector.ExportTraceServiceRequest> GetTraceExportClient(this OtlpExporterOptions options) =>
        options.Protocol switch
        {
            // 如果协议是 gRPC，则返回 OtlpGrpcTraceExportClient
            OtlpExportProtocol.Grpc => new OtlpGrpcTraceExportClient(options),
            // 如果协议是 HttpProtobuf，则返回 OtlpHttpTraceExportClient
            OtlpExportProtocol.HttpProtobuf => new OtlpHttpTraceExportClient(
                options,
                // 如果 HttpClientFactory 为空，则抛出 InvalidOperationException 异常
                options.HttpClientFactory?.Invoke() ?? throw new InvalidOperationException("OtlpExporterOptions was missing HttpClientFactory or it returned null.")),
            // 如果协议不支持，则抛出 NotSupportedException 异常
            _ => throw new NotSupportedException($"Protocol {options.Protocol} is not supported."),
        };

    // 获取 Metrics 导出客户端
    public static IExportClient<MetricsOtlpCollector.ExportMetricsServiceRequest> GetMetricsExportClient(this OtlpExporterOptions options) =>
        options.Protocol switch
        {
            OtlpExportProtocol.Grpc => new OtlpGrpcMetricsExportClient(options),
            OtlpExportProtocol.HttpProtobuf => new OtlpHttpMetricsExportClient(
                options,
                options.HttpClientFactory?.Invoke() ?? throw new InvalidOperationException("OtlpExporterOptions was missing HttpClientFactory or it returned null.")),
            _ => throw new NotSupportedException($"Protocol {options.Protocol} is not supported."),
        };

    // 获取 Log 导出客户端
    public static IExportClient<LogOtlpCollector.ExportLogsServiceRequest> GetLogExportClient(this OtlpExporterOptions options) =>
        options.Protocol switch
        {
            OtlpExportProtocol.Grpc => new OtlpGrpcLogExportClient(options),
            OtlpExportProtocol.HttpProtobuf => new OtlpHttpLogExportClient(
                options,
                options.HttpClientFactory?.Invoke() ?? throw new InvalidOperationException("OtlpExporterOptions was missing HttpClientFactory or it returned null.")),
            _ => throw new NotSupportedException($"Protocol {options.Protocol} is not supported."),
        };

    // 尝试启用 IHttpClientFactory 集成
    public static void TryEnableIHttpClientFactoryIntegration(this OtlpExporterOptions options, IServiceProvider serviceProvider, string httpClientName)
    {
        if (serviceProvider != null
            && options.Protocol == OtlpExportProtocol.HttpProtobuf
            && options.HttpClientFactory == options.DefaultHttpClientFactory)
        {
            options.HttpClientFactory = () =>
            {
                Type? httpClientFactoryType = Type.GetType("System.Net.Http.IHttpClientFactory, Microsoft.Extensions.Http", throwOnError: false);
                if (httpClientFactoryType != null)
                {
                    object? httpClientFactory = serviceProvider.GetService(httpClientFactoryType);
                    if (httpClientFactory != null)
                    {
                        MethodInfo? createClientMethod = httpClientFactoryType.GetMethod(
                            "CreateClient",
                            BindingFlags.Public | BindingFlags.Instance,
                            binder: null,
                            new Type[] { typeof(string) },
                            modifiers: null);
                        if (createClientMethod != null)
                        {
                            HttpClient? client = (HttpClient?)createClientMethod.Invoke(httpClientFactory, new object[] { httpClientName });

                            if (client != null)
                            {
                                client.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);

                                return client;
                            }
                        }
                    }
                }

                return options.DefaultHttpClientFactory();
            };
        }
    }

    // 如果路径不存在则追加路径
    internal static Uri AppendPathIfNotPresent(this Uri uri, string path)
    {
        var absoluteUri = uri.AbsoluteUri;
        var separator = string.Empty;

        if (absoluteUri.EndsWith("/"))
        {
            // Endpoint 已经以 'path/' 结尾
            if (absoluteUri.EndsWith(string.Concat(path, "/"), StringComparison.OrdinalIgnoreCase))
            {
                return uri;
            }
        }
        else
        {
            // Endpoint 已经以 'path' 结尾
            if (absoluteUri.EndsWith(path, StringComparison.OrdinalIgnoreCase))
            {
                return uri;
            }

            separator = "/";
        }

        return new Uri(string.Concat(uri.AbsoluteUri, separator, path));
    }
}
