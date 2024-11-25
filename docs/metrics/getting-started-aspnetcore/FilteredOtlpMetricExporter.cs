using System.Diagnostics.Tracing;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;

namespace AspNetCoreMetrics;

public class FilteredOtlpMetricExporter : OtlpMetricExporter
{
    private readonly Func<Metric, bool>? predicate;

    public FilteredOtlpMetricExporter(OtlpExporterOptions options, Func<Metric, bool>? predicate)
        : base(options)
    {
        this.predicate = predicate;
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        if (this.predicate == null)
        {
            return base.Export(batch);
        }

        Batch<Metric> tmpBatch;
        using (var scope = SuppressInstrumentationScope.Begin())
        {
            try
            {
                // TODO: Do we need to use ArrayPool to resue arrays?
                var list = new List<Metric>(capacity: (int)batch.Count);

                foreach (var metric in batch)
                {
                    if (this.predicate(metric))
                    {
                        list.Add(metric);
                    }
                }

                if (list.Count == 0)
                {
                    return ExportResult.Success;
                }

                tmpBatch = new Batch<Metric>([.. list], list.Count);
            }
            catch (Exception ex)
            {
                FilteredOtlpMetricExporterEventSource.Log.ExportMethodException(ex);
                return ExportResult.Failure;
            }
        }

        return base.Export(tmpBatch);
    }
}

[EventSource(Name = "OpenTelemetry-Exporter-FilteredOtlpMetricExporter")]
internal sealed class FilteredOtlpMetricExporterEventSource : EventSource
{
    public static readonly FilteredOtlpMetricExporterEventSource Log = new();

    [NonEvent]
    public void ExportMethodException(Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.ExportMethodException(ex.Message);
        }
    }

    [Event(2, Message = "Unknown error in export method. Message: '{0}'", Level = EventLevel.Error)]
    public void ExportMethodException(string ex)
    {
        this.WriteEvent(2, ex);
    }
}
