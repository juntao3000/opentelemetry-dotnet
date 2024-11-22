// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using OpenTelemetry.Context;

namespace OpenTelemetry;

/// <summary>
/// 包含管理内部操作的检测方法。
/// </summary>
public sealed class SuppressInstrumentationScope : IDisposable
{
    // 一个整数值，用于控制是否应禁止（禁用）检测。
    // * null: 不禁止检测
    // * Depth = [int.MinValue, -1]: 始终禁止检测
    // * Depth = [1, int.MaxValue]: 在引用计数模式下禁止检测
    private static readonly RuntimeContextSlot<SuppressInstrumentationScope?> Slot = RuntimeContext.RegisterSlot<SuppressInstrumentationScope?>("otel.suppress_instrumentation");

    // 保存前一个检测范围的引用
    private readonly SuppressInstrumentationScope? previousScope;
    // 标识对象是否已释放
    private bool disposed;

    // 构造函数，初始化检测范围
    internal SuppressInstrumentationScope(bool value = true)
    {
        this.previousScope = Slot.Get();
        this.Depth = value ? -1 : 0;
        Slot.Set(this);
    }

    // 检查当前是否禁止检测
    internal static bool IsSuppressed => (Slot.Get()?.Depth ?? 0) != 0;

    // 当前检测范围的深度
    internal int Depth { get; private set; }

    /// <summary>
    /// 开始一个新的范围，在该范围内禁止检测。
    /// </summary>
    /// <param name="value">指示是否禁止检测的值。</param>
    /// <returns>对象以结束范围。</returns>
    /// <remarks>
    /// 这通常用于防止由收集内部操作（如通过 HTTP 导出跟踪）创建的无限循环。
    /// <code>
    ///     public override async Task&lt;ExportResult&gt; ExportAsync(
    ///         IEnumerable&lt;Activity&gt; batch,
    ///         CancellationToken cancellationToken)
    ///     {
    ///         using (SuppressInstrumentationScope.Begin())
    ///         {
    ///             // 检测被禁止（即 Sdk.SuppressInstrumentation == true）
    ///         }
    ///
    ///         // 检测未被禁止（即 Sdk.SuppressInstrumentation == false）
    ///     }
    /// </code>
    /// </remarks>
    public static IDisposable Begin(bool value = true)
    {
        return new SuppressInstrumentationScope(value);
    }

    /// <summary>
    /// 进入禁止模式。
    /// 如果禁止模式已启用（slot.Depth 是负整数），则不执行任何操作。
    /// 如果禁止模式未启用（slot 为 null），则进入引用计数禁止模式。
    /// 如果禁止模式已启用（slot.Depth 是正整数），则增加引用计数。
    /// </summary>
    /// <returns>更新后的禁止槽值。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Enter()
    {
        var currentScope = Slot.Get();

        if (currentScope == null)
        {
            Slot.Set(
                new SuppressInstrumentationScope()
                {
                    Depth = 1,
                });

            return 1;
        }

        var currentDepth = currentScope.Depth;

        if (currentDepth >= 0)
        {
            currentScope.Depth = ++currentDepth;
        }

        return currentDepth;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.disposed)
        {
            Slot.Set(this.previousScope);
            this.disposed = true;
        }
    }

    /// <summary>
    /// 如果触发则增加深度。
    /// </summary>
    /// <returns>更新后的深度值。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int IncrementIfTriggered()
    {
        var currentScope = Slot.Get();

        if (currentScope == null)
        {
            return 0;
        }

        var currentDepth = currentScope.Depth;

        if (currentScope.Depth > 0)
        {
            currentScope.Depth = ++currentDepth;
        }

        return currentDepth;
    }

    /// <summary>
    /// 如果触发则减少深度。
    /// </summary>
    /// <returns>更新后的深度值。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int DecrementIfTriggered()
    {
        var currentScope = Slot.Get();

        if (currentScope == null)
        {
            return 0;
        }

        var currentDepth = currentScope.Depth;

        if (currentScope.Depth > 0)
        {
            if (--currentDepth == 0)
            {
                Slot.Set(currentScope.previousScope);
            }
            else
            {
                currentScope.Depth = currentDepth;
            }
        }

        return currentDepth;
    }
}
