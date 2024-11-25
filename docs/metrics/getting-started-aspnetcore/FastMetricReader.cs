// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace AspNetCoreMetrics;

public class FastMetricReader : BaseExportingMetricReader
{
    public FastMetricReader(BaseExporter<Metric> exporter) : base(exporter)
    {
    }

    protected override bool OnCollect(int timeoutMilliseconds)
    {
        base.OnCollect(timeoutMilliseconds);

        return true;
    }
}
