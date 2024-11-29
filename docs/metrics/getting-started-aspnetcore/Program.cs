// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace AspNetCoreMetrics;

public class Program
{
    private static readonly Meter MyMeter = new("MyCompany.MyProduct.MyLibrary", "1.0");
    private static readonly Counter<long> MyFruitCounter = MyMeter.CreateCounter<long>("MyFruitCounter");
    private static readonly UpDownCounter<long> MyCounter = MyMeter.CreateUpDownCounter<long>("MyCounter");

    static Program()
    {
        var process = Process.GetCurrentProcess();

        //MyMeter.CreateObservableCounter("Thread.CpuTime", () => GetThreadCpuTime(process), "ms");
        //MyMeter.CreateObservableGauge("Thread.State", () => GetThreadState(process));

        //MyMeter.CreateObservableCounter("MyTempCounter", () =>
        //{
        //    //myCounterValue++;
        //    return myCounterValue;
        //});
    }

    private static IEnumerable<Measurement<double>> GetThreadCpuTime(Process process)
    {
        foreach (ProcessThread thread in process.Threads)
        {
            yield return new(thread.TotalProcessorTime.TotalMilliseconds, new("ProcessId", process.Id), new("ThreadId", thread.Id));
        }
    }

    private static IEnumerable<Measurement<int>> GetThreadState(Process process)
    {
        foreach (ProcessThread thread in process.Threads)
        {
            yield return new((int)thread.ThreadState, new("ProcessId", process.Id), new("ThreadId", thread.Id));
        }
    }

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var exporter = new OtlpMetricExporter(new OtlpExporterOptions());
        var reader = new BaseExportingMetricReader(exporter);


        // Configure OpenTelemetry with metrics and auto-start.
        builder.Services.AddOpenTelemetry()
        //.ConfigureResource(resource => resource.AddService(serviceName: builder.Environment.ApplicationName))
        .WithMetrics(metrics => metrics
            //.ConfigureResource(cfg => { })
            //.AddAspNetCoreInstrumentation()
            //.AddHttpClientInstrumentation()
            //.AddProcessInstrumentation()
            //.AddRuntimeInstrumentation()
            //.AddEventCountersInstrumentation()
            //.AddMeter("MyCompany.MyProduct.MyLibrary")
            .AddReader(new PeriodicExportingMetricReader(new MyConsoleMetricExporter("normal"), 3000))
            //.AddReader(reader)
            //.AddConsoleExporter((exporterOptions, metricReaderOptions) =>
            //{
            //    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
            //})
            //.AddConsoleExporter((exporterOptions, metricReaderOptions) =>
            //{
            //    //metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
            //})
            .AddOtlpExporter(otlpExporterOptions =>
            {
                otlpExporterOptions.Endpoint = new Uri("http://localhost:4317");
            })
            );

        var fastMetricReader = new FastMetricReader(new OtlpMetricExporter(new OtlpExporterOptions()), 0);
        var fastMeterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .AddMyInstrumentation()
            .AddReader(fastMetricReader);
        fastMeterProviderBuilder.ConfigureServices(tmpServices =>
        {
            foreach (var tmpService in tmpServices)
            {
                if (tmpService == null)
                {
                    continue;
                }

                if (tmpService.ServiceType.CustomAttributes.Any(x => x.AttributeType == typeof(FastInstrumentationAttribute)))
                {
                    builder.Services.Add(tmpService);
                }
            }
        });

        builder.Services.AddSingleton<IFastMetricReader>(fastMetricReader);
        using var fastMeterProvider = fastMeterProviderBuilder.Build();

        var app = builder.Build();


        app.MapGet("/", () => $"Hello from OpenTelemetry Metrics!");

        app.MapGet("/t1", () =>
        {
            MyFruitCounter.Add(1, new("name", "apple"), new("color", "red"));
            return "t1";
        });

        app.MapGet("/t2", (long delta = 1) =>
        {
            MyCounter.Add(delta);

            reader.Collect();
            return "t2";
        });

        app.MapGet("/test", ([FromServices] MyInstrumentation instrumentation) =>
        {
            instrumentation.MyTestCounter11.Add(1);

            return "test";
        });

        app.MapGet("/test2", ([FromServices] MyInstrumentation instrumentation, [FromServices] IFastMetricReader fastReader) =>
        {
            instrumentation.MyTestCounter11.Add(2);
            fastReader.Collect();

            return "test2";
        });

        app.Run();
    }
}
