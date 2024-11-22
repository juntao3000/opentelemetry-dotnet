// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// 用于声明度量导出器支持的 <see cref="ExportModes"/> 的属性。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ExportModesAttribute : Attribute
{
    // 只读字段，存储支持的导出模式
    private readonly ExportModes supportedExportModes;

    /// <summary>
    /// 初始化 <see cref="ExportModesAttribute"/> 类的新实例。
    /// </summary>
    /// <param name="supported"><see cref="ExportModes"/>.</param>
    public ExportModesAttribute(ExportModes supported)
    {
        // 将传入的支持模式赋值给只读字段
        this.supportedExportModes = supported;
    }

    /// <summary>
    /// 获取支持的 <see cref="ExportModes"/>。
    /// </summary>
    public ExportModes Supported => this.supportedExportModes;
}
