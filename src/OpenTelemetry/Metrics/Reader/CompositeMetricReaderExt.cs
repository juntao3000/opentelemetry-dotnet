// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// CompositeMetricReader 处理添加度量和记录测量值。
/// </summary>
internal sealed partial class CompositeMetricReader
{
    /// <summary>
    /// 添加没有视图配置的度量。
    /// </summary>
    /// <param name="instrument">仪器</param>
    /// <returns>度量列表</returns>
    internal override List<Metric> AddMetricWithNoViews(Instrument instrument)
    {
        // 创建一个新的度量列表，初始容量为 this.count
        var metrics = new List<Metric>(this.count);

        // 遍历链表中的每个节点
        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            // 获取当前节点的度量
            var innerMetrics = cur.Value.AddMetricWithNoViews(instrument);
            if (innerMetrics.Count > 0)
            {
                // 确保没有视图配置时只返回一个度量
                Debug.Assert(innerMetrics.Count == 1, "Multiple metrics returned without view configuration");

                // 将度量添加到列表中
                metrics.AddRange(innerMetrics);
            }
        }

        // 返回度量列表
        return metrics;
    }

    /// <summary>
    /// 添加有视图配置的度量。
    /// </summary>
    /// <param name="instrument">仪器</param>
    /// <param name="metricStreamConfigs">度量流配置列表</param>
    /// <returns>度量列表</returns>
    internal override List<Metric> AddMetricWithViews(Instrument instrument, List<MetricStreamConfiguration?> metricStreamConfigs)
    {
        // 确保 metricStreamConfigs 不为 null
        Debug.Assert(metricStreamConfigs != null, "metricStreamConfigs was null");

        // 创建一个新的度量列表，初始容量为 this.count * metricStreamConfigs.Count
        var metrics = new List<Metric>(this.count * metricStreamConfigs!.Count);

        // 遍历链表中的每个节点
        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            // 获取当前节点的度量
            var innerMetrics = cur.Value.AddMetricWithViews(instrument, metricStreamConfigs);

            // 将度量添加到列表中
            metrics.AddRange(innerMetrics);
        }

        // 返回度量列表
        return metrics;
    }
}
