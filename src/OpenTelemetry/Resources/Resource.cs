// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources;

/// <summary>
/// <see cref="Resource"/> 表示一个资源，它捕获有关报告遥测的实体的识别信息。
/// 使用 <see cref="ResourceBuilder"/> 构建资源实例。
/// </summary>
public class Resource
{
    // 此实现遵循 https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md

    /// <summary>
    /// 初始化 <see cref="Resource"/> 类的新实例。
    /// </summary>
    /// <param name="attributes">描述资源的属性的 <see cref="IEnumerable{T}"/>。</param>
    public Resource(IEnumerable<KeyValuePair<string, object>> attributes)
    {
        // 如果 attributes 为空，则记录无效参数并设置空属性集合
        if (attributes == null)
        {
            OpenTelemetrySdkEventSource.Log.InvalidArgument("Create resource", "attributes", "are null");
            this.Attributes = Enumerable.Empty<KeyValuePair<string, object>>();
            return;
        }

        // 资源创建预计在应用启动期间进行几次，即不在热路径上，我们可以复制属性。
        this.Attributes = attributes.Select(SanitizeAttribute).ToList();
    }

    /// <summary>
    /// 获取一个空的 Resource。
    /// </summary>
    public static Resource Empty { get; } = new Resource(Enumerable.Empty<KeyValuePair<string, object>>());

    /// <summary>
    /// 获取描述资源的键值对集合。
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>> Attributes { get; }

    /// <summary>
    /// 通过将旧的 <see cref="Resource"/> 与 <c>other</c> <see cref="Resource"/> 合并，返回一个新的合并的 <see cref="Resource"/>。
    /// 如果发生冲突，另一个 <see cref="Resource"/> 优先。
    /// </summary>
    /// <param name="other">将与 <c>this</c> 合并的 <see cref="Resource"/>。</param>
    /// <returns><see cref="Resource"/>。</returns>
    public Resource Merge(Resource other)
    {
        // 创建一个新的字典来存储合并后的属性
        var newAttributes = new Dictionary<string, object>();

        // 如果 other 不为空，则将其属性添加到新字典中
        if (other != null)
        {
            foreach (var attribute in other.Attributes)
            {
                if (!newAttributes.TryGetValue(attribute.Key, out _))
                {
                    newAttributes[attribute.Key] = attribute.Value;
                }
            }
        }

        // 将当前实例的属性添加到新字典中
        foreach (var attribute in this.Attributes)
        {
            if (!newAttributes.TryGetValue(attribute.Key, out _))
            {
                newAttributes[attribute.Key] = attribute.Value;
            }
        }

        // 返回一个新的 Resource 实例，包含合并后的属性
        return new Resource(newAttributes);
    }

    // 清理属性键值对
    private static KeyValuePair<string, object> SanitizeAttribute(KeyValuePair<string, object> attribute)
    {
        string sanitizedKey;
        // 如果键为空，则记录无效参数并设置为空字符串
        if (attribute.Key == null)
        {
            OpenTelemetrySdkEventSource.Log.InvalidArgument("Create resource", "attribute key", "Attribute key should be non-null string.");
            sanitizedKey = string.Empty;
        }
        else
        {
            sanitizedKey = attribute.Key;
        }

        // 清理属性值
        var sanitizedValue = SanitizeValue(attribute.Value, sanitizedKey);
        return new KeyValuePair<string, object>(sanitizedKey, sanitizedValue);
    }

    // 清理属性值
    private static object SanitizeValue(object value, string keyName)
    {
        Guard.ThrowIfNull(keyName);

        // 根据值的类型进行转换和清理
        return value switch
        {
            string => value,
            bool => value,
            double => value,
            long => value,
            string[] => value,
            bool[] => value,
            double[] => value,
            long[] => value,
            int => Convert.ToInt64(value),
            short => Convert.ToInt64(value),
            float => Convert.ToDouble(value, CultureInfo.InvariantCulture),
            int[] v => Array.ConvertAll(v, Convert.ToInt64),
            short[] v => Array.ConvertAll(v, Convert.ToInt64),
            float[] v => Array.ConvertAll(v, f => Convert.ToDouble(f, CultureInfo.InvariantCulture)),
            _ => throw new ArgumentException("Attribute value type is not an accepted primitive", keyName),
        };
    }
}
