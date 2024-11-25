// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace AspNetCoreMetrics;

public class CustomMetricReader : BaseExportingMetricReader
{
    private readonly HashSet<string> allowedMetrics;

    public CustomMetricReader(BaseExporter<Metric> exporter, IEnumerable<string> allowedMetrics)
        : base(exporter)
    {
        this.allowedMetrics = new HashSet<string>(allowedMetrics);
    }

    protected override bool OnCollect(int timeoutMilliseconds)
    {
        var metricsToCollect = new List<Metric>();

        foreach (var metric in this.GetMetrics())
        {
            if (this.allowedMetrics.Contains(metric.Name))
            {
                metricsToCollect.Add(metric);
            }
        }

        if (metricsToCollect.Count > 0)
        {
            var batch = new Batch<Metric>(metricsToCollect.ToArray(), metricsToCollect.Count);
            return this.ProcessMetrics(batch, timeoutMilliseconds);
        }

        return false;
    }

    //private IEnumerable<Metric> GetMetrics()
    //{
    //    // 获取所有可用的度量
    //    // 这里假设有一个方法可以获取所有度量
    //    // 你需要根据实际情况实现这个方法
    //    return new List<Metric>();
    //}
}
