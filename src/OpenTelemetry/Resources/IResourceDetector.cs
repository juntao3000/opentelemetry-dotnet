// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources;

/// <summary>
/// 资源检测器接口。
/// </summary>
public interface IResourceDetector
{
    /// <summary>
    /// 调用以从检测器获取带有属性的资源。
    /// </summary>
    /// <returns>一个 <see cref="Resource"/> 的实例。</returns>
    Resource Detect(); // 检测资源的方法，返回一个Resource实例
}
