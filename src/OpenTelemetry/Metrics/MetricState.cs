// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 度量状态类，用于管理度量的记录和完成操作。
/// </summary>
internal sealed class MetricState
{
    /// <summary>
    /// 完成度量的操作。
    /// </summary>
    public readonly Action CompleteMeasurement;

    /// <summary>
    /// 记录长整型度量值的操作。
    /// </summary>
    public readonly RecordMeasurementAction<long> RecordMeasurementLong;

    /// <summary>
    /// 记录双精度度量值的操作。
    /// </summary>
    public readonly RecordMeasurementAction<double> RecordMeasurementDouble;

    /// <summary>
    /// MetricState 构造函数。
    /// </summary>
    /// <param name="completeMeasurement">完成度量的操作。</param>
    /// <param name="recordMeasurementLong">记录长整型度量值的操作。</param>
    /// <param name="recordMeasurementDouble">记录双精度度量值的操作。</param>
    private MetricState(
        Action completeMeasurement,
        RecordMeasurementAction<long> recordMeasurementLong,
        RecordMeasurementAction<double> recordMeasurementDouble)
    {
        this.CompleteMeasurement = completeMeasurement;
        this.RecordMeasurementLong = recordMeasurementLong;
        this.RecordMeasurementDouble = recordMeasurementDouble;
    }

    /// <summary>
    /// 记录度量值的委托。
    /// </summary>
    /// <typeparam name="T">度量值的类型。</typeparam>
    /// <param name="value">度量值。</param>
    /// <param name="tags">度量标签。</param>
    internal delegate void RecordMeasurementAction<T>(T value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

    /// <summary>
    /// 为单个度量构建 MetricState。
    /// </summary>
    /// <param name="metric">度量对象。</param>
    /// <returns>构建的 MetricState 对象。</returns>
    public static MetricState BuildForSingleMetric(
        Metric metric)
    {
        Debug.Assert(metric != null, "metric was null");

        return new(
            completeMeasurement: () => MetricReader.DeactivateMetric(metric!),
            recordMeasurementLong: metric!.UpdateLong,
            recordMeasurementDouble: metric!.UpdateDouble);
    }

    /// <summary>
    /// 为度量列表构建 MetricState。
    /// </summary>
    /// <param name="metrics">度量对象列表。</param>
    /// <returns>构建的 MetricState 对象。</returns>
    public static MetricState BuildForMetricList(
        List<Metric> metrics)
    {
        Debug.Assert(metrics != null, "metrics was null");
        Debug.Assert(!metrics.Any(m => m == null), "metrics contained null elements");

        // 注意：使用数组来省略边界检查。
        var metricsArray = metrics!.ToArray();

        return new(
            completeMeasurement: () =>
            {
                for (int i = 0; i < metricsArray.Length; i++)
                {
                    MetricReader.DeactivateMetric(metricsArray[i]);
                }
            },
            recordMeasurementLong: (v, t) =>
            {
                for (int i = 0; i < metricsArray.Length; i++)
                {
                    metricsArray[i].UpdateLong(v, t);
                }
            },
            recordMeasurementDouble: (v, t) =>
            {
                for (int i = 0; i < metricsArray.Length; i++)
                {
                    metricsArray[i].UpdateDouble(v, t);
                }
            });
    }
}
