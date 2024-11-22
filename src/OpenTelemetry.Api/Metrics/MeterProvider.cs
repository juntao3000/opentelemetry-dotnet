// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// MeterProvider 基类。
/// </summary>
public class MeterProvider : BaseProvider // MeterProvider 类，继承自 BaseProvider
{
    /// <summary>
    /// 初始化 <see cref="MeterProvider"/> 类的新实例。
    /// </summary>
    protected MeterProvider() // 受保护的构造函数，初始化 MeterProvider 类的新实例
    {
    }
}
