// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources;

/// <summary>
/// 包含用于构建 <see cref="Resource"/> 的扩展方法。
/// </summary>
public static class ResourceBuilderExtensions
{
    // 静态只读字段，用于存储唯一的实例ID
    private static readonly string InstanceId = Guid.NewGuid().ToString();

    // 静态只读属性，用于存储遥测资源
    private static Resource TelemetryResource { get; } = new Resource(new Dictionary<string, object>
    {
        [ResourceSemanticConventions.AttributeTelemetrySdkName] = "opentelemetry",
        [ResourceSemanticConventions.AttributeTelemetrySdkLanguage] = "dotnet",
        [ResourceSemanticConventions.AttributeTelemetrySdkVersion] = Sdk.InformationalVersion,
    });

    /// <summary>
    /// 添加服务信息到 <see cref="ResourceBuilder"/>，遵循
    /// <a href="https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions#service">语义约定</a>。
    /// </summary>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>。</param>
    /// <param name="serviceName">服务名称。</param>
    /// <param name="serviceNamespace">可选的服务命名空间。</param>
    /// <param name="serviceVersion">可选的服务版本。</param>
    /// <param name="autoGenerateServiceInstanceId">如果未提供 <paramref name="serviceInstanceId"/>，则指定 <see langword="true"/> 以自动生成 <see cref="Guid"/>。</param>
    /// <param name="serviceInstanceId">可选的服务实例唯一标识符。</param>
    /// <returns>返回 <see cref="ResourceBuilder"/> 以进行链式调用。</returns>
    public static ResourceBuilder AddService(
        this ResourceBuilder resourceBuilder,
        string serviceName,
        string? serviceNamespace = null,
        string? serviceVersion = null,
        bool autoGenerateServiceInstanceId = true,
        string? serviceInstanceId = null)
    {
        // 创建一个字典来存储资源属性
        Dictionary<string, object> resourceAttributes = new Dictionary<string, object>();

        // 检查服务名称是否为空或null
        Guard.ThrowIfNullOrEmpty(serviceName);

        // 添加服务名称到资源属性
        resourceAttributes.Add(ResourceSemanticConventions.AttributeServiceName, serviceName);

        // 如果服务命名空间不为空，则添加到资源属性
        if (!string.IsNullOrEmpty(serviceNamespace))
        {
            resourceAttributes.Add(ResourceSemanticConventions.AttributeServiceNamespace, serviceNamespace!);
        }

        // 如果服务版本不为空，则添加到资源属性
        if (!string.IsNullOrEmpty(serviceVersion))
        {
            resourceAttributes.Add(ResourceSemanticConventions.AttributeServiceVersion, serviceVersion!);
        }

        // 如果服务实例ID为空且自动生成服务实例ID为true，则使用InstanceId
        if (serviceInstanceId == null && autoGenerateServiceInstanceId)
        {
            serviceInstanceId = InstanceId;
        }

        // 如果服务实例ID不为空，则添加到资源属性
        if (serviceInstanceId != null)
        {
            resourceAttributes.Add(ResourceSemanticConventions.AttributeServiceInstance, serviceInstanceId);
        }

        // 添加资源并返回资源构建器
        return resourceBuilder.AddResource(new Resource(resourceAttributes));
    }

    /// <summary>
    /// 添加遥测SDK信息到 <see cref="ResourceBuilder"/>，遵循
    /// <a href="https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#telemetry-sdk">语义约定</a>。
    /// </summary>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>。</param>
    /// <returns>返回 <see cref="ResourceBuilder"/> 以进行链式调用。</returns>
    public static ResourceBuilder AddTelemetrySdk(this ResourceBuilder resourceBuilder)
    {
        // 添加遥测资源并返回资源构建器
        return resourceBuilder.AddResource(TelemetryResource);
    }

    /// <summary>
    /// 向 <see cref="ResourceBuilder"/> 添加属性。
    /// </summary>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>。</param>
    /// <param name="attributes">描述资源的属性的 <see cref="IEnumerable{T}"/>。</param>
    /// <returns>返回 <see cref="ResourceBuilder"/> 以进行链式调用。</returns>
    public static ResourceBuilder AddAttributes(this ResourceBuilder resourceBuilder, IEnumerable<KeyValuePair<string, object>> attributes)
    {
        // 添加资源并返回资源构建器
        return resourceBuilder.AddResource(new Resource(attributes));
    }

    /// <summary>
    /// 从 OTEL_RESOURCE_ATTRIBUTES 和 OTEL_SERVICE_NAME 环境变量解析资源属性并添加到 <see cref="ResourceBuilder"/>，遵循
    /// <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable">资源SDK</a>。
    /// </summary>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>。</param>
    /// <returns>返回 <see cref="ResourceBuilder"/> 以进行链式调用。</returns>
    public static ResourceBuilder AddEnvironmentVariableDetector(this ResourceBuilder resourceBuilder)
    {
        // 延迟加载配置
        Lazy<IConfiguration> configuration = new Lazy<IConfiguration>(() => new ConfigurationBuilder().AddEnvironmentVariables().Build());

        // 添加检测器并返回资源构建器
        return resourceBuilder
            .AddDetectorInternal(sp => new OtelEnvResourceDetector(sp?.GetService<IConfiguration>() ?? configuration.Value))
            .AddDetectorInternal(sp => new OtelServiceNameEnvVarDetector(sp?.GetService<IConfiguration>() ?? configuration.Value));
    }
}
