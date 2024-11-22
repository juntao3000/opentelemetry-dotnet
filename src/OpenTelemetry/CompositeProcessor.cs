// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// 表示 <see cref="BaseProcessor{T}"/> 的链。
/// </summary>
/// <typeparam name="T">要处理的对象类型。</typeparam>
public class CompositeProcessor<T> : BaseProcessor<T>
{
    // 链表头节点
    internal readonly DoublyLinkedListNode Head;
    // 链表尾节点
    private DoublyLinkedListNode tail;
    // 处理器是否已释放
    private bool disposed;

    /// <summary>
    /// 初始化 <see cref="CompositeProcessor{T}"/> 类的新实例。
    /// </summary>
    /// <param name="processors">要添加到复合处理器链的处理器。</param>
    public CompositeProcessor(IEnumerable<BaseProcessor<T>> processors)
    {
        Guard.ThrowIfNull(processors);

        using var iter = processors.GetEnumerator();
        if (!iter.MoveNext())
        {
            throw new ArgumentException($"'{iter}' is null or empty", nameof(iter));
        }

        this.Head = new DoublyLinkedListNode(iter.Current);
        this.tail = this.Head;

        while (iter.MoveNext())
        {
            this.AddProcessor(iter.Current);
        }
    }

    /// <summary>
    /// 向复合处理器链添加处理器。
    /// </summary>
    /// <param name="processor"><see cref="BaseProcessor{T}"/>。</param>
    /// <returns>当前实例以支持调用链。</returns>
    public CompositeProcessor<T> AddProcessor(BaseProcessor<T> processor)
    {
        Guard.ThrowIfNull(processor);

        var node = new DoublyLinkedListNode(processor)
        {
            Previous = this.tail,
        };
        this.tail.Next = node;
        this.tail = node;

        return this;
    }

    /// <inheritdoc/>
    public override void OnEnd(T data)
    {
        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            cur.Value.OnEnd(data);
        }
    }

    /// <inheritdoc/>
    public override void OnStart(T data)
    {
        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            cur.Value.OnStart(data);
        }
    }

    internal override void SetParentProvider(BaseProvider parentProvider)
    {
        base.SetParentProvider(parentProvider);

        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            cur.Value.SetParentProvider(parentProvider);
        }
    }

    internal IReadOnlyList<BaseProcessor<T>> ToReadOnlyList()
    {
        var list = new List<BaseProcessor<T>>();

        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            list.Add(cur.Value);
        }

        return list;
    }

    /// <inheritdoc/>
    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        var result = true;
        var sw = timeoutMilliseconds == Timeout.Infinite
            ? null
            : Stopwatch.StartNew();

        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            if (sw == null)
            {
                result = cur.Value.ForceFlush() && result;
            }
            else
            {
                var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                // 通知所有处理器，即使我们超时
                result = cur.Value.ForceFlush((int)Math.Max(timeout, 0)) && result;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var result = true;
        var sw = timeoutMilliseconds == Timeout.Infinite
            ? null
            : Stopwatch.StartNew();

        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            if (sw == null)
            {
                result = cur.Value.Shutdown() && result;
            }
            else
            {
                var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                // 通知所有处理器，即使我们超时
                result = cur.Value.Shutdown((int)Math.Max(timeout, 0)) && result;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                for (var cur = this.Head; cur != null; cur = cur.Next)
                {
                    try
                    {
                        cur.Value.Dispose();
                    }
                    catch (Exception ex)
                    {
                        OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                    }
                }
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// 双向链表节点类。
    /// </summary>
    internal sealed class DoublyLinkedListNode
    {
        // 节点的值
        public readonly BaseProcessor<T> Value;

        // 构造函数，初始化节点
        public DoublyLinkedListNode(BaseProcessor<T> value)
        {
            this.Value = value;
        }

        // 前一个节点
        public DoublyLinkedListNode? Previous { get; set; }

        // 下一个节点
        public DoublyLinkedListNode? Next { get; set; }
    }
}
