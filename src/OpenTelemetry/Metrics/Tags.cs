// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
#if NET
using System.Runtime.InteropServices;
#endif

namespace OpenTelemetry.Metrics;

// Tags 结构体用于表示一组键值对标签，并实现了 IEquatable<Tags> 接口
internal readonly struct Tags : IEquatable<Tags>
{
    // 定义一个静态只读的空标签集合
    public static readonly Tags EmptyTags = new(Array.Empty<KeyValuePair<string, object?>>());

    // 存储标签集合的哈希码
    private readonly int hashCode;

    // 构造函数，初始化标签集合和哈希码
    public Tags(KeyValuePair<string, object?>[] keyValuePairs)
    {
        this.KeyValuePairs = keyValuePairs;
        this.hashCode = ComputeHashCode(keyValuePairs);
    }

    // 存储标签集合的键值对数组
    public readonly KeyValuePair<string, object?>[] KeyValuePairs { get; }

    // 重载 == 运算符，比较两个 Tags 对象是否相等
    public static bool operator ==(Tags tag1, Tags tag2) => tag1.Equals(tag2);

    // 重载 != 运算符，比较两个 Tags 对象是否不相等
    public static bool operator !=(Tags tag1, Tags tag2) => !tag1.Equals(tag2);

    // 重写 Equals 方法，比较当前对象与另一个对象是否相等
    public override readonly bool Equals(object? obj)
    {
        return obj is Tags other && this.Equals(other);
    }

    // 实现 IEquatable<Tags> 接口的 Equals 方法，比较两个 Tags 对象是否相等
    public readonly bool Equals(Tags other)
    {
        var ourKvps = this.KeyValuePairs;
        var theirKvps = other.KeyValuePairs;

        var length = ourKvps.Length;

        if (length != theirKvps.Length)
        {
            return false;
        }

#if NET
        // 注意：此循环使用不安全代码（指针）来消除我们知道长度相等的两个数组的边界检查。
        if (length > 0)
        {
            ref var ours = ref MemoryMarshal.GetArrayDataReference(ourKvps);
            ref var theirs = ref MemoryMarshal.GetArrayDataReference(theirKvps);
            while (true)
            {
                // 注意：string.Equals 执行的是序数比较
                if (!ours.Key.Equals(theirs.Key))
                {
                    return false;
                }

                if (!ours.Value?.Equals(theirs.Value) ?? theirs.Value != null)
                {
                    return false;
                }

                if (--length == 0)
                {
                    break;
                }

                ours = ref Unsafe.Add(ref ours, 1);
                theirs = ref Unsafe.Add(ref theirs, 1);
            }
        }
#else
        for (int i = 0; i < length; i++)
        {
            ref var ours = ref ourKvps[i];

            // 注意：此处对 theirKvps 元素访问进行边界检查
            ref var theirs = ref theirKvps[i];

            // 注意：string.Equals 执行的是序数比较
            if (!ours.Key.Equals(theirs.Key))
            {
                return false;
            }

            if (!ours.Value?.Equals(theirs.Value) ?? theirs.Value != null)
            {
                return false;
            }
        }
#endif

        return true;
    }

    // 重写 GetHashCode 方法，返回标签集合的哈希码
    public override readonly int GetHashCode() => this.hashCode;

    // 计算标签集合的哈希码
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeHashCode(KeyValuePair<string, object?>[] keyValuePairs)
    {
        Debug.Assert(keyValuePairs != null, "keyValuePairs 不能为空");

#if NET
        HashCode hashCode = default;

        for (int i = 0; i < keyValuePairs.Length; i++)
        {
            ref var item = ref keyValuePairs[i];
            hashCode.Add(item.Key.GetHashCode());
            hashCode.Add(item.Value);
        }

        return hashCode.ToHashCode();
#else
        var hash = 17;

        for (int i = 0; i < keyValuePairs!.Length; i++)
        {
            ref var item = ref keyValuePairs[i];
            unchecked
            {
                hash = (hash * 31) + item.Key.GetHashCode();
                hash = (hash * 31) + (item.Value?.GetHashCode() ?? 0);
            }
        }

        return hash;
#endif
    }
}
