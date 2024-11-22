// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 存储具有 base-2 指数桶边界的直方图度量流的配置。
/// </summary>
public sealed class Base2ExponentialBucketHistogramConfiguration : HistogramConfiguration
{
    // 最大桶数，默认为 Metric.DefaultExponentialHistogramMaxBuckets
    private int maxSize = Metric.DefaultExponentialHistogramMaxBuckets;
    // 最大比例因子，默认为 Metric.DefaultExponentialHistogramMaxScale
    private int maxScale = Metric.DefaultExponentialHistogramMaxScale;

    /// <summary>
    /// 获取或设置正负范围内每个范围的最大桶数，不包括特殊的零桶。
    /// </summary>
    /// <remarks>
    /// 默认值为 160。最小值为 2。
    /// </remarks>
    public int MaxSize
    {
        get
        {
            return this.maxSize;
        }

        set
        {
            // 如果值小于 2，则抛出异常
            if (value < 2)
            {
                throw new ArgumentException($"Histogram max size is invalid. Minimum size is 2.", nameof(value));
            }

            this.maxSize = value;
        }
    }

    /// <summary>
    /// 获取或设置用于确定桶边界分辨率的最大比例因子。比例越高，分辨率越高。
    /// </summary>
    /// <remarks>
    /// 默认值为 20。最小值为 -11。最大值为 20。
    /// </remarks>
    public int MaxScale
    {
        get
        {
            return this.maxScale;
        }

        set
        {
            // 如果值不在 [-11, 20] 范围内，则抛出异常
            if (value < -11 || value > 20)
            {
                throw new ArgumentException($"Histogram max scale is invalid. Max scale must be in the range [-11, 20].", nameof(value));
            }

            this.maxScale = value;
        }
    }
}
