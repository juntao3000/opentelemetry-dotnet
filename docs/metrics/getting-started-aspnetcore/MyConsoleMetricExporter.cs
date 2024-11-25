// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace AspNetCoreMetrics;

public class MyConsoleMetricExporter : BaseExporter<Metric>
{
    private readonly string name;

    public MyConsoleMetricExporter(string? name = null)
    {
        this.name = name ?? string.Empty;
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        foreach (var metric in batch)
        {
            var msg = new StringBuilder();
            msg.AppendLine();
            msg.Append($"[{name}]{metric.Name}");

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                string valueDisplay = string.Empty;

                var tagsBuilder = new StringBuilder();
                //foreach (var tag in metricPoint.Tags)
                //{
                //    if (this.TagWriter.TryTransformTag(tag, out var result))
                //    {
                //        tagsBuilder.Append($"{result.Key}: {result.Value}");
                //        tagsBuilder.Append(' ');
                //    }
                //}
                var tags = tagsBuilder.ToString().TrimEnd();

                var metricType = metric.MetricType;

                if (metricType == MetricType.Histogram || metricType == MetricType.ExponentialHistogram)
                {
                    var bucketsBuilder = new StringBuilder();
                    var sum = metricPoint.GetHistogramSum();
                    var count = metricPoint.GetHistogramCount();
                    bucketsBuilder.Append($"Sum: {sum} Count: {count} ");
                    if (metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
                    {
                        bucketsBuilder.Append($"Min: {min} Max: {max} ");
                    }

                    bucketsBuilder.AppendLine();

                    if (metricType == MetricType.Histogram)
                    {
                        bool isFirstIteration = true;
                        double previousExplicitBound = default;
                        foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
                        {
                            if (isFirstIteration)
                            {
                                bucketsBuilder.Append("(-Infinity,");
                                bucketsBuilder.Append(histogramMeasurement.ExplicitBound);
                                bucketsBuilder.Append(']');
                                bucketsBuilder.Append(':');
                                bucketsBuilder.Append(histogramMeasurement.BucketCount);
                                previousExplicitBound = histogramMeasurement.ExplicitBound;
                                isFirstIteration = false;
                            }
                            else
                            {
                                bucketsBuilder.Append('(');
                                bucketsBuilder.Append(previousExplicitBound);
                                bucketsBuilder.Append(',');
                                if (histogramMeasurement.ExplicitBound != double.PositiveInfinity)
                                {
                                    bucketsBuilder.Append(histogramMeasurement.ExplicitBound);
                                    previousExplicitBound = histogramMeasurement.ExplicitBound;
                                }
                                else
                                {
                                    bucketsBuilder.Append("+Infinity");
                                }

                                bucketsBuilder.Append(']');
                                bucketsBuilder.Append(':');
                                bucketsBuilder.Append(histogramMeasurement.BucketCount);
                            }

                            bucketsBuilder.AppendLine();
                        }
                    }
                    else
                    {
                        var exponentialHistogramData = metricPoint.GetExponentialHistogramData();
                        var scale = exponentialHistogramData.Scale;

                        if (exponentialHistogramData.ZeroCount != 0)
                        {
                            bucketsBuilder.AppendLine($"Zero Bucket:{exponentialHistogramData.ZeroCount}");
                        }

                        //var offset = exponentialHistogramData.PositiveBuckets.Offset;
                        //foreach (var bucketCount in exponentialHistogramData.PositiveBuckets)
                        //{
                        //    var lowerBound = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(offset, scale).ToString(CultureInfo.InvariantCulture);
                        //    var upperBound = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(++offset, scale).ToString(CultureInfo.InvariantCulture);
                        //    bucketsBuilder.AppendLine($"({lowerBound}, {upperBound}]:{bucketCount}");
                        //}
                    }

                    valueDisplay = bucketsBuilder.ToString();
                }
                else if (metricType.IsDouble())
                {
                    if (metricType.IsSum())
                    {
                        valueDisplay = metricPoint.GetSumDouble().ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        valueDisplay = metricPoint.GetGaugeLastValueDouble().ToString(CultureInfo.InvariantCulture);
                    }
                }
                else if (metricType.IsLong())
                {
                    if (metricType.IsSum())
                    {
                        valueDisplay = metricPoint.GetSumLong().ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        valueDisplay = metricPoint.GetGaugeLastValueLong().ToString(CultureInfo.InvariantCulture);
                    }
                }

                var exemplarString = new StringBuilder();
                if (metricPoint.TryGetExemplars(out var exemplars))
                {
                    foreach (ref readonly var exemplar in exemplars)
                    {
                        exemplarString.Append("Timestamp: ");
                        exemplarString.Append(exemplar.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                        if (metricType.IsDouble())
                        {
                            exemplarString.Append(" Value: ");
                            exemplarString.Append(exemplar.DoubleValue);
                        }
                        else if (metricType.IsLong())
                        {
                            exemplarString.Append(" Value: ");
                            exemplarString.Append(exemplar.LongValue);
                        }

                        if (exemplar.TraceId != default)
                        {
                            exemplarString.Append(" TraceId: ");
                            exemplarString.Append(exemplar.TraceId.ToHexString());
                            exemplarString.Append(" SpanId: ");
                            exemplarString.Append(exemplar.SpanId.ToHexString());
                        }

                        //bool appendedTagString = false;
                        //foreach (var tag in exemplar.FilteredTags)
                        //{
                        //    if (this.TagWriter.TryTransformTag(tag, out var result))
                        //    {
                        //        if (!appendedTagString)
                        //        {
                        //            exemplarString.Append(" Filtered Tags: ");
                        //            appendedTagString = true;
                        //        }

                        //        exemplarString.Append($"{result.Key}: {result.Value}");
                        //        exemplarString.Append(' ');
                        //    }
                        //}

                        exemplarString.AppendLine();
                    }
                }

                //msg = new StringBuilder();
                msg.Append('(');
                msg.Append(metricPoint.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                msg.Append(", ");
                msg.Append(metricPoint.EndTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                msg.Append("] ");
                msg.Append(tags);
                if (tags != string.Empty)
                {
                    msg.Append(' ');
                }

                msg.Append(metric.MetricType);
                msg.AppendLine();
                msg.Append($"Value: {valueDisplay}");

                if (exemplarString.Length > 0)
                {
                    msg.AppendLine();
                    msg.AppendLine("Exemplars");
                    msg.Append(exemplarString.ToString());
                }

                msg.AppendLine();
                msg.AppendLine("Instrumentation scope (Meter):");
                msg.AppendLine($"\tName: {metric.MeterName}");
                if (!string.IsNullOrEmpty(metric.MeterVersion))
                {
                    msg.AppendLine($"\tVersion: {metric.MeterVersion}");
                }

                if (metric.MeterTags?.Any() == true)
                {
                    msg.AppendLine("\tTags:");
                    foreach (var meterTag in metric.MeterTags)
                    {
                        //if (this.TagWriter.TryTransformTag(meterTag, out var result))
                        {
                            msg.AppendLine($"\t\t{meterTag.Key}: {meterTag.Value}");
                        }
                    }
                }

                var resource = this.ParentProvider.GetResource();
                if (resource != Resource.Empty)
                {
                    msg.AppendLine("Resource associated with Metric:");
                    foreach (var resourceAttribute in resource.Attributes)
                    {
                        //if (this.TagWriter.TryTransformTag(resourceAttribute.Key, resourceAttribute.Value, out var result))
                        {
                            msg.AppendLine($"\t{resourceAttribute.Key}: {resourceAttribute.Value}");
                        }
                    }
                }

                Console.WriteLine(msg.ToString());

            }
        }

        return ExportResult.Success;
    }
}
