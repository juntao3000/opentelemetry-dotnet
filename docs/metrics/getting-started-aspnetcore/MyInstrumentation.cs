// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using System.Diagnostics.Metrics;

namespace AspNetCoreMetrics;

internal class MyInstrumentation : IDisposable
{
    internal const string MeterName = "Dida.Test.Instrumentation";
    internal const string Version = "1.0.0";

    // Meter 类负责创建和跟踪指标
    private readonly Meter meter;

    // 自定义指标1
    public Counter<long> MyTestCounter11 { get; }

    // 自定义指标2
    public Counter<long> MyTestCounter12 { get; }

    public MyInstrumentation()
    {
        meter = new Meter(MeterName, Version);

        MyTestCounter11 = meter.CreateCounter<long>("my.test.count.11", description: "The test count 11.");
        MyTestCounter12 = meter.CreateCounter<long>("my.test.count.12", description: "The test count 12.");
    }

    public void Dispose()
    {
        meter.Dispose();
    }
}

public static class MeterProviderBuilderExtensions
{
    public static MeterProviderBuilder AddMyInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // 登记 Meter
        builder.AddMeter(MyInstrumentation.MeterName);

        var instrumentation = new MyInstrumentation();

        // 注入到 DI 容器，以便在需要时从 DI 容器中获取
        builder.ConfigureServices(s =>
        {
            if (s == null)
            {
                return;
            }

            s.AddSingleton(instrumentation);
        });

        return builder.AddInstrumentation(() => instrumentation);
    }
}
