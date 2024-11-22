// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Resources;

/// <summary>
/// OtelEnvResourceDetector 类实现了 IResourceDetector 接口，用于检测环境变量中的资源属性。
/// </summary>
internal sealed class OtelEnvResourceDetector : IResourceDetector
{
    /// <summary>
    /// 环境变量的键名。
    /// </summary>
    public const string EnvVarKey = "OTEL_RESOURCE_ATTRIBUTES";
    /// <summary>
    /// 属性列表的分隔符。
    /// </summary>
    private const char AttributeListSplitter = ',';
    /// <summary>
    /// 属性键值对的分隔符。
    /// </summary>
    private const char AttributeKeyValueSplitter = '=';

    /// <summary>
    /// 配置对象，用于获取环境变量的值。
    /// </summary>
    private readonly IConfiguration configuration;

    /// <summary>
    /// 初始化 OtelEnvResourceDetector 类的新实例。
    /// </summary>
    /// <param name="configuration">配置对象。</param>
    public OtelEnvResourceDetector(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    /// <summary>
    /// 检测资源属性并返回 Resource 对象。
    /// </summary>
    /// <returns>一个包含检测到的资源属性的 Resource 对象。</returns>
    public Resource Detect()
    {
        var resource = Resource.Empty;

        // 尝试从配置中获取环境变量的值
        if (this.configuration.TryGetStringValue(EnvVarKey, out string? envResourceAttributeValue))
        {
            // 解析资源属性
            var attributes = ParseResourceAttributes(envResourceAttributeValue!);
            // 创建包含属性的 Resource 对象
            resource = new Resource(attributes);
        }

        return resource;
    }

    /// <summary>
    /// 解析资源属性字符串并返回键值对集合。
    /// </summary>
    /// <param name="resourceAttributes">资源属性字符串。</param>
    /// <returns>包含资源属性的键值对集合。</returns>
    private static IEnumerable<KeyValuePair<string, object>> ParseResourceAttributes(string resourceAttributes)
    {
        var attributes = new List<KeyValuePair<string, object>>();

        // 按照分隔符拆分属性列表
        string[] rawAttributes = resourceAttributes.Split(AttributeListSplitter);
        foreach (string rawKeyValuePair in rawAttributes)
        {
            // 按照分隔符拆分键值对
            string[] keyValuePair = rawKeyValuePair.Split(AttributeKeyValueSplitter);
            if (keyValuePair.Length != 2)
            {
                continue;
            }

            // 添加键值对到属性集合
            attributes.Add(new KeyValuePair<string, object>(keyValuePair[0].Trim(), keyValuePair[1].Trim()));
        }

        return attributes;
    }
}
