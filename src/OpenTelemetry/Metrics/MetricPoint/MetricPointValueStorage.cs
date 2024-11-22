// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace OpenTelemetry.Metrics;

// MetricPointValueStorage 结构用于存储指标点的值
[StructLayout(LayoutKind.Explicit)]
internal struct MetricPointValueStorage
{
    // 以 long 类型存储的值
    [FieldOffset(0)]
    public long AsLong;

    // 以 double 类型存储的值
    [FieldOffset(0)]
    public double AsDouble;
}
