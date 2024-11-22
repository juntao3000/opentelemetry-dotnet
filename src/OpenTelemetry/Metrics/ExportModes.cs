// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 描述度量导出器的模式。
/// </summary>
[Flags]
public enum ExportModes : byte
{
    /*
    0 0 0 0 0 0 0 0
    | | | | | | | |
    | | | | | | | +--- Push
    | | | | | | +----- Pull
    | | | | | +------- (保留)
    | | | | +--------- (保留)
    | | | +----------- (保留)
    | | +------------- (保留)
    | +--------------- (保留)
    +----------------- (保留)
    */

    /// <summary>
    /// 推送模式。
    /// </summary>
    Push = 0b1, // Push模式的二进制表示

    /// <summary>
    /// 拉取模式。
    /// </summary>
    Pull = 0b10, // Pull模式的二进制表示
}
