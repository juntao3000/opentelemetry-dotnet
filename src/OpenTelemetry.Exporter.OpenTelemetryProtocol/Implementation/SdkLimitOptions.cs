// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

// SdkLimitOptions类：用于配置SDK的各种限制选项
internal sealed class SdkLimitOptions
{
    // 默认的SDK限制值
    private const int DefaultSdkLimit = 128;
    // span属性值长度限制
    private int? spanAttributeValueLengthLimit;
    // 是否设置了span属性值长度限制
    private bool spanAttributeValueLengthLimitSet;
    // span属性数量限制
    private int? spanAttributeCountLimit;
    // 是否设置了span属性数量限制
    private bool spanAttributeCountLimitSet;
    // span事件属性数量限制
    private int? spanEventAttributeCountLimit;
    // 是否设置了span事件属性数量限制
    private bool spanEventAttributeCountLimitSet;
    // span链接属性数量限制
    private int? spanLinkAttributeCountLimit;
    // 是否设置了span链接属性数量限制
    private bool spanLinkAttributeCountLimitSet;
    // 日志记录属性值长度限制
    private int? logRecordAttributeValueLengthLimit;
    // 是否设置了日志记录属性值长度限制
    private bool logRecordAttributeValueLengthLimitSet;
    // 日志记录属性数量限制
    private int? logRecordAttributeCountLimit;
    // 是否设置了日志记录属性数量限制
    private bool logRecordAttributeCountLimitSet;

    // SdkLimitOptions构造函数：从环境变量中读取配置
    public SdkLimitOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    // SdkLimitOptions构造函数：从指定的配置中读取配置
    internal SdkLimitOptions(IConfiguration configuration)
    {
        // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.25.0/specification/configuration/sdk-environment-variables.md#attribute-limits
        SetIntConfigValue(configuration, "OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT", value => this.AttributeValueLengthLimit = value, null);
        SetIntConfigValue(configuration, "OTEL_ATTRIBUTE_COUNT_LIMIT", value => this.AttributeCountLimit = value, DefaultSdkLimit);

        // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.25.0/specification/configuration/sdk-environment-variables.md#span-limits
        SetIntConfigValue(configuration, "OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT", value => this.SpanAttributeValueLengthLimit = value, null);
        SetIntConfigValue(configuration, "OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT", value => this.SpanAttributeCountLimit = value, null);
        SetIntConfigValue(configuration, "OTEL_SPAN_EVENT_COUNT_LIMIT", value => this.SpanEventCountLimit = value, DefaultSdkLimit);
        SetIntConfigValue(configuration, "OTEL_SPAN_LINK_COUNT_LIMIT", value => this.SpanLinkCountLimit = value, DefaultSdkLimit);
        SetIntConfigValue(configuration, "OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT", value => this.SpanEventAttributeCountLimit = value, null);
        SetIntConfigValue(configuration, "OTEL_LINK_ATTRIBUTE_COUNT_LIMIT", value => this.SpanLinkAttributeCountLimit = value, null);

        // https://github.com/open-telemetry/opentelemetry-specification/blob/v1.25.0/specification/configuration/sdk-environment-variables.md#logrecord-limits
        SetIntConfigValue(configuration, "OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT", value => this.LogRecordAttributeValueLengthLimit = value, null);
        SetIntConfigValue(configuration, "OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT", value => this.LogRecordAttributeCountLimit = value, null);
    }

    /// <summary>
    /// 获取或设置最大允许的属性值大小。
    /// </summary>
    public int? AttributeValueLengthLimit { get; set; }

    /// <summary>
    /// 获取或设置最大允许的属性数量。
    /// </summary>
    public int? AttributeCountLimit { get; set; }

    /// <summary>
    /// 获取或设置最大允许的span属性值大小。
    /// </summary>
    /// <remarks>
    /// 注意：如果指定，将覆盖<see cref="AttributeValueLengthLimit"/>设置。
    /// </remarks>
    public int? SpanAttributeValueLengthLimit
    {
        get => this.spanAttributeValueLengthLimitSet ? this.spanAttributeValueLengthLimit : this.AttributeValueLengthLimit;
        set
        {
            this.spanAttributeValueLengthLimitSet = true;
            this.spanAttributeValueLengthLimit = value;
        }
    }

    /// <summary>
    /// 获取或设置最大允许的span属性数量。
    /// </summary>
    /// <remarks>
    /// 注意：如果指定，将覆盖<see cref="AttributeCountLimit"/>设置。
    /// </remarks>
    public int? SpanAttributeCountLimit
    {
        get => this.spanAttributeCountLimitSet ? this.spanAttributeCountLimit : this.AttributeCountLimit;
        set
        {
            this.spanAttributeCountLimitSet = true;
            this.spanAttributeCountLimit = value;
        }
    }

    /// <summary>
    /// 获取或设置最大允许的span事件数量。
    /// </summary>
    public int? SpanEventCountLimit { get; set; }

    /// <summary>
    /// 获取或设置最大允许的span链接数量。
    /// </summary>
    public int? SpanLinkCountLimit { get; set; }

    /// <summary>
    /// 获取或设置最大允许的span事件属性数量。
    /// </summary>
    /// <remarks>
    /// 注意：如果指定，将覆盖<see cref="SpanAttributeCountLimit"/>设置。
    /// </remarks>
    public int? SpanEventAttributeCountLimit
    {
        get => this.spanEventAttributeCountLimitSet ? this.spanEventAttributeCountLimit : this.SpanAttributeCountLimit;
        set
        {
            this.spanEventAttributeCountLimitSet = true;
            this.spanEventAttributeCountLimit = value;
        }
    }

    /// <summary>
    /// 获取或设置最大允许的span链接属性数量。
    /// </summary>
    /// <remarks>
    /// 注意：如果指定，将覆盖<see cref="SpanAttributeCountLimit"/>设置。
    /// </remarks>
    public int? SpanLinkAttributeCountLimit
    {
        get => this.spanLinkAttributeCountLimitSet ? this.spanLinkAttributeCountLimit : this.SpanAttributeCountLimit;
        set
        {
            this.spanLinkAttributeCountLimitSet = true;
            this.spanLinkAttributeCountLimit = value;
        }
    }

    /// <summary>
    /// 获取或设置最大允许的日志记录属性值大小。
    /// </summary>
    /// <remarks>
    /// 注意：如果指定，将覆盖<see cref="AttributeValueLengthLimit"/>设置。
    /// </remarks>
    public int? LogRecordAttributeValueLengthLimit
    {
        get => this.logRecordAttributeValueLengthLimitSet ? this.logRecordAttributeValueLengthLimit : this.AttributeValueLengthLimit;
        set
        {
            this.logRecordAttributeValueLengthLimitSet = true;
            this.logRecordAttributeValueLengthLimit = value;
        }
    }

    /// <summary>
    /// 获取或设置最大允许的日志记录属性数量。
    /// </summary>
    /// <remarks>
    /// 注意：如果指定，将覆盖<see cref="AttributeCountLimit"/>设置。
    /// </remarks>
    public int? LogRecordAttributeCountLimit
    {
        get => this.logRecordAttributeCountLimitSet ? this.logRecordAttributeCountLimit : this.AttributeCountLimit;
        set
        {
            this.logRecordAttributeCountLimitSet = true;
            this.logRecordAttributeCountLimit = value;
        }
    }

    // SetIntConfigValue方法：从配置中读取整数值并设置
    private static void SetIntConfigValue(IConfiguration configuration, string key, Action<int?> setter, int? defaultValue)
    {
        if (configuration.TryGetIntValue(OpenTelemetryProtocolExporterEventSource.Log, key, out var result))
        {
            setter(result);
        }
        else if (defaultValue.HasValue)
        {
            setter(defaultValue);
        }
    }
}
