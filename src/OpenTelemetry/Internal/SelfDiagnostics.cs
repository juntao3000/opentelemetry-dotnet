// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

/// <summary>
/// 自诊断类捕获由OpenTelemetry模块发送的EventSource事件，并将其写入本地文件以进行内部故障排除。
/// </summary>
internal sealed class SelfDiagnostics : IDisposable
{
    /// <summary>
    /// 长生命周期对象，持有相关资源。
    /// </summary>
    private static readonly SelfDiagnostics Instance = new();
    private readonly SelfDiagnosticsConfigRefresher configRefresher;

    static SelfDiagnostics()
    {
        // 在进程退出时，调用Dispose方法释放资源
        AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
        {
            Instance.Dispose();
        };
    }

    private SelfDiagnostics()
    {
        // 初始化配置刷新器
        this.configRefresher = new SelfDiagnosticsConfigRefresher();
    }

    /// <summary>
    /// 当EventSource类（例如OpenTelemetryApiEventSource）被调用以发送事件时，不会显式调用SelfDiagnostics类的任何成员。
    /// 为了触发CLR初始化SelfDiagnostics的静态字段和静态构造函数，在发送任何EventSource事件之前调用EnsureInitialized方法。
    /// </summary>
    public static void EnsureInitialized()
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 释放配置刷新器资源
            this.configRefresher.Dispose();
        }
    }
}
