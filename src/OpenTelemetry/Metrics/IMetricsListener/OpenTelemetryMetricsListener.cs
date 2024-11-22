// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics;

// OpenTelemetryMetricsListener 类实现了 IMetricsListener 和 IDisposable 接口，用于监听和处理度量数据。
internal sealed class OpenTelemetryMetricsListener : IMetricsListener, IDisposable
{
    // MeterProviderSdk 实例，用于管理度量提供者。
    private readonly MeterProviderSdk meterProviderSdk;
    // 可观察仪器源（可选）。
    private IObservableInstrumentsSource? observableInstrumentsSource;

    // 构造函数，初始化 OpenTelemetryMetricsListener 实例。
    public OpenTelemetryMetricsListener(MeterProvider meterProvider)
    {
        // 将 meterProvider 转换为 MeterProviderSdk 类型。
        var meterProviderSdk = meterProvider as MeterProviderSdk;

        // 断言 meterProviderSdk 不为 null。
        Debug.Assert(meterProviderSdk != null, "meterProvider was not MeterProviderSdk");

        // 初始化 meterProviderSdk 字段。
        this.meterProviderSdk = meterProviderSdk!;

        // 订阅 OnCollectObservableInstruments 事件。
        this.meterProviderSdk.OnCollectObservableInstruments += this.OnCollectObservableInstruments;
    }

    // 获取监听器名称。
    public string Name => "OpenTelemetry";

    // 实现 IDisposable 接口，取消订阅 OnCollectObservableInstruments 事件。
    public void Dispose()
    {
        this.meterProviderSdk.OnCollectObservableInstruments -= this.OnCollectObservableInstruments;
    }

    // 获取测量处理程序。
    public MeasurementHandlers GetMeasurementHandlers()
    {
        return new MeasurementHandlers()
        {
            // 处理字节类型的测量。
            ByteHandler = (instrument, value, tags, state)
                => MeterProviderSdk.MeasurementRecordedLong(instrument, value, tags, state),
            // 处理短整型的测量。
            ShortHandler = (instrument, value, tags, state)
                => MeterProviderSdk.MeasurementRecordedLong(instrument, value, tags, state),
            // 处理整型的测量。
            IntHandler = (instrument, value, tags, state)
                => MeterProviderSdk.MeasurementRecordedLong(instrument, value, tags, state),
            // 处理长整型的测量。
            LongHandler = MeterProviderSdk.MeasurementRecordedLong,
            // 处理浮点型的测量。
            FloatHandler = (instrument, value, tags, state)
                => MeterProviderSdk.MeasurementRecordedDouble(instrument, value, tags, state),
            // 处理双精度浮点型的测量。
            DoubleHandler = MeterProviderSdk.MeasurementRecordedDouble,
        };
    }

    // 处理发布的仪器。
    public bool InstrumentPublished(Instrument instrument, out object? userState)
    {
        // 调用 InstrumentPublished 方法并返回结果。
        userState = this.meterProviderSdk.InstrumentPublished(instrument, listeningIsManagedExternally: true);
        return userState != null;
    }

    // 处理测量完成。
    public void MeasurementsCompleted(Instrument instrument, object? userState)
    {
        // 调用 MeasurementsCompleted 方法。
        MeterProviderSdk.MeasurementsCompleted(instrument, userState);
    }

    // 初始化可观察仪器源。
    public void Initialize(IObservableInstrumentsSource source)
    {
        this.observableInstrumentsSource = source;
    }

    // 收集可观察仪器的回调方法。
    private void OnCollectObservableInstruments()
    {
        // 记录可观察仪器。
        this.observableInstrumentsSource?.RecordObservableInstruments();
    }
}
