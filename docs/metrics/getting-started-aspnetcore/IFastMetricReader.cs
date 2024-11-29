namespace AspNetCoreMetrics;

public interface IFastMetricReader
{
    bool Collect(int timeoutMilliseconds = Timeout.Infinite);
}
