// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 基于环形缓冲区的直方图桶实现。
/// </summary>
internal sealed class CircularBufferBuckets
{
    // 存储桶值的数组
    private long[]? trait;
    // 环形缓冲区的起始索引
    private int begin = 0;
    // 环形缓冲区的结束索引
    private int end = -1;

    // 构造函数，初始化容量
    public CircularBufferBuckets(int capacity)
    {
        Guard.ThrowIfOutOfRange(capacity, min: 1);
        this.Capacity = capacity;
    }

    /// <summary>
    /// 获取 <see cref="CircularBufferBuckets"/> 的容量。
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 获取 <see cref="CircularBufferBuckets"/> 的起始索引偏移量。
    /// </summary>
    public int Offset => this.begin;

    /// <summary>
    /// 获取 <see cref="CircularBufferBuckets"/> 的大小。
    /// </summary>
    public int Size => this.end - this.begin + 1;

    /// <summary>
    /// 返回 <c>Bucket[index]</c> 的值。
    /// </summary>
    /// <param name="index">桶的索引。</param>
    /// <remarks>
    /// "index" 值可以是正数、零或负数。
    /// 此方法不验证 "index" 是否落在 [begin, end] 范围内，
    /// 调用者负责验证。
    /// </remarks>
    public long this[int index]
    {
        get
        {
            Debug.Assert(this.trait != null, "trait 为空");
            return this.trait![this.ModuloIndex(index)];
        }
    }

    /// <summary>
    /// 尝试将 <c>Bucket[index]</c> 的值增加 <c>value</c>。
    /// </summary>
    /// <param name="index">桶的索引。</param>
    /// <param name="value">增量。</param>
    /// <returns>
    /// 如果增量尝试成功，则返回 <c>0</c>；
    /// 如果增量尝试失败，则返回一个正整数，表示最小的缩放减少级别。
    /// </returns>
    /// <remarks>
    /// "index" 值可以是正数、零或负数。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int TryIncrement(int index, long value = 1)
    {
        var capacity = this.Capacity;

        if (this.trait == null)
        {
            this.trait = new long[capacity];
            this.begin = index;
            this.end = index;
            this.trait[this.ModuloIndex(index)] += value;
            return 0;
        }

        var begin = this.begin;
        var end = this.end;

        if (index > end)
        {
            end = index;
        }
        else if (index < begin)
        {
            begin = index;
        }
        else
        {
            this.trait[this.ModuloIndex(index)] += value;
            return 0;
        }

        var diff = end - begin;

        if (diff >= capacity || diff < 0)
        {
            return CalculateScaleReduction(begin, end, capacity);
        }

        this.begin = begin;
        this.end = end;
        this.trait[this.ModuloIndex(index)] += value;
        return 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int CalculateScaleReduction(int begin, int end, int capacity)
        {
            Debug.Assert(capacity >= 2, "容量必须至少为 2。");
            var retval = 0;
            var diff = end - begin;
            while (diff >= capacity || diff < 0)
            {
                begin >>= 1;
                end >>= 1;
                diff = end - begin;
                retval++;
            }
            return retval;
        }
    }

    // 缩小环形缓冲区的范围
    public void ScaleDown(int level = 1)
    {
        Debug.Assert(level > 0, "缩小级别必须是正整数。");

        if (this.trait == null)
        {
            return;
        }

        uint capacity = (uint)this.Capacity;
        var offset = (uint)this.ModuloIndex(this.begin);

        var currentBegin = this.begin;
        var currentEnd = this.end;

        for (int i = 0; i < level; i++)
        {
            var newBegin = currentBegin >> 1;
            var newEnd = currentEnd >> 1;

            if (currentBegin != currentEnd)
            {
                if (currentBegin % 2 == 0)
                {
                    ScaleDownInternal(this.trait, offset, currentBegin, currentEnd, capacity);
                }
                else
                {
                    currentBegin++;
                    if (currentBegin != currentEnd)
                    {
                        ScaleDownInternal(this.trait, offset + 1, currentBegin, currentEnd, capacity);
                    }
                }
            }

            currentBegin = newBegin;
            currentEnd = newEnd;
        }

        this.begin = currentBegin;
        this.end = currentEnd;

        if (capacity > 1)
        {
            AdjustPosition(this.trait, offset, (uint)this.ModuloIndex(currentBegin), (uint)(currentEnd - currentBegin + 1), capacity);
        }

        return;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ScaleDownInternal(long[] array, uint offset, int begin, int end, uint capacity)
        {
            for (var index = begin + 1; index < end; index++)
            {
                Consolidate(array, (offset + (uint)(index - begin)) % capacity, (offset + (uint)((index >> 1) - (begin >> 1))) % capacity);
            }

            Consolidate(array, (offset + (uint)(end - begin)) % capacity, (offset + (uint)((end >> 1) - (begin >> 1))) % capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void AdjustPosition(long[] array, uint src, uint dst, uint size, uint capacity)
        {
            var advancement = (dst + capacity - src) % capacity;

            if (advancement == 0)
            {
                return;
            }

            if (size - 1 == advancement && advancement << 1 == capacity)
            {
                Exchange(array, src++, dst++);
                size -= 2;
            }
            else if (advancement < size)
            {
                src = src + size - 1;
                dst = dst + size - 1;

                while (size-- != 0)
                {
                    Move(array, src-- % capacity, dst-- % capacity);
                }

                return;
            }

            while (size-- != 0)
            {
                Move(array, src++ % capacity, dst++ % capacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Consolidate(long[] array, uint src, uint dst)
        {
            array[dst] += array[src];
            array[src] = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Exchange(long[] array, uint src, uint dst)
        {
            var value = array[dst];
            array[dst] = array[src];
            array[src] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Move(long[] array, uint src, uint dst)
        {
            array[dst] = array[src];
            array[src] = 0;
        }
    }

    // 重置环形缓冲区
    internal void Reset()
    {
        if (this.trait != null)
        {
            for (var i = 0; i < this.trait.Length; ++i)
            {
                this.trait[i] = 0;
            }
        }
    }

    // 复制环形缓冲区的内容到目标数组
    internal void Copy(long[] dst)
    {
        Debug.Assert(dst.Length == this.Capacity, "目标数组的长度必须等于容量。");

        if (this.trait != null)
        {
            for (var i = 0; i < this.Size; ++i)
            {
                dst[i] = this[this.Offset + i];
            }
        }
    }

    // 计算索引的正模
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ModuloIndex(int value)
    {
        return MathHelper.PositiveModulo32(value, this.Capacity);
    }
}
