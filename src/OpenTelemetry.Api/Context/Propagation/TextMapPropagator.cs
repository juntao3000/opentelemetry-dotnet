// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// 定义了一个 TextMap 类型的 Propagator 接口，
/// 使用字符串键/值对来注入和提取传播数据。
/// </summary>
public abstract class TextMapPropagator
{
    /// <summary>
    /// 获取传播器使用的头列表。其用例包括：
    ///   * 允许预分配字段，特别是在像 gRPC Metadata 这样的系统中
    ///   * 允许对迭代器进行单次遍历（例如 OpenTracing 在 TextMap 中没有 getter）。
    /// </summary>
    public abstract ISet<string>? Fields { get; }

    /// <summary>
    /// 将上下文注入到载体中。
    /// </summary>
    /// <typeparam name="T">设置上下文的对象类型。通常是 HttpRequest 或类似对象。</typeparam>
    /// <param name="context">要通过网络传输的默认上下文。</param>
    /// <param name="carrier">设置上下文的对象。此对象的实例将传递给 setter。</param>
    /// <param name="setter">将在对象上设置名称和值对的操作。</param>
    public abstract void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter);

    /// <summary>
    /// 从载体中提取上下文。
    /// </summary>
    /// <typeparam name="T">从中提取上下文的对象类型。通常是 HttpRequest 或类似对象。</typeparam>
    /// <param name="context">提取失败时使用的默认上下文。</param>
    /// <param name="carrier">从中提取上下文的对象。此对象的实例将传递给 getter。</param>
    /// <param name="getter">将返回具有指定名称的键的字符串值的函数。</param>
    /// <returns>从其文本表示形式中提取的上下文。</returns>
    public abstract PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter);
}
