// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 表示一个只读的 Exemplar 集合。
/// </summary>
public readonly struct ReadOnlyExemplarCollection
{
    // 定义一个空的 ReadOnlyExemplarCollection 实例
    internal static readonly ReadOnlyExemplarCollection Empty = new(Array.Empty<Exemplar>());
    // 存储 Exemplar 的数组
    private readonly Exemplar[] exemplars;

    // 构造函数，初始化 ReadOnlyExemplarCollection 实例
    internal ReadOnlyExemplarCollection(Exemplar[] exemplars)
    {
        Debug.Assert(exemplars != null, "exemplars was null"); // 确保 exemplars 不为 null

        this.exemplars = exemplars!;
    }

    /// <summary>
    /// 获取集合中 Exemplar 的最大数量。
    /// </summary>
    /// <remarks>
    /// 注意：枚举集合时可能会返回更少的结果，具体取决于集合中的哪些 Exemplar 收到了更新。
    /// </remarks>
    internal int MaximumCount => this.exemplars.Length;

    /// <summary>
    /// 返回一个枚举器，用于遍历 Exemplar 集合。
    /// </summary>
    /// <returns><see cref="Enumerator"/>。</returns>
    public Enumerator GetEnumerator()
        => new(this.exemplars);

    // 复制当前的 ReadOnlyExemplarCollection 实例
    internal ReadOnlyExemplarCollection Copy()
    {
        var maximumCount = this.MaximumCount;

        if (maximumCount > 0)
        {
            var exemplarCopies = new Exemplar[maximumCount];

            int i = 0;
            foreach (ref readonly var exemplar in this)
            {
                if (exemplar.IsUpdated())
                {
                    exemplar.Copy(ref exemplarCopies[i++]);
                }
            }

            return new ReadOnlyExemplarCollection(exemplarCopies);
        }

        return Empty;
    }

    // 将当前的 ReadOnlyExemplarCollection 实例转换为只读列表
    internal IReadOnlyList<Exemplar> ToReadOnlyList()
    {
        var list = new List<Exemplar>(this.MaximumCount);

        foreach (var exemplar in this)
        {
            // 注意：如果 ToReadOnlyList 方法公开，则应确保对 exemplars 进行复制，或者确保实例首先使用上面的 Copy 方法进行复制。
            list.Add(exemplar);
        }

        return list;
    }

    /// <summary>
    /// 枚举 ReadOnlyExemplarCollection 的元素。
    /// </summary>
    public struct Enumerator
    {
        // 存储 Exemplar 的数组
        private readonly Exemplar[] exemplars;
        // 当前索引
        private int index;

        // 构造函数，初始化 Enumerator 实例
        internal Enumerator(Exemplar[] exemplars)
        {
            this.exemplars = exemplars;
            this.index = -1;
        }

        /// <summary>
        /// 获取枚举器当前位置的 Exemplar。
        /// </summary>
        public readonly ref readonly Exemplar Current
            => ref this.exemplars[this.index];

        /// <summary>
        /// 将枚举器推进到 ReadOnlyExemplarCollection 的下一个元素。
        /// </summary>
        /// <returns>如果枚举器成功地推进到下一个元素，则为 true；如果枚举器已越过集合的末尾，则为 false。</returns>
        public bool MoveNext()
        {
            var exemplars = this.exemplars;

            while (true)
            {
                var index = ++this.index;
                if (index < exemplars.Length)
                {
                    if (!exemplars[index].IsUpdated())
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
