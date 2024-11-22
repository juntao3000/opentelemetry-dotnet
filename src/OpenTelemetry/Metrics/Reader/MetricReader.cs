// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// MetricReader 基类。
/// </summary>
public abstract partial class MetricReader : IDisposable
{
    // 未指定的 MetricReaderTemporalityPreference 常量
    private const MetricReaderTemporalityPreference MetricReaderTemporalityPreferenceUnspecified = (MetricReaderTemporalityPreference)0;

    // 累积时间偏好函数
    private static readonly Func<Type, AggregationTemporality> CumulativeTemporalityPreferenceFunc =
        (instrumentType) => AggregationTemporality.Cumulative;

    // 单调增量时间偏好函数
    private static readonly Func<Type, AggregationTemporality> MonotonicDeltaTemporalityPreferenceFunc = (instrumentType) =>
    {
        return instrumentType.GetGenericTypeDefinition() switch
        {
            var type when type == typeof(Counter<>) => AggregationTemporality.Delta,
            var type when type == typeof(ObservableCounter<>) => AggregationTemporality.Delta,
            var type when type == typeof(Histogram<>) => AggregationTemporality.Delta,

            // 暂时性未定义的仪表，不会影响任何事情。
            var type when type == typeof(ObservableGauge<>) => AggregationTemporality.Delta,
            var type when type == typeof(Gauge<>) => AggregationTemporality.Delta,

            var type when type == typeof(UpDownCounter<>) => AggregationTemporality.Cumulative,
            var type when type == typeof(ObservableUpDownCounter<>) => AggregationTemporality.Cumulative,

            // TODO: 考虑在此处记录日志，因为不应落入此情况。
            _ => AggregationTemporality.Delta,
        };
    };

    // 新任务锁
    private readonly Lock newTaskLock = new();
    // 收集锁
    private readonly Lock onCollectLock = new();
    // 关闭任务完成源
    private readonly TaskCompletionSource<bool> shutdownTcs = new();
    // 时间偏好
    private MetricReaderTemporalityPreference temporalityPreference = MetricReaderTemporalityPreferenceUnspecified;
    // 时间函数
    private Func<Type, AggregationTemporality> temporalityFunc = CumulativeTemporalityPreferenceFunc;
    // 关闭计数
    private int shutdownCount;
    // 收集任务完成源
    private TaskCompletionSource<bool>? collectionTcs;
    // 父提供者
    private BaseProvider? parentProvider;

    /// <summary>
    /// 获取或设置度量读取器的时间偏好。
    /// </summary>
    public MetricReaderTemporalityPreference TemporalityPreference
    {
        get
        {
            if (this.temporalityPreference == MetricReaderTemporalityPreferenceUnspecified)
            {
                this.temporalityPreference = MetricReaderTemporalityPreference.Cumulative;
            }

            return this.temporalityPreference;
        }

        set
        {
            if (this.temporalityPreference != MetricReaderTemporalityPreferenceUnspecified)
            {
                throw new NotSupportedException($"The temporality preference cannot be modified (the current value is {this.temporalityPreference}).");
            }

            this.temporalityPreference = value;
            this.temporalityFunc = value switch
            {
                MetricReaderTemporalityPreference.Delta => MonotonicDeltaTemporalityPreferenceFunc,
                _ => CumulativeTemporalityPreferenceFunc,
            };
        }
    }

    /// <summary>
    /// 尝试收集度量，阻塞当前线程直到度量收集完成、关闭信号或超时。
    /// 如果涉及异步仪表，它们的回调函数将被触发。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或<c>Timeout.Infinite</c>表示无限等待。
    /// </param>
    /// <returns>
    /// 返回<c>true</c>表示度量收集成功；否则返回<c>false</c>。
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 当<c>timeoutMilliseconds</c>小于-1时抛出。
    /// </exception>
    /// <remarks>
    /// 此函数保证线程安全。如果同时发生多个调用，它们可能会折叠并导致较少的<c>OnCollect</c>回调调用，以提高性能，只要语义可以保留。
    /// </remarks>
    public bool Collect(int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.Collect 方法被调用。");
        var shouldRunCollect = false;
        var tcs = this.collectionTcs;

        if (tcs == null)
        {
            lock (this.newTaskLock)
            {
                tcs = this.collectionTcs;

                if (tcs == null)
                {
                    shouldRunCollect = true;
                    tcs = new TaskCompletionSource<bool>();
                    this.collectionTcs = tcs;
                }
            }
        }

        if (!shouldRunCollect)
        {
            return Task.WaitAny(tcs.Task, this.shutdownTcs.Task, Task.Delay(timeoutMilliseconds)) == 0 && tcs.Task.Result;
        }

        var result = false;
        try
        {
            lock (this.onCollectLock)
            {
                this.collectionTcs = null;
                result = this.OnCollect(timeoutMilliseconds);
            }
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.Collect), ex);
        }

        tcs.TrySetResult(result);

        if (result)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.Collect 成功。");
        }
        else
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.Collect 失败。");
        }

        return result;
    }

    /// <summary>
    /// 尝试关闭处理器，阻塞当前线程直到关闭完成或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或<c>Timeout.Infinite</c>表示无限等待。
    /// </param>
    /// <returns>
    /// 返回<c>true</c>表示关闭成功；否则返回<c>false</c>。
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 当<c>timeoutMilliseconds</c>小于-1时抛出。
    /// </exception>
    /// <remarks>
    /// 此函数保证线程安全。只有第一次调用会生效，后续调用将无效。
    /// </remarks>
    public bool Shutdown(int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.Shutdown 被调用。");

        if (Interlocked.CompareExchange(ref this.shutdownCount, 1, 0) != 0)
        {
            return false; // 已经调用过关闭
        }

        var result = false;
        try
        {
            result = this.OnShutdown(timeoutMilliseconds);
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.Shutdown), ex);
        }

        this.shutdownTcs.TrySetResult(result);

        if (result)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.Shutdown 成功。");
        }
        else
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.Shutdown 失败。");
        }

        return result;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 设置父提供者。
    /// </summary>
    /// <param name="parentProvider">父提供者。</param>
    internal virtual void SetParentProvider(BaseProvider parentProvider)
    {
        this.parentProvider = parentProvider;
    }

    /// <summary>
    /// 处理一批度量。
    /// </summary>
    /// <param name="metrics">要处理的一批度量。</param>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或<c>Timeout.Infinite</c>表示无限等待。
    /// </param>
    /// <returns>
    /// 返回<c>true</c>表示度量处理成功；否则返回<c>false</c>。
    /// </returns>
    internal virtual bool ProcessMetrics(in Batch<Metric> metrics, int timeoutMilliseconds)
    {
        return true;
    }

    /// <summary>
    /// 由<c>Collect</c>调用。此函数应阻塞当前线程直到度量收集完成、关闭信号或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或<c>Timeout.Infinite</c>表示无限等待。
    /// </param>
    /// <returns>
    /// 返回<c>true</c>表示度量收集成功；否则返回<c>false</c>。
    /// </returns>
    /// <remarks>
    /// 此函数在调用<c>Collect</c>的线程上同步调用。此函数不应抛出异常。
    /// </remarks>
    protected virtual bool OnCollect(int timeoutMilliseconds)
    {
        OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.OnCollect 被调用。");

        var sw = timeoutMilliseconds == Timeout.Infinite
            ? null
            : Stopwatch.StartNew();

        var meterProviderSdk = this.parentProvider as MeterProviderSdk;
        meterProviderSdk?.CollectObservableInstruments();

        OpenTelemetrySdkEventSource.Log.MetricReaderEvent("可观察仪表已收集。");

        var metrics = this.GetMetricsBatch();

        bool result;
        if (sw == null)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderEvent("ProcessMetrics 被调用。");
            result = this.ProcessMetrics(metrics, Timeout.Infinite);
            if (result)
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent("ProcessMetrics 成功。");
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent("ProcessMetrics 失败。");
            }

            return result;
        }
        else
        {
            var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

            if (timeout <= 0)
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent("OnCollect 失败，超时时间已过。");
                return false;
            }

            OpenTelemetrySdkEventSource.Log.MetricReaderEvent("ProcessMetrics 被调用。");
            result = this.ProcessMetrics(metrics, (int)timeout);
            if (result)
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent("ProcessMetrics 成功。");
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent("ProcessMetrics 失败。");
            }

            return result;
        }
    }

    /// <summary>
    /// 由<c>Shutdown</c>调用。此函数应阻塞当前线程直到关闭完成或超时。
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或<c>Timeout.Infinite</c>表示无限等待。
    /// </param>
    /// <returns>
    /// 返回<c>true</c>表示关闭成功；否则返回<c>false</c>。
    /// </returns>
    /// <remarks>
    /// 此函数在第一次调用<c>Shutdown</c>的线程上同步调用。此函数不应抛出异常。
    /// </remarks>
    protected virtual bool OnShutdown(int timeoutMilliseconds)
    {
        return this.Collect(timeoutMilliseconds);
    }

    /// <summary>
    /// 释放此类使用的非托管资源，并可选择释放托管资源。
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/>表示释放托管和非托管资源；
    /// <see langword="false"/>表示仅释放非托管资源。
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
