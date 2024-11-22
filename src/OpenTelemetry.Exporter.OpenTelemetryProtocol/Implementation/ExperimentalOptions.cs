// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

// ExperimentalOptions类用于处理实验性配置选项
internal sealed class ExperimentalOptions
{
    // 日志记录事件ID属性
    public const string LogRecordEventIdAttribute = "logrecord.event.id";

    // 日志记录事件名称属性
    public const string LogRecordEventNameAttribute = "logrecord.event.name";

    // 环境变量：是否导出日志事件属性
    public const string EmitLogEventEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES";

    // 环境变量：OTLP重试策略
    public const string OtlpRetryEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY";

    // 环境变量：OTLP磁盘重试目录路径
    public const string OtlpDiskRetryDirectoryPathEnvVar = "OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH";

    // 环境变量：是否使用自定义序列化器
    public const string OtlpUseCustomSerializer = "OTEL_DOTNET_EXPERIMENTAL_USE_CUSTOM_PROTOBUF_SERIALIZER";

    // 构造函数：从环境变量构建配置
    public ExperimentalOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    // 构造函数：从给定的配置构建ExperimentalOptions
    public ExperimentalOptions(IConfiguration configuration)
    {
        // 尝试从配置中获取EmitLogEventEnvVar的布尔值
        if (configuration.TryGetBoolValue(OpenTelemetryProtocolExporterEventSource.Log, EmitLogEventEnvVar, out var emitLogEventAttributes))
        {
            this.EmitLogEventAttributes = emitLogEventAttributes;
        }

        // 尝试从配置中获取OtlpUseCustomSerializer的布尔值
        if (configuration.TryGetBoolValue(OpenTelemetryProtocolExporterEventSource.Log, OtlpUseCustomSerializer, out var useCustomSerializer))
        {
            this.UseCustomProtobufSerializer = useCustomSerializer;
        }

        // 尝试从配置中获取OtlpRetryEnvVar的字符串值
        if (configuration.TryGetStringValue(OtlpRetryEnvVar, out var retryPolicy) && retryPolicy != null)
        {
            if (retryPolicy.Equals("in_memory", StringComparison.OrdinalIgnoreCase))
            {
                this.EnableInMemoryRetry = true;
            }
            else if (retryPolicy.Equals("disk", StringComparison.OrdinalIgnoreCase))
            {
                this.EnableDiskRetry = true;
                // 尝试从配置中获取OtlpDiskRetryDirectoryPathEnvVar的字符串值
                if (configuration.TryGetStringValue(OtlpDiskRetryDirectoryPathEnvVar, out var path) && path != null)
                {
                    this.DiskRetryDirectoryPath = path;
                }
                else
                {
                    // 回退到临时位置
                    this.DiskRetryDirectoryPath = Path.GetTempPath();
                }
            }
            else
            {
                throw new NotSupportedException($"Retry Policy '{retryPolicy}' is not supported.");
            }
        }
    }

    /// <summary>
    /// 获取一个值，指示是否应导出日志事件属性。
    /// </summary>
    public bool EmitLogEventAttributes { get; }

    /// <summary>
    /// 获取一个值，指示是否应为瞬态错误启用内存重试。
    /// </summary>
    /// <remarks>
    /// 规范：<see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#retry"/>。
    /// </remarks>
    public bool EnableInMemoryRetry { get; }

    /// <summary>
    /// 获取一个值，指示是否应为瞬态错误启用磁盘重试。
    /// </summary>
    public bool EnableDiskRetry { get; }

    /// <summary>
    /// 获取磁盘上的路径，遥测数据将存储在该路径以便稍后重试。
    /// </summary>
    public string? DiskRetryDirectoryPath { get; }

    /// <summary>
    /// 获取一个值，指示是否应使用自定义序列化器进行OTLP导出。
    /// </summary>
    public bool UseCustomProtobufSerializer { get; }
}
