// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OpenTelemetry;

// WildcardHelper类：提供处理通配符的帮助方法
internal static class WildcardHelper
{
    // ContainsWildcard方法：检查字符串中是否包含通配符（*或?）
    public static bool ContainsWildcard(
        [NotNullWhen(true)]
            string? value)
    {
        if (value == null)
        {
            return false;
        }

        // 检查字符串中是否包含'*'或'?'字符
        return value.Contains('*') || value.Contains('?');
    }

    // GetWildcardRegex方法：将通配符模式转换为正则表达式
    public static Regex GetWildcardRegex(IEnumerable<string> patterns)
    {
        // 断言patterns不为空且包含元素
        Debug.Assert(patterns?.Any() == true, "patterns was null or empty");

        // 将通配符模式转换为正则表达式模式
        var convertedPattern = string.Join(
            "|",
            from p in patterns select "(?:" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + ')');

        // 创建并返回一个编译后的正则表达式对象
        return new Regex("^(?:" + convertedPattern + ")$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
