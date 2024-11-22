// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry;

/// <summary>
/// 存储一批已完成的 <typeparamref name="T"/> 对象以供导出。
/// </summary>
/// <typeparam name="T"> <see cref="Batch{T}"/> 中对象的类型。</typeparam>
public readonly struct Batch<T> : IDisposable
    where T : class
{
    // 单个项目
    private readonly T? item = null;
    // 环形缓冲区
    private readonly CircularBuffer<T>? circularBuffer = null;
    // 项目数组
    private readonly T[]? items = null;
    // 目标计数
    private readonly long targetCount;

    /// <summary>
    /// 初始化 <see cref="Batch{T}"/> 结构的新实例。
    /// </summary>
    /// <param name="items">要存储在批次中的项目。</param>
    /// <param name="count">批次中的项目数。</param>
    public Batch(T[] items, int count)
    {
        Guard.ThrowIfNull(items);
        Guard.ThrowIfOutOfRange(count, min: 0, max: items.Length);

        this.items = items;
        this.Count = this.targetCount = count;
    }

    /// <summary>
    /// 初始化 <see cref="Batch{T}"/> 结构的新实例。
    /// </summary>
    /// <param name="item">要存储在批次中的项目。</param>
    public Batch(T item)
    {
        Guard.ThrowIfNull(item);

        this.item = item;
        this.Count = this.targetCount = 1;
    }

    internal Batch(CircularBuffer<T> circularBuffer, int maxSize)
    {
        Debug.Assert(maxSize > 0, $"{nameof(maxSize)} 应该是一个正数。");
        Debug.Assert(circularBuffer != null, $"{nameof(circularBuffer)} 为空。");

        this.circularBuffer = circularBuffer;
        this.Count = Math.Min(maxSize, circularBuffer!.Count);
        this.targetCount = circularBuffer.RemovedCount + this.Count;
    }

    // 批次枚举器移动下一个函数委托
    private delegate bool BatchEnumeratorMoveNextFunc(ref Enumerator enumerator);

    /// <summary>
    /// 获取批次中的项目数。
    /// </summary>
    public long Count { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.circularBuffer != null)
        {
            // 清空批次中剩余的任何内容。
            while (this.circularBuffer.RemovedCount < this.targetCount)
            {
                T item = this.circularBuffer.Read();
                if (typeof(T) == typeof(LogRecord))
                {
                    var logRecord = (LogRecord)(object)item;
                    if (logRecord.Source == LogRecord.LogRecordSource.FromSharedPool)
                    {
                        LogRecordSharedPool.Current.Return(logRecord);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 返回一个枚举器，该枚举器可遍历 <see cref="Batch{T}"/>。
    /// </summary>
    /// <returns><see cref="Enumerator"/>。</returns>
    public Enumerator GetEnumerator()
    {
        return this.circularBuffer != null
            ? new Enumerator(this.circularBuffer, this.targetCount)
            : this.item != null
                ? new Enumerator(this.item)
                /* 如果有人使用 default/new Batch() 创建 Batch，我们会回退到空项目模式。 */
                : new Enumerator(this.items ?? Array.Empty<T>(), this.targetCount);
    }

    /// <summary>
    /// 枚举 <see cref="Batch{T}"/> 的元素。
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        // 单个项目的移动下一个函数
        private static readonly BatchEnumeratorMoveNextFunc MoveNextSingleItem = (ref Enumerator enumerator) =>
        {
            if (enumerator.targetCount >= 0)
            {
                enumerator.current = null;
                return false;
            }

            enumerator.targetCount++;
            return true;
        };

        // 环形缓冲区的移动下一个函数
        private static readonly BatchEnumeratorMoveNextFunc MoveNextCircularBuffer = (ref Enumerator enumerator) =>
        {
            var circularBuffer = enumerator.circularBuffer;

            if (circularBuffer!.RemovedCount < enumerator.targetCount)
            {
                enumerator.current = circularBuffer.Read();
                return true;
            }

            enumerator.current = null;
            return false;
        };

        // 环形缓冲区日志记录的移动下一个函数
        private static readonly BatchEnumeratorMoveNextFunc MoveNextCircularBufferLogRecord = (ref Enumerator enumerator) =>
        {
            // 注意：此处的类型检查是为了给 JIT 提示，当 T != LogRecord 时可以删除所有这些代码
            if (typeof(T) == typeof(LogRecord))
            {
                var circularBuffer = enumerator.circularBuffer;

                var currentItem = enumerator.Current;

                if (currentItem != null)
                {
                    var logRecord = (LogRecord)(object)currentItem;
                    if (logRecord.Source == LogRecord.LogRecordSource.FromSharedPool)
                    {
                        LogRecordSharedPool.Current.Return(logRecord);
                    }
                }

                if (circularBuffer!.RemovedCount < enumerator.targetCount)
                {
                    enumerator.current = circularBuffer.Read();
                    return true;
                }

                enumerator.current = null;
            }

            return false;
        };

        // 数组的移动下一个函数
        private static readonly BatchEnumeratorMoveNextFunc MoveNextArray = (ref Enumerator enumerator) =>
        {
            var items = enumerator.items;

            if (enumerator.itemIndex < enumerator.targetCount)
            {
                enumerator.current = items![enumerator.itemIndex++];
                return true;
            }

            enumerator.current = null;
            return false;
        };

        // 环形缓冲区
        private readonly CircularBuffer<T>? circularBuffer;
        // 项目数组
        private readonly T[]? items;
        // 移动下一个函数
        private readonly BatchEnumeratorMoveNextFunc moveNextFunc;
        // 目标计数
        private long targetCount;
        // 项目索引
        private int itemIndex;
        // 当前项目
        [AllowNull]
        private T current;

        internal Enumerator(T item)
        {
            this.current = item;
            this.circularBuffer = null;
            this.items = null;
            this.targetCount = -1;
            this.itemIndex = 0;
            this.moveNextFunc = MoveNextSingleItem;
        }

        internal Enumerator(CircularBuffer<T> circularBuffer, long targetCount)
        {
            this.current = null;
            this.items = null;
            this.circularBuffer = circularBuffer;
            this.targetCount = targetCount;
            this.itemIndex = 0;
            this.moveNextFunc = typeof(T) == typeof(LogRecord) ? MoveNextCircularBufferLogRecord : MoveNextCircularBuffer;
        }

        internal Enumerator(T[] items, long targetCount)
        {
            this.current = null;
            this.circularBuffer = null;
            this.items = items;
            this.targetCount = targetCount;
            this.itemIndex = 0;
            this.moveNextFunc = MoveNextArray;
        }

        /// <inheritdoc/>
        public readonly T Current => this.current;

        /// <inheritdoc/>
        readonly object? IEnumerator.Current => this.current;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (typeof(T) == typeof(LogRecord))
            {
                var currentItem = this.current;
                if (currentItem != null)
                {
                    var logRecord = (LogRecord)(object)currentItem;
                    if (logRecord.Source == LogRecord.LogRecordSource.FromSharedPool)
                    {
                        LogRecordSharedPool.Current.Return(logRecord);
                    }

                    this.current = null;
                }
            }
        }

        /// <inheritdoc/>
        public bool MoveNext()
        {
            return this.moveNextFunc(ref this);
        }

        /// <inheritdoc/>
        public readonly void Reset()
            => throw new NotSupportedException();
    }
}
