// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 存储具有显式桶边界的直方图度量流的配置。
/// </summary>
public class ExplicitBucketHistogramConfiguration : HistogramConfiguration
{
    /// <summary>
    /// 获取或设置直方图度量流的可选边界。
    /// </summary>
    /// <remarks>
    /// 要求：
    /// <list type="bullet">
    /// <item>数组必须按升序排列且值唯一。</item>
    /// <item>空数组将导致不计算任何直方图桶。</item>
    /// <item>空值将导致使用默认的桶边界。</item>
    /// </list>
    /// 注意：对提供的数组进行复制。
    /// </remarks>
    public double[]? Boundaries
    {
        get
        {
            // 如果 CopiedBoundaries 不为空，则返回其副本
            if (this.CopiedBoundaries != null)
            {
                double[] copy = new double[this.CopiedBoundaries.Length];
                this.CopiedBoundaries.AsSpan().CopyTo(copy);
                return copy;
            }

            // 否则返回空
            return null;
        }

        set
        {
            // 如果设置的值不为空
            if (value != null)
            {
                // 检查数组是否按升序排列且值唯一
                if (!IsSortedAndDistinct(value))
                {
                    throw new ArgumentException($"Histogram boundaries are invalid. Histogram boundaries must be in ascending order with distinct values.", nameof(value));
                }

                // 复制数组
                double[] copy = new double[value.Length];
                value.AsSpan().CopyTo(copy);
                this.CopiedBoundaries = copy;
            }
            else
            {
                // 如果设置的值为空，则将 CopiedBoundaries 设为空
                this.CopiedBoundaries = null;
            }
        }
    }

    // 内部存储的边界副本
    internal double[]? CopiedBoundaries { get; private set; }

    // 检查数组是否按升序排列且值唯一
    private static bool IsSortedAndDistinct(double[] values)
    {
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] <= values[i - 1])
            {
                return false;
            }
        }

        return true;
    }
}
