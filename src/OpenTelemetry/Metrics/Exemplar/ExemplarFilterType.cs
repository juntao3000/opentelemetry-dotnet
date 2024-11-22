// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 定义支持的示例过滤器。
/// </summary>
/// <remarks>
/// 规范：<see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplarfilter"/>。
/// </remarks>
public enum ExemplarFilterType
{
    /// <summary>
    /// 一个示例过滤器，不使任何测量值有资格成为
    /// <see cref="Exemplar"/>。
    /// </summary>
    /// <remarks>
    /// <para>注意：在仪表提供程序上设置 <see cref="AlwaysOff"/>
    /// 实际上禁用了示例。</para>
    /// <para>规范：<see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#alwaysoff"/>。</para>
    /// </remarks>
    AlwaysOff,

    /// <summary>
    /// 一个示例过滤器，使所有测量值都有资格成为
    /// <see cref="Exemplar"/>。
    /// </summary>
    /// <remarks>
    /// 规范：<see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#alwayson"/>。
    /// </remarks>
    AlwaysOn,

    /// <summary>
    /// 一个示例过滤器，使在采样的 <see cref="Activity"/>（跨度）上下文中记录的测量值有资格成为 <see
    /// cref="Exemplar"/>。
    /// </summary>
    /// <remarks>
    /// 规范：<see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#tracebased"/>。
    /// </remarks>
    TraceBased,
}
