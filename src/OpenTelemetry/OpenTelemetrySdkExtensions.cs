// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// 包含扩展 <see cref="OpenTelemetrySdk"/> 类的方法。
/// </summary>
public static class OpenTelemetrySdkExtensions
{
    // 定义一个静态的 NullLoggerFactory 实例，作为默认的 no-op 日志工厂
    private static readonly NullLoggerFactory NoopLoggerFactory = new();

    /// <summary>
    /// 获取 <see cref="ILoggerFactory"/> 包含在 <see cref="OpenTelemetrySdk"/> 实例中。
    /// </summary>
    /// <remarks>
    /// 注意：默认的 <see cref="ILoggerFactory"/> 将是一个 no-op 实例。
    /// 调用 <see cref="OpenTelemetryBuilderSdkExtensions.WithLogging(IOpenTelemetryBuilder)"/> 以启用日志记录。
    /// </remarks>
    /// <param name="sdk"><see cref="OpenTelemetrySdk"/>。</param>
    /// <returns><see cref="ILoggerFactory"/>。</returns>
    public static ILoggerFactory GetLoggerFactory(this OpenTelemetrySdk sdk)
    {
        // 检查 sdk 是否为 null，如果是则抛出异常
        Guard.ThrowIfNull(sdk);

        // 尝试从 sdk 的服务中获取 ILoggerFactory 实例，如果获取失败则返回 NoopLoggerFactory
        return (ILoggerFactory?)sdk.Services.GetService(typeof(ILoggerFactory))
            ?? NoopLoggerFactory;
    }
}
