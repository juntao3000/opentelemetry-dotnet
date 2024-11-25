// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.Serialization;
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
        MyMeter.CreateObservableGauge("Thread.State", () => GetThreadState(process));

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

        var exporter = new FilteredOtlpMetricExporter(new OtlpExporterOptions(), metric =>
        {
            return true;
        });
        var reader = new BaseExportingMetricReader(exporter);


        //MyMeter

        // Configure OpenTelemetry with metrics and auto-start.
        builder.Services.AddOpenTelemetry()
        //.ConfigureResource(resource => resource.AddService(serviceName: builder.Environment.ApplicationName))
        .WithMetrics(metrics => metrics
            .ConfigureResource(cfg => { })
            //.AddAspNetCoreInstrumentation()
            .AddMeter("MyCompany.MyProduct.MyLibrary")
            .AddReader(new PeriodicExportingMetricReader(new MyConsoleMetricExporter("11111"), 1000))
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

        app.Run();
    }
}
