// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>
/// 一个只读的标签键/值对集合。
/// </summary>
// 注意：不实现 IReadOnlyCollection<> 或 IEnumerable<> 以防止意外装箱。
public readonly struct ReadOnlyTagCollection
{
    // 内部只读键值对数组
    internal readonly KeyValuePair<string, object?>[] KeyAndValues;

    // 内部构造函数，初始化键值对数组
    internal ReadOnlyTagCollection(KeyValuePair<string, object?>[]? keyAndValues)
    {
        // 如果传入的键值对数组为空，则使用空数组
        this.KeyAndValues = keyAndValues ?? Array.Empty<KeyValuePair<string, object?>>();
    }

    /// <summary>
    /// 获取集合中的标签数量。
    /// </summary>
    public int Count => this.KeyAndValues.Length;

    /// <summary>
    /// 返回一个枚举器，该枚举器可遍历标签。
    /// </summary>
    /// <returns><see cref="Enumerator"/>。</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <summary>
    /// 枚举 <see cref="ReadOnlyTagCollection"/> 的元素。
    /// </summary>
    // 注意：不实现 IEnumerator<> 以防止意外装箱。
    public struct Enumerator
    {
        // 源标签集合
        private readonly ReadOnlyTagCollection source;
        // 当前索引
        private int index;

        // 内部构造函数，初始化源标签集合和索引
        internal Enumerator(ReadOnlyTagCollection source)
        {
            this.source = source;
            this.index = -1;
        }

        /// <summary>
        /// 获取枚举器当前位置的标签。
        /// </summary>
        public readonly KeyValuePair<string, object?> Current
            => this.source.KeyAndValues[this.index];

        /// <summary>
        /// 将枚举器推进到 <see cref="ReadOnlyTagCollection"/> 的下一个元素。
        /// </summary>
        /// <returns>如果枚举器成功推进到下一个元素，则为 <see langword="true"/>；如果枚举器已越过集合的末尾，则为 <see langword="false"/>。</returns>
        public bool MoveNext() => ++this.index < this.source.Count;
    }
}
