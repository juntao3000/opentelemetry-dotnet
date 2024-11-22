// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES && NET
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 存储 MetricStream 的配置。
/// </summary>
public class MetricStreamConfiguration
{
    // 度量流的名称
    private string? name;

    // 基数限制
    private int? cardinalityLimit = null;

    /// <summary>
    /// 获取丢弃配置。
    /// </summary>
    /// <remarks>
    /// 注意：给定仪器的所有度量将被丢弃（不收集）。
    /// </remarks>
    public static MetricStreamConfiguration Drop { get; } = new MetricStreamConfiguration { ViewId = -1 };

    /// <summary>
    /// 获取或设置度量流的可选名称。
    /// </summary>
    /// <remarks>
    /// 注意：如果未提供，将使用仪器名称。
    /// </remarks>
    public string? Name
    {
        get => this.name;
        set
        {
            if (value != null && !MeterProviderBuilderSdk.IsValidViewName(value))
            {
                throw new ArgumentException($"Custom view name {value} is invalid.", nameof(value));
            }

            this.name = value;
        }
    }

    /// <summary>
    /// 获取或设置度量流的可选描述。
    /// </summary>
    /// <remarks>
    /// 注意：如果未提供，将使用仪器描述。
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// 获取或设置要包含在度量流中的可选标签键。
    /// </summary>
    /// <remarks>
    /// 注意：
    /// <list type="bullet">
    /// <item>如果未提供，报告测量时仪器提供的所有标签将用于聚合。如果提供，则仅使用此列表中的标签进行聚合。提供空数组将导致没有任何标签的度量流。</item>
    /// <item>对提供的数组进行复制。</item>
    /// </list>
    /// </remarks>
    public string[]? TagKeys
    {
        get
        {
            if (this.CopiedTagKeys != null)
            {
                string[] copy = new string[this.CopiedTagKeys.Length];
                this.CopiedTagKeys.AsSpan().CopyTo(copy);
                return copy;
            }

            return null;
        }

        set
        {
            if (value != null)
            {
                string[] copy = new string[value.Length];
                value.AsSpan().CopyTo(copy);
                this.CopiedTagKeys = copy;
            }
            else
            {
                this.CopiedTagKeys = null;
            }
        }
    }

    /// <summary>
    /// 获取或设置定义视图管理的度量允许的最大数据点数的正整数值。
    /// </summary>
    /// <remarks>
    /// <para>规范参考：<see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#cardinality-limits">基数限制</see>。</para>
    /// 注意：基数限制确定度量的唯一维度组合的最大数量。没有维度的度量和溢出度量被特殊处理，不计入此限制。如果未设置，将应用默认的 MeterProvider 基数限制 2000。
    /// </remarks>
    public int? CardinalityLimit
    {
        get => this.cardinalityLimit;
        set
        {
            if (value != null)
            {
                Guard.ThrowIfOutOfRange(value.Value, min: 1, max: int.MaxValue);
            }

            this.cardinalityLimit = value;
        }
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// 获取或设置用于生成 <see cref="ExemplarReservoir"/> 的工厂函数，视图管理的度量在存储 <see cref="Exemplar"/> 时使用。
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="ExemplarReservoir" path="/remarks/para[@experimental-warning='true']"/>
    /// <para>注意：从工厂函数返回 <see langword="null"/> 将导致 SDK 根据度量类型选择默认的 <see cref="ExemplarReservoir"/>。</para>
    /// 规范：<see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#stream-configuration"/>.
    /// </remarks>
#if NET
        [Experimental(DiagnosticDefinitions.ExemplarReservoirExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
    public Func<ExemplarReservoir?>? ExemplarReservoirFactory { get; set; }
#else
        internal Func<ExemplarReservoir?>? ExemplarReservoirFactory { get; set; }
#endif

    // 复制的标签键
    internal string[]? CopiedTagKeys { get; private set; }

    // 视图 ID
    internal int? ViewId { get; set; }
}
