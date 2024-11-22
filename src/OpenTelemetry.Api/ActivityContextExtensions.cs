// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

// The ActivityContext class is in the System.Diagnostics namespace.
// These extension methods on ActivityContext are intentionally not placed in the
// same namespace as Activity to prevent name collisions in the future.
// The OpenTelemetry namespace is used because ActivityContext applies to all types
// of telemetry data - i.e. traces, metrics, and logs.
namespace OpenTelemetry;

/// <summary>
/// ActivityContext 的扩展方法。
/// </summary>
public static class ActivityContextExtensions // 扩展 ActivityContext 的静态类
{
    /// <summary>
    /// 返回一个布尔值，指示 ActivityContext 是否有效。
    /// </summary>
    /// <param name="ctx">ActivityContext。</param>
    /// <returns>上下文是否有效。</returns>
    public static bool IsValid(this ActivityContext ctx) // 判断 ActivityContext 是否有效的扩展方法
    {
        return ctx != default; // 如果 ctx 不等于默认值，则表示有效
    }
}
