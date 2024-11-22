// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

// 定义一个内部枚举类型 MetricPointStatus，用于表示 MetricPoint 的状态
internal enum MetricPointStatus
{
    /// <summary>
    /// 此状态应用于在 Collect 之后状态为 CollectPending 的 MetricPoint。
    /// 如果发生更新，状态将变为 CollectPending。
    /// </summary>
    NoCollectPending, // 表示没有收集挂起的状态

    /// <summary>
    /// 自上一个 Collect 周期以来，MetricPoint 已被更新。
    /// Collect 会将其状态变为 NoCollectPending。
    /// </summary>
    CollectPending, // 表示有收集挂起的状态
}
