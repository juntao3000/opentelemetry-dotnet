// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// CompositeMetricReader 不处理添加度量和记录测量。
/// </summary>
internal sealed partial class CompositeMetricReader : MetricReader
{
    // 头节点
    public readonly DoublyLinkedListNode Head;
    // 尾节点
    private DoublyLinkedListNode tail;
    // 是否已释放
    private bool disposed;
    // 计数
    private int count;

    // 构造函数，初始化 CompositeMetricReader
    public CompositeMetricReader(IEnumerable<MetricReader> readers)
    {
        Guard.ThrowIfNull(readers);

        using var iter = readers.GetEnumerator();
        if (!iter.MoveNext())
        {
            throw new ArgumentException($"'{iter}' is null or empty", nameof(iter));
        }

        this.Head = new DoublyLinkedListNode(iter.Current);
        this.tail = this.Head;
        this.count++;

        while (iter.MoveNext())
        {
            this.AddReader(iter.Current);
        }
    }

    // 添加 MetricReader
    public CompositeMetricReader AddReader(MetricReader reader)
    {
        Guard.ThrowIfNull(reader);

        var node = new DoublyLinkedListNode(reader)
        {
            Previous = this.tail,
        };
        this.tail.Next = node;
        this.tail = node;
        this.count++;

        return this;
    }

    // 获取枚举器
    public Enumerator GetEnumerator() => new(this.Head);

    /// <inheritdoc/>
    internal override bool ProcessMetrics(in Batch<Metric> metrics, int timeoutMilliseconds)
    {
        // CompositeMetricReader 将工作委托给其底层读取器，
        // 因此不应调用 CompositeMetricReader.ProcessMetrics。
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    protected override bool OnCollect(int timeoutMilliseconds = Timeout.Infinite)
    {
        var result = true;
        var sw = timeoutMilliseconds == Timeout.Infinite
            ? null
            : Stopwatch.StartNew();

        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            if (sw == null)
            {
                result = cur.Value.Collect(Timeout.Infinite) && result;
            }
            else
            {
                var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                // 通知所有读取器，即使我们超时
                result = cur.Value.Collect((int)Math.Max(timeout, 0)) && result;
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
                result = cur.Value.Shutdown(Timeout.Infinite) && result;
            }
            else
            {
                var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                // 通知所有读取器，即使我们超时
                result = cur.Value.Shutdown((int)Math.Max(timeout, 0)) && result;
            }
        }

        return result;
    }

    // 释放资源
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
                        cur.Value?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.Dispose), ex);
                    }
                }
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    // 枚举器结构
    public struct Enumerator
    {
        // 当前节点
        private DoublyLinkedListNode? node;

        // 构造函数，初始化枚举器
        internal Enumerator(DoublyLinkedListNode node)
        {
            this.node = node;
            this.Current = null;
        }

        // 当前 MetricReader
        [AllowNull]
        public MetricReader Current { get; private set; }

        // 移动到下一个节点
        public bool MoveNext()
        {
            if (this.node != null)
            {
                this.Current = this.node.Value;
                this.node = this.node.Next;
                return true;
            }

            return false;
        }
    }

    // 双向链表节点类
    internal sealed class DoublyLinkedListNode
    {
        // 节点值
        public readonly MetricReader Value;

        // 构造函数，初始化节点
        public DoublyLinkedListNode(MetricReader value)
        {
            this.Value = value;
        }

        // 前一个节点
        public DoublyLinkedListNode? Previous { get; set; }

        // 下一个节点
        public DoublyLinkedListNode? Next { get; set; }
    }
}
