// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>
/// 包含批量导出处理器选项。
/// </summary>
/// <typeparam name="T">要导出的遥测对象的类型。</typeparam>
public class BatchExportProcessorOptions<T>
    where T : class
{
    /// <summary>
    /// 获取或设置最大队列大小。如果达到最大大小，队列将丢弃数据。默认值为2048。
    /// </summary>
    public int MaxQueueSize { get; set; } = BatchExportProcessor<T>.DefaultMaxQueueSize;

    /// <summary>
    /// 获取或设置两次连续导出之间的延迟间隔（以毫秒为单位）。默认值为5000。
    /// </summary>
    public int ScheduledDelayMilliseconds { get; set; } = BatchExportProcessor<T>.DefaultScheduledDelayMilliseconds;

    /// <summary>
    /// 获取或设置导出取消的超时时间（以毫秒为单位）。默认值为30000。
    /// </summary>
    public int ExporterTimeoutMilliseconds { get; set; } = BatchExportProcessor<T>.DefaultExporterTimeoutMilliseconds;

    /// <summary>
    /// 获取或设置每次导出的最大批量大小。它必须小于或等于MaxQueueLength。默认值为512。
    /// </summary>
    public int MaxExportBatchSize { get; set; } = BatchExportProcessor<T>.DefaultMaxExportBatchSize;
}
