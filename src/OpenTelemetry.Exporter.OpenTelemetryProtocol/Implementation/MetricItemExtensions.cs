// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Metrics;
using OpenTelemetry.Proto.Metrics.V1;
using AggregationTemporality = OpenTelemetry.Metrics.AggregationTemporality;
using Metric = OpenTelemetry.Metrics.Metric;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpMetrics = OpenTelemetry.Proto.Metrics.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

// MetricItemExtensions类：提供扩展方法用于处理度量项
internal static class MetricItemExtensions
{
    // MetricListPool：用于存储ScopeMetrics对象的并发集合
    private static readonly ConcurrentBag<ScopeMetrics> MetricListPool = new();

    // AddMetrics方法：将度量数据添加到请求中
    internal static void AddMetrics(
        this OtlpCollector.ExportMetricsServiceRequest request,
        OtlpResource.Resource processResource,
        in Batch<Metric> metrics)
    {
        // metricsByLibrary：按库名分组的度量数据字典
        var metricsByLibrary = new Dictionary<string, ScopeMetrics>();
        // resourceMetrics：资源度量数据
        var resourceMetrics = new ResourceMetrics
        {
            Resource = processResource,
        };
        request.ResourceMetrics.Add(resourceMetrics);

        // 遍历度量数据
        foreach (var metric in metrics)
        {
            var otlpMetric = metric.ToOtlpMetric();

            // TODO: 用异常处理替换空检查
            if (otlpMetric == null)
            {
                OpenTelemetryProtocolExporterEventSource.Log.CouldNotTranslateMetric(
                    nameof(MetricItemExtensions),
                    nameof(AddMetrics));
                continue;
            }

            var meterName = metric.MeterName;
            if (!metricsByLibrary.TryGetValue(meterName, out var scopeMetrics))
            {
                scopeMetrics = GetMetricListFromPool(meterName, metric.MeterVersion, metric.MeterTags);

                metricsByLibrary.Add(meterName, scopeMetrics);
                resourceMetrics.ScopeMetrics.Add(scopeMetrics);
            }

            scopeMetrics.Metrics.Add(otlpMetric);
        }
    }

    // Return方法：将请求中的度量数据返回到池中
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Return(this OtlpCollector.ExportMetricsServiceRequest request)
    {
        //Return 方法的目的是为了提高性能。具体来说，它通过对象池（ConcurrentBag < ScopeMetrics >）来重用 ScopeMetrics 对象，从而减少了频繁创建和销毁对象所带来的性能开销。

        // 获取第一个资源度量数据
        var resourceMetrics = request.ResourceMetrics.FirstOrDefault();
        if (resourceMetrics == null)
        {
            return;
        }

        // 遍历ScopeMetrics并清空度量数据和属性
        foreach (var scopeMetrics in resourceMetrics.ScopeMetrics)
        {
            scopeMetrics.Metrics.Clear();
            scopeMetrics.Scope.Attributes.Clear();
            // 将ScopeMetrics对象返回到池中
            MetricListPool.Add(scopeMetrics);
        }
    }

    // GetMetricListFromPool方法：从池中获取ScopeMetrics对象
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ScopeMetrics GetMetricListFromPool(string name, string version, IEnumerable<KeyValuePair<string, object?>>? meterTags)
    {
        // 尝试从池中获取ScopeMetrics对象
        if (!MetricListPool.TryTake(out var scopeMetrics))
        {
            // 如果池中没有可用的ScopeMetrics对象，则创建一个新的
            scopeMetrics = new ScopeMetrics
            {
                Scope = new OtlpCommon.InstrumentationScope
                {
                    Name = name, // Name被强制不能为空，但可以为空字符串
                    Version = version ?? string.Empty, // proto抛出的NRE
                },
            };

            // 如果meterTags不为空，则添加范围属性
            if (meterTags != null)
            {
                AddScopeAttributes(meterTags, scopeMetrics.Scope.Attributes);
            }
        }
        else
        {
            // 如果从池中获取到了ScopeMetrics对象，则更新其属性
            scopeMetrics.Scope.Name = name;
            scopeMetrics.Scope.Version = version ?? string.Empty;
            if (meterTags != null)
            {
                AddScopeAttributes(meterTags, scopeMetrics.Scope.Attributes);
            }
        }

        return scopeMetrics;
    }

    // ToOtlpMetric方法：将Metric对象转换为OtlpMetrics.Metric对象
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OtlpMetrics.Metric ToOtlpMetric(this Metric metric)
    {
        // 创建OtlpMetrics.Metric对象并设置其名称
        var otlpMetric = new OtlpMetrics.Metric
        {
            Name = metric.Name,
        };

        // 如果描述不为空，则设置描述
        if (metric.Description != null)
        {
            otlpMetric.Description = metric.Description;
        }

        // 如果单位不为空，则设置单位
        if (metric.Unit != null)
        {
            otlpMetric.Unit = metric.Unit;
        }

        // 设置聚合时间类型
        OtlpMetrics.AggregationTemporality temporality;
        if (metric.Temporality == AggregationTemporality.Delta)
        {
            temporality = OtlpMetrics.AggregationTemporality.Delta;
        }
        else
        {
            temporality = OtlpMetrics.AggregationTemporality.Cumulative;
        }

        // 根据度量类型转换度量数据
        switch (metric.MetricType)
        {
            case MetricType.LongSum:
            case MetricType.LongSumNonMonotonic:
                {
                    var sum = new Sum
                    {
                        IsMonotonic = metric.MetricType == MetricType.LongSum,
                        AggregationTemporality = temporality,
                    };

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new NumberDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);

                        dataPoint.AsInt = metricPoint.GetSumLong();

                        if (metricPoint.TryGetExemplars(out var exemplars))
                        {
                            foreach (ref readonly var exemplar in exemplars)
                            {
                                dataPoint.Exemplars.Add(
                                    ToOtlpExemplar(exemplar.LongValue, in exemplar));
                            }
                        }

                        sum.DataPoints.Add(dataPoint);
                    }

                    otlpMetric.Sum = sum;
                    break;
                }

            case MetricType.DoubleSum:
            case MetricType.DoubleSumNonMonotonic:
                {
                    var sum = new Sum
                    {
                        IsMonotonic = metric.MetricType == MetricType.DoubleSum,
                        AggregationTemporality = temporality,
                    };

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new NumberDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);

                        dataPoint.AsDouble = metricPoint.GetSumDouble();

                        if (metricPoint.TryGetExemplars(out var exemplars))
                        {
                            foreach (ref readonly var exemplar in exemplars)
                            {
                                dataPoint.Exemplars.Add(
                                    ToOtlpExemplar(exemplar.DoubleValue, in exemplar));
                            }
                        }

                        sum.DataPoints.Add(dataPoint);
                    }

                    otlpMetric.Sum = sum;
                    break;
                }

            case MetricType.LongGauge:
                {
                    var gauge = new Gauge();
                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new NumberDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);

                        dataPoint.AsInt = metricPoint.GetGaugeLastValueLong();

                        if (metricPoint.TryGetExemplars(out var exemplars))
                        {
                            foreach (ref readonly var exemplar in exemplars)
                            {
                                dataPoint.Exemplars.Add(
                                    ToOtlpExemplar(exemplar.LongValue, in exemplar));
                            }
                        }

                        gauge.DataPoints.Add(dataPoint);
                    }

                    otlpMetric.Gauge = gauge;
                    break;
                }

            case MetricType.DoubleGauge:
                {
                    var gauge = new Gauge();
                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new NumberDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);

                        dataPoint.AsDouble = metricPoint.GetGaugeLastValueDouble();

                        if (metricPoint.TryGetExemplars(out var exemplars))
                        {
                            foreach (ref readonly var exemplar in exemplars)
                            {
                                dataPoint.Exemplars.Add(
                                    ToOtlpExemplar(exemplar.DoubleValue, in exemplar));
                            }
                        }

                        gauge.DataPoints.Add(dataPoint);
                    }

                    otlpMetric.Gauge = gauge;
                    break;
                }

            case MetricType.Histogram:
                {
                    var histogram = new Histogram
                    {
                        AggregationTemporality = temporality,
                    };

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new HistogramDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);
                        dataPoint.Count = (ulong)metricPoint.GetHistogramCount();
                        dataPoint.Sum = metricPoint.GetHistogramSum();

                        if (metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
                        {
                            dataPoint.Min = min;
                            dataPoint.Max = max;
                        }

                        foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
                        {
                            dataPoint.BucketCounts.Add((ulong)histogramMeasurement.BucketCount);
                            if (histogramMeasurement.ExplicitBound != double.PositiveInfinity)
                            {
                                dataPoint.ExplicitBounds.Add(histogramMeasurement.ExplicitBound);
                            }
                        }

                        if (metricPoint.TryGetExemplars(out var exemplars))
                        {
                            foreach (ref readonly var exemplar in exemplars)
                            {
                                dataPoint.Exemplars.Add(
                                    ToOtlpExemplar(exemplar.DoubleValue, in exemplar));
                            }
                        }

                        histogram.DataPoints.Add(dataPoint);
                    }

                    otlpMetric.Histogram = histogram;
                    break;
                }

            case MetricType.ExponentialHistogram:
                {
                    var histogram = new ExponentialHistogram
                    {
                        AggregationTemporality = temporality,
                    };

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new ExponentialHistogramDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);
                        dataPoint.Count = (ulong)metricPoint.GetHistogramCount();
                        dataPoint.Sum = metricPoint.GetHistogramSum();

                        if (metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
                        {
                            dataPoint.Min = min;
                            dataPoint.Max = max;
                        }

                        var exponentialHistogramData = metricPoint.GetExponentialHistogramData();
                        dataPoint.Scale = exponentialHistogramData.Scale;
                        dataPoint.ZeroCount = (ulong)exponentialHistogramData.ZeroCount;

                        dataPoint.Positive = new ExponentialHistogramDataPoint.Types.Buckets();
                        dataPoint.Positive.Offset = exponentialHistogramData.PositiveBuckets.Offset;
                        foreach (var bucketCount in exponentialHistogramData.PositiveBuckets)
                        {
                            dataPoint.Positive.BucketCounts.Add((ulong)bucketCount);
                        }

                        if (metricPoint.TryGetExemplars(out var exemplars))
                        {
                            foreach (ref readonly var exemplar in exemplars)
                            {
                                dataPoint.Exemplars.Add(
                                    ToOtlpExemplar(exemplar.DoubleValue, in exemplar));
                            }
                        }

                        histogram.DataPoints.Add(dataPoint);
                    }

                    otlpMetric.ExponentialHistogram = histogram;
                    break;
                }
        }

        return otlpMetric;
    }

    // ToOtlpExemplar方法：将Exemplar对象转换为OtlpMetrics.Exemplar对象
    internal static OtlpMetrics.Exemplar ToOtlpExemplar<T>(T value, in Metrics.Exemplar exemplar)
        where T : struct
    {
        // 创建OtlpMetrics.Exemplar对象并设置时间戳
        var otlpExemplar = new OtlpMetrics.Exemplar
        {
            TimeUnixNano = (ulong)exemplar.Timestamp.ToUnixTimeNanoseconds(),
        };

        // 如果TraceId不为空，则设置TraceId和SpanId
        if (exemplar.TraceId != default)
        {
            byte[] traceIdBytes = new byte[16];
            exemplar.TraceId.CopyTo(traceIdBytes);

            byte[] spanIdBytes = new byte[8];
            exemplar.SpanId.CopyTo(spanIdBytes);

            otlpExemplar.TraceId = UnsafeByteOperations.UnsafeWrap(traceIdBytes);
            otlpExemplar.SpanId = UnsafeByteOperations.UnsafeWrap(spanIdBytes);
        }

        // 根据类型设置值
        if (typeof(T) == typeof(long))
        {
            otlpExemplar.AsInt = (long)(object)value;
        }
        else if (typeof(T) == typeof(double))
        {
            otlpExemplar.AsDouble = (double)(object)value;
        }
        else
        {
            Debug.Fail("Unexpected type");
            otlpExemplar.AsDouble = Convert.ToDouble(value);
        }

        // 设置过滤后的属性
        var otlpExemplarFilteredAttributes = otlpExemplar.FilteredAttributes;

        foreach (var tag in exemplar.FilteredTags)
        {
            OtlpTagWriter.Instance.TryWriteTag(ref otlpExemplarFilteredAttributes, tag);
        }

        return otlpExemplar;
    }

    // AddAttributes方法：将标签添加到属性集合中
    private static void AddAttributes(ReadOnlyTagCollection tags, RepeatedField<OtlpCommon.KeyValue> attributes)
    {
        foreach (var tag in tags)
        {
            OtlpTagWriter.Instance.TryWriteTag(ref attributes, tag);
        }
    }

    // AddScopeAttributes方法：将范围标签添加到属性集合中
    private static void AddScopeAttributes(IEnumerable<KeyValuePair<string, object?>> meterTags, RepeatedField<OtlpCommon.KeyValue> attributes)
    {
        foreach (var tag in meterTags)
        {
            OtlpTagWriter.Instance.TryWriteTag(ref attributes, tag);
        }
    }
}
