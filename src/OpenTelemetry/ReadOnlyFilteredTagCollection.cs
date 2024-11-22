// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif
using System.Diagnostics;

namespace OpenTelemetry;

/// <summary>
/// 一个只读的标签键/值对集合，当枚举时返回标签的过滤子集。
/// </summary>
// 注意：不实现 IReadOnlyCollection<> 或 IEnumerable<> 以防止意外装箱。
public readonly struct ReadOnlyFilteredTagCollection
{
#if NET
    // 排除的键集合
    private readonly FrozenSet<string>? excludedKeys;
#else
        // 排除的键集合
        private readonly HashSet<string>? excludedKeys;
#endif
    // 标签键/值对数组
    private readonly KeyValuePair<string, object?>[] tags;
    // 标签数量
    private readonly int count;

    // 内部构造函数
    internal ReadOnlyFilteredTagCollection(
#if NET
        FrozenSet<string>? excludedKeys,
#else
            HashSet<string>? excludedKeys,
#endif
        KeyValuePair<string, object?>[] tags,
        int count)
    {
        Debug.Assert(tags != null, "tags was null");
        Debug.Assert(count <= tags!.Length, "count was invalid");

        this.excludedKeys = excludedKeys;
        this.tags = tags;
        this.count = count;
    }

    /// <summary>
    /// 获取集合中的最大标签数。
    /// </summary>
    /// <remarks>
    /// 注意：枚举集合可能会根据过滤器返回更少的结果。
    /// </remarks>
    internal int MaximumCount => this.count;

    /// <summary>
    /// 返回一个枚举器，该枚举器可遍历标签。
    /// </summary>
    /// <returns><see cref="Enumerator"/>。</returns>
    public Enumerator GetEnumerator() => new(this);

    // 将标签集合转换为只读列表
    internal IReadOnlyList<KeyValuePair<string, object?>> ToReadOnlyList()
    {
        var list = new List<KeyValuePair<string, object?>>(this.MaximumCount);

        foreach (var item in this)
        {
            list.Add(item);
        }

        return list;
    }

    /// <summary>
    /// 枚举 <see cref="ReadOnlyTagCollection"/> 的元素。
    /// </summary>
    // 注意：不实现 IEnumerator<> 以防止意外装箱。
    public struct Enumerator
    {
        // 源标签集合
        private readonly ReadOnlyFilteredTagCollection source;
        // 当前索引
        private int index;

        // 内部构造函数
        internal Enumerator(ReadOnlyFilteredTagCollection source)
        {
            this.source = source;
            this.index = -1;
        }

        /// <summary>
        /// 获取枚举器当前位置的标签。
        /// </summary>
        public readonly KeyValuePair<string, object?> Current
            => this.source.tags[this.index];

        /// <summary>
        /// 将枚举器推进到 <see cref="ReadOnlyTagCollection"/> 的下一个元素。
        /// </summary>
        /// <returns>如果枚举器成功推进到下一个元素，则为 <see langword="true"/>；如果枚举器已越过集合的末尾，则为 <see langword="false"/>。</returns>
        public bool MoveNext()
        {
            while (true)
            {
                int index = ++this.index;
                if (index < this.source.MaximumCount)
                {
                    if (this.source.excludedKeys?.Contains(this.source.tags[index].Key) ?? false)
                    {
                        continue;
                    }

                    return true;
                }

                return false;
            }
        }
    }
}
