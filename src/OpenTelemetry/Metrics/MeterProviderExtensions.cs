// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 包含 <see cref="MeterProvider"/> 类的扩展方法。
/// </summary>
public static class MeterProviderExtensions
{
    /// <summary>
    /// 刷新所有注册在 MeterProviderSdk 下的读取器，阻塞当前线程直到刷新完成、关闭信号或超时。
    /// </summary>
    /// <param name="provider">将调用 ForceFlush 的 MeterProviderSdk 实例。</param>
    /// <param name="timeoutMilliseconds">
    /// 等待的毫秒数（非负），或<c>Timeout.Infinite</c>表示无限等待。
    /// </param>
    /// <returns>
    /// 返回<c>true</c>表示强制刷新成功；否则返回<c>false</c>。
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 当<c>timeoutMilliseconds</c>小于-1时抛出。
    /// </exception>
    /// <remarks>
    /// 此函数保证线程安全。
    /// </remarks>
    public static bool ForceFlush(this MeterProvider provider, int timeoutMilliseconds = Timeout.Infinite)
    {
        // 检查 provider 是否为 null
        Guard.ThrowIfNull(provider);
        // 检查 timeoutMilliseconds 是否有效
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        // 如果 provider 是 MeterProviderSdk 类型
        if (provider is MeterProviderSdk meterProviderSdk)
        {
            try
            {
                // 调用 OnForceFlush 方法并返回结果
                return meterProviderSdk.OnForceFlush(timeoutMilliseconds);
            }
            catch (Exception ex)
            {
                // 记录异常
                OpenTelemetrySdkEventSource.Log.MeterProviderException(nameof(meterProviderSdk.OnForceFlush), ex);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 尝试关闭 MeterProviderSdk，阻塞当前线程直到关闭完成或超时。
    /// </summary>
    /// <param name="provider">将调用 Shutdown 的 MeterProviderSdk 实例。</param>
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
    public static bool Shutdown(this MeterProvider provider, int timeoutMilliseconds = Timeout.Infinite)
    {
        // 检查 provider 是否为 null
        Guard.ThrowIfNull(provider);
        // 检查 timeoutMilliseconds 是否有效
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        // 如果 provider 是 MeterProviderSdk 类型
        if (provider is MeterProviderSdk meterProviderSdk)
        {
            // 增加 ShutdownCount，如果大于1，表示已经调用过 shutdown
            if (Interlocked.Increment(ref meterProviderSdk.ShutdownCount) > 1)
            {
                return false; // shutdown 已经被调用
            }

            try
            {
                // 调用 OnShutdown 方法并返回结果
                return meterProviderSdk.OnShutdown(timeoutMilliseconds);
            }
            catch (Exception ex)
            {
                // 记录异常
                OpenTelemetrySdkEventSource.Log.MeterProviderException(nameof(meterProviderSdk.OnShutdown), ex);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 从 provider 中查找给定类型的 Metric 导出器。
    /// </summary>
    /// <typeparam name="T">导出器的类型。</typeparam>
    /// <param name="provider">要从中查找导出器的 MeterProvider。</param>
    /// <param name="exporter">导出器实例。</param>
    /// <returns>如果找到指定类型的导出器，则返回 true；否则返回 false。</returns>
    internal static bool TryFindExporter<T>(
        this MeterProvider provider,
        [NotNullWhen(true)]
        out T? exporter)
        where T : BaseExporter<Metric>
    {
        // 如果 provider 是 MeterProviderSdk 类型
        if (provider is MeterProviderSdk meterProviderSdk)
        {
            // 尝试从 Reader 中查找导出器
            return TryFindExporter(meterProviderSdk.Reader, out exporter);
        }

        exporter = null;
        return false;

        // 从 MetricReader 中查找导出器
        static bool TryFindExporter(MetricReader? reader, out T? exporter)
        {
            // 如果 reader 是 BaseExportingMetricReader 类型
            if (reader is BaseExportingMetricReader exportingMetricReader)
            {
                // 尝试将导出器转换为 T 类型
                exporter = exportingMetricReader.Exporter as T;
                return exporter != null;
            }

            // 如果 reader 是 CompositeMetricReader 类型
            if (reader is CompositeMetricReader compositeMetricReader)
            {
                // 遍历子读取器
                foreach (MetricReader childReader in compositeMetricReader)
                {
                    // 递归查找导出器
                    if (TryFindExporter(childReader, out exporter))
                    {
                        return true;
                    }
                }
            }

            exporter = null;
            return false;
        }
    }
}
