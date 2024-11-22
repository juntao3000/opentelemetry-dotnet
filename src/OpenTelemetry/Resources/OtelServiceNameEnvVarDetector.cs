// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Resources;

// OtelServiceNameEnvVarDetector 类实现了 IResourceDetector 接口，用于检测 OTEL_SERVICE_NAME 环境变量并生成相应的 Resource 对象。
internal sealed class OtelServiceNameEnvVarDetector : IResourceDetector
{
    // OTEL_SERVICE_NAME 环境变量的键名。
    public const string EnvVarKey = "OTEL_SERVICE_NAME";

    // 用于访问配置的 IConfiguration 实例。
    private readonly IConfiguration configuration;

    // 构造函数，接受一个 IConfiguration 实例作为参数。
    public OtelServiceNameEnvVarDetector(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    // Detect 方法用于检测 OTEL_SERVICE_NAME 环境变量并生成相应的 Resource 对象。
    public Resource Detect()
    {
        // 初始化一个空的 Resource 对象。
        var resource = Resource.Empty;

        // 尝试从配置中获取 OTEL_SERVICE_NAME 环境变量的值。
        if (this.configuration.TryGetStringValue(EnvVarKey, out string? envResourceAttributeValue))
        {
            // 如果获取成功，则创建一个包含该值的 Resource 对象。
            resource = new Resource(new Dictionary<string, object>
            {
                [ResourceSemanticConventions.AttributeServiceName] = envResourceAttributeValue!,
            });
        }

        // 返回生成的 Resource 对象。
        return resource;
    }
}
