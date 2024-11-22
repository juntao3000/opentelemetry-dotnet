// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter;

// OpenTelemetry 协议 (OTLP) 导出器选项。
public class OtlpExporterOptions : IOtlpExporterOptions
{
    // 默认的 gRPC 端点
    internal const string DefaultGrpcEndpoint = "http://localhost:4317";
    // 默认的 HTTP 端点
    internal const string DefaultHttpEndpoint = "http://localhost:4318";
    // 默认的 OTLP 导出协议
    internal const OtlpExportProtocol DefaultOtlpExportProtocol = OtlpExportProtocol.Grpc;

    // 标准请求头
    internal static readonly KeyValuePair<string, string>[] StandardHeaders = new KeyValuePair<string, string>[]
    {
        new("User-Agent", GetUserAgentString()),
    };

    // 默认的 HttpClient 工厂方法
    internal readonly Func<HttpClient> DefaultHttpClientFactory;

    // OTLP 导出协议
    private OtlpExportProtocol? protocol;
    // 端点 URI
    private Uri? endpoint;
    // 超时时间（毫秒）
    private int? timeoutMilliseconds;
    // HttpClient 工厂方法
    private Func<HttpClient>? httpClientFactory;

    // 初始化 OtlpExporterOptions 类的新实例。
    public OtlpExporterOptions()
        : this(OtlpExporterOptionsConfigurationType.Default)
    {
    }

    internal OtlpExporterOptions(
        OtlpExporterOptionsConfigurationType configurationType)
        : this(
              configuration: new ConfigurationBuilder().AddEnvironmentVariables().Build(),
              configurationType,
              defaultBatchOptions: new())
    {
    }

    internal OtlpExporterOptions(
        IConfiguration configuration,
        OtlpExporterOptionsConfigurationType configurationType,
        BatchExportActivityProcessorOptions defaultBatchOptions)
    {
        Debug.Assert(defaultBatchOptions != null, "defaultBatchOptions was null");

        this.ApplyConfiguration(configuration, configurationType);

        this.DefaultHttpClientFactory = () =>
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(this.TimeoutMilliseconds),
            };
        };

        this.BatchExportProcessorOptions = defaultBatchOptions!;
    }

    // 获取或设置导出器的端点 URI。
    public Uri Endpoint
    {
        get
        {
            if (this.endpoint == null)
            {
                return this.Protocol == OtlpExportProtocol.Grpc
                    ? new Uri(DefaultGrpcEndpoint)
                    : new Uri(DefaultHttpEndpoint);
            }

            return this.endpoint;
        }

        set
        {
            Guard.ThrowIfNull(value);

            this.endpoint = value;
            this.AppendSignalPathToEndpoint = false;
        }
    }

    // 获取或设置连接的可选请求头。
    public string? Headers { get; set; }

    // 获取或设置后端处理每个批次的最大等待时间（毫秒）。
    public int TimeoutMilliseconds
    {
        get => this.timeoutMilliseconds ?? 10000;
        set => this.timeoutMilliseconds = value;
    }

    // 获取或设置 OTLP 传输协议。
    public OtlpExportProtocol Protocol
    {
        get => this.protocol ?? DefaultOtlpExportProtocol;
        set => this.protocol = value;
    }

    // 获取或设置要与 OpenTelemetry 协议导出器一起使用的导出处理器类型。默认值为 Batch。
    public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

    // 获取或设置 BatchExportProcessor 选项。除非 ExportProcessorType 为 Batch，否则忽略。
    public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; }

    // 获取或设置用于创建 HttpClient 实例的工厂函数。
    public Func<HttpClient> HttpClientFactory
    {
        get => this.httpClientFactory ?? this.DefaultHttpClientFactory;
        set
        {
            Guard.ThrowIfNull(value);

            this.httpClientFactory = value;
        }
    }

    // 获取一个值，该值指示是否应将信号特定路径附加到 Endpoint。
    internal bool AppendSignalPathToEndpoint { get; private set; } = true;

    // 检查是否有数据
    internal bool HasData
        => this.protocol.HasValue
        || this.endpoint != null
        || this.timeoutMilliseconds.HasValue
        || this.httpClientFactory != null;

    // 创建 OtlpExporterOptions 实例
    internal static OtlpExporterOptions CreateOtlpExporterOptions(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        string name)
        => new(
            configuration,
            OtlpExporterOptionsConfigurationType.Default,
            serviceProvider.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>().Get(name));

    // 使用规范环境变量应用配置
    internal void ApplyConfigurationUsingSpecificationEnvVars(
        IConfiguration configuration,
        string endpointEnvVarKey,
        bool appendSignalPathToEndpoint,
        string protocolEnvVarKey,
        string headersEnvVarKey,
        string timeoutEnvVarKey)
    {
        if (configuration.TryGetUriValue(OpenTelemetryProtocolExporterEventSource.Log, endpointEnvVarKey, out var endpoint))
        {
            this.endpoint = endpoint;
            this.AppendSignalPathToEndpoint = appendSignalPathToEndpoint;
        }

        if (configuration.TryGetValue<OtlpExportProtocol>(
            OpenTelemetryProtocolExporterEventSource.Log,
            protocolEnvVarKey,
            OtlpExportProtocolParser.TryParse,
            out var protocol))
        {
            this.Protocol = protocol;
        }

        if (configuration.TryGetStringValue(headersEnvVarKey, out var headers))
        {
            this.Headers = headers;
        }

        if (configuration.TryGetIntValue(OpenTelemetryProtocolExporterEventSource.Log, timeoutEnvVarKey, out var timeout))
        {
            this.TimeoutMilliseconds = timeout;
        }
    }

    // 应用默认配置
    internal OtlpExporterOptions ApplyDefaults(OtlpExporterOptions defaultExporterOptions)
    {
        this.protocol ??= defaultExporterOptions.protocol;

        this.endpoint ??= defaultExporterOptions.endpoint;

        // 注意：我们在这里将 AppendSignalPathToEndpoint 设置为 true，因为我们希望在端点来自默认端点时附加信号。

        this.Headers ??= defaultExporterOptions.Headers;

        this.timeoutMilliseconds ??= defaultExporterOptions.timeoutMilliseconds;

        this.httpClientFactory ??= defaultExporterOptions.httpClientFactory;

        return this;
    }

    // 获取 User-Agent 字符串
    private static string GetUserAgentString()
    {
        var assembly = typeof(OtlpExporterOptions).Assembly;
        return $"OTel-OTLP-Exporter-Dotnet/{assembly.GetPackageVersion()}";
    }

    // 应用配置
    private void ApplyConfiguration(
        IConfiguration configuration,
        OtlpExporterOptionsConfigurationType configurationType)
    {
        Debug.Assert(configuration != null, "configuration was null");

        // 注意：使用 "AddOtlpExporter" 扩展时，configurationType 从未有其他值，因为 OtlpExporterOptions 由所有信号共享，无法区分正在构建哪个信号。
        if (configurationType == OtlpExporterOptionsConfigurationType.Default)
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration!,
                OtlpSpecConfigDefinitions.DefaultEndpointEnvVarName,
                appendSignalPathToEndpoint: true,
                OtlpSpecConfigDefinitions.DefaultProtocolEnvVarName,
                OtlpSpecConfigDefinitions.DefaultHeadersEnvVarName,
                OtlpSpecConfigDefinitions.DefaultTimeoutEnvVarName);
        }
        else if (configurationType == OtlpExporterOptionsConfigurationType.Logs)
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration!,
                OtlpSpecConfigDefinitions.LogsEndpointEnvVarName,
                appendSignalPathToEndpoint: false,
                OtlpSpecConfigDefinitions.LogsProtocolEnvVarName,
                OtlpSpecConfigDefinitions.LogsHeadersEnvVarName,
                OtlpSpecConfigDefinitions.LogsTimeoutEnvVarName);
        }
        else if (configurationType == OtlpExporterOptionsConfigurationType.Metrics)
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration!,
                OtlpSpecConfigDefinitions.MetricsEndpointEnvVarName,
                appendSignalPathToEndpoint: false,
                OtlpSpecConfigDefinitions.MetricsProtocolEnvVarName,
                OtlpSpecConfigDefinitions.MetricsHeadersEnvVarName,
                OtlpSpecConfigDefinitions.MetricsTimeoutEnvVarName);
        }
        else if (configurationType == OtlpExporterOptionsConfigurationType.Traces)
        {
            this.ApplyConfigurationUsingSpecificationEnvVars(
                configuration!,
                OtlpSpecConfigDefinitions.TracesEndpointEnvVarName,
                appendSignalPathToEndpoint: false,
                OtlpSpecConfigDefinitions.TracesProtocolEnvVarName,
                OtlpSpecConfigDefinitions.TracesHeadersEnvVarName,
                OtlpSpecConfigDefinitions.TracesTimeoutEnvVarName);
        }
        else
        {
            throw new NotSupportedException($"OtlpExporterOptionsConfigurationType '{configurationType}' is not supported.");
        }
    }
}
