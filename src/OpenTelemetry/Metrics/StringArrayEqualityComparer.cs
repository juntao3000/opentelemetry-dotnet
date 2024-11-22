// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

// StringArrayEqualityComparer类用于比较两个字符串数组是否相等，并生成字符串数组的哈希码
internal sealed class StringArrayEqualityComparer : IEqualityComparer<string[]>
{
    // Equals方法用于比较两个字符串数组是否相等
    public bool Equals(string[]? strings1, string[]? strings2)
    {
        // 如果两个数组引用相同，则它们相等
        if (ReferenceEquals(strings1, strings2))
        {
            return true;
        }

        // 如果其中一个数组为null，则它们不相等
        if (ReferenceEquals(strings1, null) || ReferenceEquals(strings2, null))
        {
            return false;
        }

        // 获取第一个数组的长度
        var len1 = strings1.Length;

        // 如果两个数组的长度不同，则它们不相等
        if (len1 != strings2.Length)
        {
            return false;
        }

        // 逐个比较数组中的每个字符串
        for (int i = 0; i < len1; i++)
        {
            // 如果有任意一个字符串不相等，则数组不相等
            if (!strings1[i].Equals(strings2[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        // 如果所有字符串都相等，则数组相等
        return true;
    }

    // GetHashCode方法用于生成字符串数组的哈希码
    public int GetHashCode(string[] strings)
    {
        // 确保字符串数组不为null
        Debug.Assert(strings != null, "strings was null");

#if NET
        // 使用HashCode结构生成哈希码
        HashCode hashCode = default;

        // 逐个添加字符串的哈希码
        for (int i = 0; i < strings.Length; i++)
        {
            hashCode.Add(strings[i]);
        }

        // 获取最终的哈希码
        var hash = hashCode.ToHashCode();
#else
        // 初始化哈希码
        var hash = 17;

        // 逐个计算字符串的哈希码
        for (int i = 0; i < strings!.Length; i++)
        {
            unchecked
            {
                // 使用31作为乘数计算哈希码
                hash = (hash * 31) + (strings[i]?.GetHashCode() ?? 0);
            }
        }
#endif

        // 返回最终的哈希码
        return hash;
    }
}
