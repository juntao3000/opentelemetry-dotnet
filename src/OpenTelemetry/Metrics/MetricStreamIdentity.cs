// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics;

// MetricStreamIdentity 结构体用于表示度量流的标识，并实现了 IEquatable 接口
internal readonly struct MetricStreamIdentity : IEquatable<MetricStreamIdentity>
{
    // StringArrayEqualityComparer 类用于比较两个字符串数组是否相等，并生成字符串数组的哈希码
    private static readonly StringArrayEqualityComparer StringArrayComparer = new();
    // hashCode 变量用于存储对象的哈希码
    private readonly int hashCode;

    // MetricStreamIdentity 构造函数用于初始化度量流标识
    public MetricStreamIdentity(Instrument instrument, MetricStreamConfiguration? metricStreamConfiguration)
    {
        // MeterName 变量用于存储仪表的名称
        this.MeterName = instrument.Meter.Name;
        // MeterVersion 变量用于存储仪表的版本，如果版本为空则使用空字符串
        this.MeterVersion = instrument.Meter.Version ?? string.Empty;
        // MeterTags 变量用于存储仪表的标签
        this.MeterTags = instrument.Meter.Tags;
        // InstrumentName 变量用于存储度量流的名称，如果配置中没有名称则使用仪器的名称
        this.InstrumentName = metricStreamConfiguration?.Name ?? instrument.Name;
        // Unit 变量用于存储仪器的单位，如果单位为空则使用空字符串
        this.Unit = instrument.Unit ?? string.Empty;
        // Description 变量用于存储度量流的描述，如果配置中没有描述则使用仪器的描述，如果描述为空则使用空字符串
        this.Description = metricStreamConfiguration?.Description ?? instrument.Description ?? string.Empty;
        // InstrumentType 变量用于存储仪器的类型
        this.InstrumentType = instrument.GetType();
        // ViewId 变量用于存储视图 ID
        this.ViewId = metricStreamConfiguration?.ViewId;
        // MetricStreamName 变量用于存储度量流的名称，格式为 "MeterName.MeterVersion.InstrumentName"
        this.MetricStreamName = $"{this.MeterName}.{this.MeterVersion}.{this.InstrumentName}";
        // TagKeys 变量用于存储标签键
        this.TagKeys = metricStreamConfiguration?.CopiedTagKeys;
        // HistogramBucketBounds 变量用于存储直方图的桶边界
        this.HistogramBucketBounds = GetExplicitBucketHistogramBounds(instrument, metricStreamConfiguration);
        // ExponentialHistogramMaxSize 变量用于存储指数直方图的最大大小
        this.ExponentialHistogramMaxSize = (metricStreamConfiguration as Base2ExponentialBucketHistogramConfiguration)?.MaxSize ?? 0;
        // ExponentialHistogramMaxScale 变量用于存储指数直方图的最大比例
        this.ExponentialHistogramMaxScale = (metricStreamConfiguration as Base2ExponentialBucketHistogramConfiguration)?.MaxScale ?? 0;
        // HistogramRecordMinMax 变量用于存储是否记录直方图的最小值和最大值
        this.HistogramRecordMinMax = (metricStreamConfiguration as HistogramConfiguration)?.RecordMinMax ?? true;

#if NET
        HashCode hashCode = default;
        hashCode.Add(this.InstrumentType);
        hashCode.Add(this.MeterName);
        hashCode.Add(this.MeterVersion);
        hashCode.Add(this.InstrumentName);
        hashCode.Add(this.HistogramRecordMinMax);
        hashCode.Add(this.Unit);
        hashCode.Add(this.Description);
        hashCode.Add(this.ViewId);

        // 注意：这里的 this.TagKeys! 看起来很奇怪，但值为 null 是可以的。HashCode.Add 处理值为 null 的情况。
        // 我们基本上是在抑制一个误报，这是由于注释的一个问题/怪癖引起的。参见：https://github.com/dotnet/runtime/pull/91905。
        hashCode.Add(this.TagKeys!, StringArrayComparer);

        hashCode.Add(this.ExponentialHistogramMaxSize);
        hashCode.Add(this.ExponentialHistogramMaxScale);
        if (this.HistogramBucketBounds != null)
        {
            for (var i = 0; i < this.HistogramBucketBounds.Length; ++i)
            {
                hashCode.Add(this.HistogramBucketBounds[i]);
            }
        }

        var hash = hashCode.ToHashCode();
#else
        var hash = 17;
        unchecked
        {
            hash = (hash * 31) + this.InstrumentType.GetHashCode();
            hash = (hash * 31) + this.MeterName.GetHashCode();
            hash = (hash * 31) + this.MeterVersion.GetHashCode();

            // MeterTags 不是标识的一部分，所以不包括在这里。
            hash = (hash * 31) + this.InstrumentName.GetHashCode();
            hash = (hash * 31) + this.HistogramRecordMinMax.GetHashCode();
            hash = (hash * 31) + this.ExponentialHistogramMaxSize.GetHashCode();
            hash = (hash * 31) + this.ExponentialHistogramMaxScale.GetHashCode();
            hash = (hash * 31) + this.Unit.GetHashCode();
            hash = (hash * 31) + this.Description.GetHashCode();
            hash = (hash * 31) + (this.ViewId ?? 0);
            hash = (hash * 31) + (this.TagKeys != null ? StringArrayComparer.GetHashCode(this.TagKeys) : 0);
            if (this.HistogramBucketBounds != null)
            {
                var len = this.HistogramBucketBounds.Length;
                for (var i = 0; i < len; ++i)
                {
                    hash = (hash * 31) + this.HistogramBucketBounds[i].GetHashCode();
                }
            }
        }

#endif
        this.hashCode = hash;
    }

    // MeterName 属性用于获取仪表的名称
    public string MeterName { get; }

    // MeterVersion 属性用于获取仪表的版本
    public string MeterVersion { get; }

    // MeterTags 属性用于获取仪表的标签
    public IEnumerable<KeyValuePair<string, object?>>? MeterTags { get; }

    // InstrumentName 属性用于获取度量流的名称
    public string InstrumentName { get; }

    // Unit 属性用于获取仪器的单位
    public string Unit { get; }

    // Description 属性用于获取度量流的描述
    public string Description { get; }

    // InstrumentType 属性用于获取仪器的类型
    public Type InstrumentType { get; }

    // ViewId 属性用于获取视图 ID
    public int? ViewId { get; }

    // MetricStreamName 属性用于获取度量流的名称
    public string MetricStreamName { get; }

    // TagKeys 属性用于获取标签键
    public string[]? TagKeys { get; }

    // HistogramBucketBounds 属性用于获取直方图的桶边界
    public double[]? HistogramBucketBounds { get; }

    // ExponentialHistogramMaxSize 属性用于获取指数直方图的最大大小
    public int ExponentialHistogramMaxSize { get; }

    // ExponentialHistogramMaxScale 属性用于获取指数直方图的最大比例
    public int ExponentialHistogramMaxScale { get; }

    // HistogramRecordMinMax 属性用于获取是否记录直方图的最小值和最大值
    public bool HistogramRecordMinMax { get; }

    // IsHistogram 属性用于判断仪器是否为直方图类型
    public bool IsHistogram =>
        this.InstrumentType == typeof(Histogram<long>)
        || this.InstrumentType == typeof(Histogram<int>)
        || this.InstrumentType == typeof(Histogram<short>)
        || this.InstrumentType == typeof(Histogram<byte>)
        || this.InstrumentType == typeof(Histogram<float>)
        || this.InstrumentType == typeof(Histogram<double>);

    // 重载 == 运算符用于比较两个 MetricStreamIdentity 对象是否相等
    public static bool operator ==(MetricStreamIdentity metricIdentity1, MetricStreamIdentity metricIdentity2) => metricIdentity1.Equals(metricIdentity2);

    // 重载 != 运算符用于比较两个 MetricStreamIdentity 对象是否不相等
    public static bool operator !=(MetricStreamIdentity metricIdentity1, MetricStreamIdentity metricIdentity2) => !metricIdentity1.Equals(metricIdentity2);

    // 重写 Equals 方法用于比较当前对象与另一个对象是否相等
    public override readonly bool Equals(object? obj)
    {
        return obj is MetricStreamIdentity other && this.Equals(other);
    }

    // 实现 IEquatable<MetricStreamIdentity> 接口的 Equals 方法用于比较两个 MetricStreamIdentity 对象是否相等
    public bool Equals(MetricStreamIdentity other)
    {
        return this.InstrumentType == other.InstrumentType
            && this.MeterName == other.MeterName
            && this.MeterVersion == other.MeterVersion
            && this.InstrumentName == other.InstrumentName
            && this.Unit == other.Unit
            && this.Description == other.Description
            && this.ViewId == other.ViewId
            && this.HistogramRecordMinMax == other.HistogramRecordMinMax
            && this.ExponentialHistogramMaxSize == other.ExponentialHistogramMaxSize
            && this.ExponentialHistogramMaxScale == other.ExponentialHistogramMaxScale
            && StringArrayComparer.Equals(this.TagKeys, other.TagKeys)
            && HistogramBoundsEqual(this.HistogramBucketBounds, other.HistogramBucketBounds);
    }

    // 重写 GetHashCode 方法用于获取对象的哈希码
    public override readonly int GetHashCode() => this.hashCode;

    // GetExplicitBucketHistogramBounds 方法用于获取显式桶直方图的桶边界
    private static double[]? GetExplicitBucketHistogramBounds(Instrument instrument, MetricStreamConfiguration? metricStreamConfiguration)
    {
        if (metricStreamConfiguration is ExplicitBucketHistogramConfiguration explicitBucketHistogramConfiguration
            && explicitBucketHistogramConfiguration.CopiedBoundaries != null)
        {
            return explicitBucketHistogramConfiguration.CopiedBoundaries;
        }

        return instrument switch
        {
            Histogram<long> longHistogram => GetExplicitBucketHistogramBoundsFromAdvice(longHistogram),
            Histogram<int> intHistogram => GetExplicitBucketHistogramBoundsFromAdvice(intHistogram),
            Histogram<short> shortHistogram => GetExplicitBucketHistogramBoundsFromAdvice(shortHistogram),
            Histogram<byte> byteHistogram => GetExplicitBucketHistogramBoundsFromAdvice(byteHistogram),
            Histogram<float> floatHistogram => GetExplicitBucketHistogramBoundsFromAdvice(floatHistogram),
            Histogram<double> doubleHistogram => GetExplicitBucketHistogramBoundsFromAdvice(doubleHistogram),
            _ => null,
        };
    }

    // GetExplicitBucketHistogramBoundsFromAdvice 方法用于从建议中获取显式桶直方图的桶边界
    private static double[]? GetExplicitBucketHistogramBoundsFromAdvice<T>(Histogram<T> histogram)
        where T : struct
    {
        var adviceExplicitBucketBoundaries = histogram.Advice?.HistogramBucketBoundaries;
        if (adviceExplicitBucketBoundaries == null)
        {
            return null;
        }

        if (typeof(T) == typeof(double))
        {
            return ((IReadOnlyList<double>)adviceExplicitBucketBoundaries).ToArray();
        }
        else
        {
            double[] explicitBucketBoundaries = new double[adviceExplicitBucketBoundaries.Count];

            for (int i = 0; i < adviceExplicitBucketBoundaries.Count; i++)
            {
                explicitBucketBoundaries[i] = Convert.ToDouble(adviceExplicitBucketBoundaries[i]);
            }

            return explicitBucketBoundaries;
        }
    }

    // HistogramBoundsEqual 方法用于比较两个直方图的桶边界是否相等
    private static bool HistogramBoundsEqual(double[]? bounds1, double[]? bounds2)
    {
        if (ReferenceEquals(bounds1, bounds2))
        {
            return true;
        }

        if (ReferenceEquals(bounds1, null) || ReferenceEquals(bounds2, null))
        {
            return false;
        }

        var len1 = bounds1.Length;

        if (len1 != bounds2.Length)
        {
            return false;
        }

        for (int i = 0; i < len1; i++)
        {
            if (!bounds1[i].Equals(bounds2[i]))
            {
                return false;
            }
        }

        return true;
    }
}
