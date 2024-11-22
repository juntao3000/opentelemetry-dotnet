// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif
using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

// 线程静态存储类，用于在每个线程中存储标签键值对
internal sealed class ThreadStaticStorage
{
    // 最大标签缓存大小
    internal const int MaxTagCacheSize = 8;

    // 线程静态存储实例
    [ThreadStatic]
    private static ThreadStaticStorage? storage;

    // 主标签存储数组
    private readonly TagStorage[] primaryTagStorage = new TagStorage[MaxTagCacheSize];
    // 次标签存储数组
    private readonly TagStorage[] secondaryTagStorage = new TagStorage[MaxTagCacheSize];

    // 构造函数，初始化标签存储数组
    private ThreadStaticStorage()
    {
        for (int i = 0; i < MaxTagCacheSize; i++)
        {
            this.primaryTagStorage[i] = new TagStorage(i + 1);
            this.secondaryTagStorage[i] = new TagStorage(i + 1);
        }
    }

    // 获取线程静态存储实例
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ThreadStaticStorage GetStorage()
        => storage ??= new ThreadStaticStorage();

    // 将标签分割为键和值
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SplitToKeysAndValues(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        int tagLength,
        out KeyValuePair<string, object?>[] tagKeysAndValues)
    {
        Guard.ThrowIfZero(tagLength, $"必须至少有一个标签才能使用{nameof(ThreadStaticStorage)}");

        if (tagLength <= MaxTagCacheSize)
        {
            tagKeysAndValues = this.primaryTagStorage[tagLength - 1].TagKeysAndValues;
        }
        else
        {
            tagKeysAndValues = new KeyValuePair<string, object?>[tagLength];
        }

        tags.CopyTo(tagKeysAndValues);
    }

    // 将标签分割为键和值，并过滤感兴趣的标签
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SplitToKeysAndValues(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        int tagLength,
#if NET
        FrozenSet<string> tagKeysInteresting,
#else
        HashSet<string> tagKeysInteresting,
#endif
        out KeyValuePair<string, object?>[]? tagKeysAndValues,
        out int actualLength)
    {
        // 我们事先不知道实际长度，所以从最大可能长度开始
        var maxLength = Math.Min(tagKeysInteresting.Count, tagLength);
        if (maxLength == 0)
        {
            tagKeysAndValues = null;
        }
        else if (maxLength <= MaxTagCacheSize)
        {
            tagKeysAndValues = this.primaryTagStorage[maxLength - 1].TagKeysAndValues;
        }
        else
        {
            tagKeysAndValues = new KeyValuePair<string, object?>[maxLength];
        }

        actualLength = 0;
        for (var n = 0; n < tagLength; n++)
        {
            // 仅复制感兴趣的标签，并保持计数
            if (tagKeysInteresting.Contains(tags[n].Key))
            {
                Debug.Assert(tagKeysAndValues != null, "tagKeysAndValues 为空");

                tagKeysAndValues![actualLength] = tags[n];
                actualLength++;
            }
        }

        // 如果实际长度等于最大长度，太好了！
        // 否则，我们需要选择实际长度的数组，并将标签复制到其中
        // 这优化了常见场景：
        // 用户只对 TagA 和 TagB 感兴趣，而传入的测量包含 TagA 和 TagB 以及更多
        // 在这种情况下，实际长度将与最大长度相同，并且避免了以下复制
        if (actualLength < maxLength)
        {
            if (actualLength == 0)
            {
                tagKeysAndValues = null;
                return;
            }

            Debug.Assert(tagKeysAndValues != null, "tagKeysAndValues 为空");

            if (actualLength <= MaxTagCacheSize)
            {
                var tmpTagKeysAndValues = this.primaryTagStorage[actualLength - 1].TagKeysAndValues;

                Array.Copy(tagKeysAndValues, 0, tmpTagKeysAndValues, 0, actualLength);

                tagKeysAndValues = tmpTagKeysAndValues;
            }
            else
            {
                var tmpTagKeysAndValues = new KeyValuePair<string, object?>[actualLength];

                Array.Copy(tagKeysAndValues, 0, tmpTagKeysAndValues, 0, actualLength);

                tagKeysAndValues = tmpTagKeysAndValues;
            }
        }
    }

    // 克隆标签键和值
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CloneKeysAndValues(
        KeyValuePair<string, object?>[] inputTagKeysAndValues,
        int tagLength,
        out KeyValuePair<string, object?>[] clonedTagKeysAndValues)
    {
        Guard.ThrowIfZero(tagLength, $"必须至少有一个标签才能使用{nameof(ThreadStaticStorage)}", $"{nameof(tagLength)}");

        if (tagLength <= MaxTagCacheSize)
        {
            clonedTagKeysAndValues = this.secondaryTagStorage[tagLength - 1].TagKeysAndValues;
        }
        else
        {
            clonedTagKeysAndValues = new KeyValuePair<string, object?>[tagLength];
        }

        Array.Copy(inputTagKeysAndValues, 0, clonedTagKeysAndValues, 0, tagLength);
    }

    // 标签存储类，用于存储键值对数组
    internal sealed class TagStorage
    {
        // 用于分割为键序列和值序列
        internal readonly KeyValuePair<string, object?>[] TagKeysAndValues;

        // 构造函数，初始化键值对数组
        internal TagStorage(int n)
        {
            this.TagKeysAndValues = new KeyValuePair<string, object?>[n];
        }
    }
}
