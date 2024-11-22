// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

// LookupData类用于存储查找数据的相关信息
internal sealed class LookupData
{
    // DeferredReclaim表示是否延迟回收
    public bool DeferredReclaim;
    // Index表示索引
    public int Index;
    // SortedTags表示排序后的标签
    public Tags SortedTags;
    // GivenTags表示给定的标签
    public Tags GivenTags;

    public LookupData(int index, in Tags sortedTags, in Tags givenTags)
    {
        this.DeferredReclaim = false;
        this.Index = index;
        this.SortedTags = sortedTags;
        this.GivenTags = givenTags;
    }
}
