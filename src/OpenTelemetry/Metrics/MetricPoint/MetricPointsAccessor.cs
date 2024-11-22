// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// MetricPointsAccessor 结构体用于访问为 Metric 收集的 MetricPoint。
/// </summary>
public readonly struct MetricPointsAccessor
{
    // 存储 MetricPoint 数组
    private readonly MetricPoint[] metricsPoints;
    // 存储需要处理的 MetricPoint 索引数组
    private readonly int[] metricPointsToProcess;
    // 目标计数
    private readonly long targetCount;

    // MetricPointsAccessor 构造函数，初始化 metricsPoints、metricPointsToProcess 和 targetCount
    internal MetricPointsAccessor(MetricPoint[] metricsPoints, int[] metricPointsToProcess, long targetCount)
    {
        Debug.Assert(metricsPoints != null, "metricPoints was null");
        Debug.Assert(metricPointsToProcess != null, "metricPointsToProcess was null");

        this.metricsPoints = metricsPoints!;
        this.metricPointsToProcess = metricPointsToProcess!;
        this.targetCount = targetCount;
    }

    /// <summary>
    /// 返回一个 Enumerator，用于遍历 MetricPointsAccessor。
    /// </summary>
    /// <returns>Enumerator。</returns>
    public Enumerator GetEnumerator()
        => new(this.metricsPoints, this.metricPointsToProcess, this.targetCount);

    /// <summary>
    /// Enumerator 结构体用于枚举 MetricPointsAccessor 的元素。
    /// </summary>
    public struct Enumerator
    {
        // 存储 MetricPoint 数组
        private readonly MetricPoint[] metricsPoints;
        // 存储需要处理的 MetricPoint 索引数组
        private readonly int[] metricPointsToProcess;
        // 目标计数
        private readonly long targetCount;
        // 当前索引
        private long index;

        // Enumerator 构造函数，初始化 metricsPoints、metricPointsToProcess、targetCount 和 index
        internal Enumerator(MetricPoint[] metricsPoints, int[] metricPointsToProcess, long targetCount)
        {
            this.metricsPoints = metricsPoints;
            this.metricPointsToProcess = metricPointsToProcess;
            this.targetCount = targetCount;
            this.index = -1;
        }

        /// <summary>
        /// 获取枚举器当前位置的 MetricPoint。
        /// </summary>
        public readonly ref readonly MetricPoint Current
            => ref this.metricsPoints[this.metricPointsToProcess[this.index]];

        /// <summary>
        /// 将枚举器推进到 MetricPointsAccessor 的下一个元素。
        /// </summary>
        /// <returns>如果枚举器成功地推进到下一个元素，则为 true；如果枚举器已越过集合的末尾，则为 false。</returns>
        public bool MoveNext()
            => ++this.index < this.targetCount;
    }
}
