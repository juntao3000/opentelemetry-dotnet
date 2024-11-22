// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES && NET
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// 表示一个示例测量。
/// </summary>
/// <remarks><inheritdoc cref="ExemplarReservoir" path="/remarks/para[@experimental-warning='true']"/></remarks>
/// <typeparam name="T">测量类型。</typeparam>
#if NET
[Experimental(DiagnosticDefinitions.ExemplarReservoirExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
public
#else
internal
#endif
    // 只读的示例测量结构
    readonly ref struct ExemplarMeasurement<T>
    where T : struct
{
    // 构造函数，初始化示例测量
    internal ExemplarMeasurement(
        T value, // 测量值
        ReadOnlySpan<KeyValuePair<string, object?>> tags) // 测量标签
    {
        this.Value = value; // 设置测量值
        this.Tags = tags; // 设置测量标签
        this.ExplicitBucketHistogramBucketIndex = -1; // 设置显式桶直方图索引为-1
    }

    // 构造函数，初始化示例测量，带显式桶直方图索引
    internal ExemplarMeasurement(
        T value, // 测量值
        ReadOnlySpan<KeyValuePair<string, object?>> tags, // 测量标签
        int explicitBucketHistogramIndex) // 显式桶直方图索引
    {
        this.Value = value; // 设置测量值
        this.Tags = tags; // 设置测量标签
        this.ExplicitBucketHistogramBucketIndex = explicitBucketHistogramIndex; // 设置显式桶直方图索引
    }

    /// <summary>
    /// 获取测量值。
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// 获取测量标签。
    /// </summary>
    /// <remarks>
    /// 注意：<see cref="Tags"/> 表示在测量时提供的完整标签集，无论视图（<see
    /// cref="MetricStreamConfiguration.TagKeys"/>）配置了任何过滤。
    /// </remarks>
    public ReadOnlySpan<KeyValuePair<string, object?>> Tags { get; }

    // 显式桶直方图索引
    internal int ExplicitBucketHistogramBucketIndex { get; }
}
