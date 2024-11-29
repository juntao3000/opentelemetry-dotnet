// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation;

// DiagnosticSourceSubscriber类实现了IDisposable和IObserver<DiagnosticListener>接口，用于订阅DiagnosticSource事件
internal sealed class DiagnosticSourceSubscriber : IDisposable, IObserver<DiagnosticListener>
{
    // 存储所有订阅的监听器
    private readonly List<IDisposable> listenerSubscriptions;
    // 工厂方法，用于创建ListenerHandler实例
    private readonly Func<string, ListenerHandler> handlerFactory;
    // 过滤器，用于筛选DiagnosticListener
    private readonly Func<DiagnosticListener, bool> diagnosticSourceFilter;
    // 可选的过滤器，用于筛选是否启用特定的事件
    private readonly Func<string, object?, object?, bool>? isEnabledFilter;
    // 用于记录未知异常的回调方法
    private readonly Action<string, string, Exception> logUnknownException;
    // 标志位，表示是否已释放资源
    private long disposed;
    // 订阅所有DiagnosticListener的订阅对象
    private IDisposable? allSourcesSubscription;

    // 构造函数，接受一个ListenerHandler实例、一个可选的isEnabledFilter和一个logUnknownException回调
    public DiagnosticSourceSubscriber(
        ListenerHandler handler,
        Func<string, object?, object?, bool>? isEnabledFilter,
        Action<string, string, Exception> logUnknownException)
        : this(_ => handler, value => handler.SourceName == value.Name, isEnabledFilter, logUnknownException)
    {
    }

    // 构造函数，接受一个handlerFactory、一个diagnosticSourceFilter、一个可选的isEnabledFilter和一个logUnknownException回调
    public DiagnosticSourceSubscriber(
        Func<string, ListenerHandler> handlerFactory,
        Func<DiagnosticListener, bool> diagnosticSourceFilter,
        Func<string, object?, object?, bool>? isEnabledFilter,
        Action<string, string, Exception> logUnknownException)
    {
        Guard.ThrowIfNull(handlerFactory);

        this.listenerSubscriptions = [];
        this.handlerFactory = handlerFactory;
        this.diagnosticSourceFilter = diagnosticSourceFilter;
        this.isEnabledFilter = isEnabledFilter;
        this.logUnknownException = logUnknownException;
    }

    // 订阅所有DiagnosticListener
    public void Subscribe()
    {
        this.allSourcesSubscription ??= DiagnosticListener.AllListeners.Subscribe(this);
    }

    // 当有新的DiagnosticListener时调用
    public void OnNext(DiagnosticListener value)
    {
        if ((Interlocked.Read(ref this.disposed) == 0) &&
            this.diagnosticSourceFilter(value))
        {
            var handler = this.handlerFactory(value.Name);
            var listener = new DiagnosticSourceListener(handler, this.logUnknownException);
            var subscription = this.isEnabledFilter == null ?
                value.Subscribe(listener) :
                value.Subscribe(listener, this.isEnabledFilter);

            lock (this.listenerSubscriptions)
            {
                this.listenerSubscriptions.Add(subscription);
            }
        }
    }

    // 当所有DiagnosticListener完成时调用
    public void OnCompleted()
    {
    }

    // 当有错误发生时调用
    public void OnError(Exception error)
    {
    }

    /// <inheritdoc/>
    // 释放资源
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    // 释放资源的具体实现
    private void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref this.disposed, 1, 0) == 1)
        {
            return;
        }

        lock (this.listenerSubscriptions)
        {
            foreach (var listenerSubscription in this.listenerSubscriptions)
            {
                listenerSubscription?.Dispose();
            }

            this.listenerSubscriptions.Clear();
        }

        this.allSourcesSubscription?.Dispose();
        this.allSourcesSubscription = null;
    }
}
