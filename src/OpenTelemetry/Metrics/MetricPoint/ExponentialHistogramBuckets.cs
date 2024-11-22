// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 包含指数直方图的桶。
/// </summary>
// 注意：不实现 IEnumerable<> 以防止意外装箱。
public sealed class ExponentialHistogramBuckets
{
    // 存储桶的数组
    private long[] buckets = Array.Empty<long>();
    // 存储桶的大小
    private int size;

    // 构造函数，初始化 ExponentialHistogramBuckets 实例
    internal ExponentialHistogramBuckets()
    {
    }

    /// <summary>
    /// 获取指数直方图的偏移量。
    /// </summary>
    public int Offset { get; private set; }

    /// <summary>
    /// 返回一个枚举器，该枚举器可遍历 <see cref="ExponentialHistogramBuckets"/>。
    /// </summary>
    /// <returns><see cref="Enumerator"/>。</returns>
    public Enumerator GetEnumerator() => new(this.buckets, this.size);

    // 快照桶，将 CircularBufferBuckets 的内容复制到当前实例
    internal void SnapshotBuckets(CircularBufferBuckets buckets)
    {
        if (this.buckets.Length != buckets.Capacity)
        {
            this.buckets = new long[buckets.Capacity];
        }

        this.size = buckets.Size;
        this.Offset = buckets.Offset;
        buckets.Copy(this.buckets);
    }

    // 复制当前 ExponentialHistogramBuckets 实例
    internal ExponentialHistogramBuckets Copy()
    {
        var copy = new ExponentialHistogramBuckets
        {
            size = this.size,
            Offset = this.Offset,
            buckets = new long[this.buckets.Length],
        };
        Array.Copy(this.buckets, copy.buckets, this.buckets.Length);
        return copy;
    }

    /// <summary>
    /// 枚举指数直方图的桶计数。
    /// </summary>
    // 注意：不实现 IEnumerator<> 以防止意外装箱。
    public struct Enumerator
    {
        // 存储桶的数组
        private readonly long[] buckets;
        // 存储桶的大小
        private readonly int size;
        // 当前索引
        private int index;

        // 构造函数，初始化 Enumerator 实例
        internal Enumerator(long[] buckets, int size)
        {
            this.index = 0;
            this.size = size;
            this.buckets = buckets;
            this.Current = default;
        }

        /// <summary>
        /// 获取枚举器当前位置的桶计数。
        /// </summary>
        public long Current { get; private set; }

        /// <summary>
        /// 将枚举器推进到 <see cref="HistogramBuckets"/> 的下一个元素。
        /// </summary>
        /// <returns>如果枚举器成功地推进到下一个元素，则为 <see langword="true"/>；如果枚举器已越过集合的末尾，则为 <see langword="false"/>。</returns>
        public bool MoveNext()
        {
            if (this.index < this.size)
            {
                this.Current = this.buckets[this.index++];
                return true;
            }

            return false;
        }
    }
}
