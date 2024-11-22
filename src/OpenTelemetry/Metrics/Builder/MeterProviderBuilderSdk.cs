// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 存储用于构建 <see cref="MeterProvider"/> 的状态。
/// </summary>
internal sealed class MeterProviderBuilderSdk : MeterProviderBuilder, IMeterProviderBuilder
{
    // 默认度量限制
    public const int DefaultMetricLimit = 1000;
    // 默认基数限制
    public const int DefaultCardinalityLimit = 2000;
    // 默认仪器版本
    private const string DefaultInstrumentationVersion = "1.0.0.0";

    // 服务提供者
    private readonly IServiceProvider serviceProvider;
    // 度量提供者
    private MeterProviderSdk? meterProvider;

    // 构造函数，初始化服务提供者
    public MeterProviderBuilderSdk(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    // 注意：我们不在这里使用 static readonly，因为某些客户使用反射替换它，这在 initonly static 字段上是不允许的。
    // 参见：https://github.com/dotnet/runtime/issues/11571。
    // 客户：这不能保证永远有效。我们可能会在未来更改此机制，风险自负。
    public static Regex InstrumentNameRegex { get; set; } = new(
        @"^[a-z][a-z0-9-._/]{0,254}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 仪器注册列表
    public List<InstrumentationRegistration> Instrumentation { get; } = new();

    // 资源构建器
    public ResourceBuilder? ResourceBuilder { get; private set; }

    // 示例过滤器类型
    public ExemplarFilterType? ExemplarFilter { get; private set; }

    // 度量提供者
    public MeterProvider? Provider => this.meterProvider;

    // 度量读取器列表
    public List<MetricReader> Readers { get; } = new();

    // 度量源列表
    public List<string> MeterSources { get; } = new();

    // 视图配置列表
    public List<Func<Instrument, MetricStreamConfiguration?>> ViewConfigs { get; } = new();

    // 度量限制
    public int MetricLimit { get; private set; } = DefaultMetricLimit;

    // 基数限制
    public int CardinalityLimit { get; private set; } = DefaultCardinalityLimit;

    /// <summary>
    /// 返回给定的仪器名称是否根据规范有效。
    /// </summary>
    /// <remarks>参见规范：<see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument"/>。</remarks>
    /// <param name="instrumentName">仪器名称。</param>
    /// <returns>指示仪器是否有效的布尔值。</returns>
    public static bool IsValidInstrumentName(string instrumentName)
    {
        if (string.IsNullOrWhiteSpace(instrumentName))
        {
            return false;
        }

        return InstrumentNameRegex.IsMatch(instrumentName);
    }

    /// <summary>
    /// 返回给定的自定义视图名称是否根据规范有效。
    /// </summary>
    /// <remarks>参见规范：<see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument"/>。</remarks>
    /// <param name="customViewName">视图名称。</param>
    /// <returns>指示视图是否有效的布尔值。</returns>
    public static bool IsValidViewName(string customViewName)
    {
        // 仅在视图名称不为 null 的情况下验证视图名称。如果为 null，视图名称将根据规范为仪器名称。
        if (customViewName == null)
        {
            return true;
        }

        return InstrumentNameRegex.IsMatch(customViewName);
    }

    // 注册度量提供者
    public void RegisterProvider(MeterProviderSdk meterProvider)
    {
        Debug.Assert(meterProvider != null, "meterProvider 为空");

        if (this.meterProvider != null)
        {
            throw new NotSupportedException("在构建执行期间无法访问 MeterProvider。");
        }

        this.meterProvider = meterProvider;
    }

    // 添加仪器
    public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        Debug.Assert(instrumentationFactory != null, "instrumentationFactory 为空");

        return this.AddInstrumentation(
            typeof(TInstrumentation).Name,
            typeof(TInstrumentation).Assembly.GetName().Version?.ToString() ?? DefaultInstrumentationVersion,
            instrumentationFactory!());
    }

    // 添加仪器
    public MeterProviderBuilder AddInstrumentation(
        string instrumentationName,
        string instrumentationVersion,
        object? instrumentation)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(instrumentationName), "instrumentationName 为空或空白");
        Debug.Assert(!string.IsNullOrWhiteSpace(instrumentationVersion), "instrumentationVersion 为空或空白");

        this.Instrumentation.Add(
            new InstrumentationRegistration(
                instrumentationName,
                instrumentationVersion,
                instrumentation));

        return this;
    }

    // 配置资源
    public MeterProviderBuilder ConfigureResource(Action<ResourceBuilder> configure)
    {
        Debug.Assert(configure != null, "configure 为空");

        var resourceBuilder = this.ResourceBuilder ??= ResourceBuilder.CreateDefault();

        configure!(resourceBuilder);

        return this;
    }

    // 设置资源构建器
    public MeterProviderBuilder SetResourceBuilder(ResourceBuilder resourceBuilder)
    {
        Debug.Assert(resourceBuilder != null, "resourceBuilder 为空");

        this.ResourceBuilder = resourceBuilder;

        return this;
    }

    // 设置示例过滤器
    public MeterProviderBuilder SetExemplarFilter(ExemplarFilterType exemplarFilter)
    {
        this.ExemplarFilter = exemplarFilter;

        return this;
    }

    // 添加度量
    public override MeterProviderBuilder AddMeter(params string[] names)
    {
        Debug.Assert(names != null, "names 为空");

        foreach (var name in names!)
        {
            Guard.ThrowIfNullOrWhitespace(name);

            this.MeterSources.Add(name);
        }

        return this;
    }

    // 添加读取器
    public MeterProviderBuilder AddReader(MetricReader reader)
    {
        Debug.Assert(reader != null, "reader 为空");

        this.Readers.Add(reader!);

        return this;
    }

    // 添加视图
    public MeterProviderBuilder AddView(Func<Instrument, MetricStreamConfiguration?> viewConfig)
    {
        Debug.Assert(viewConfig != null, "viewConfig 为空");

        this.ViewConfigs.Add(viewConfig!);

        return this;
    }

    // 设置度量限制
    public MeterProviderBuilder SetMetricLimit(int metricLimit)
    {
        this.MetricLimit = metricLimit;

        return this;
    }

    // 设置默认基数限制
    public MeterProviderBuilder SetDefaultCardinalityLimit(int cardinalityLimit)
    {
        this.CardinalityLimit = cardinalityLimit;

        return this;
    }

    // 配置构建器
    public MeterProviderBuilder ConfigureBuilder(Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        Debug.Assert(configure != null, "configure 为空");

        configure!(this.serviceProvider, this);

        return this;
    }

    // 配置服务
    public MeterProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        throw new NotSupportedException("在创建 ServiceProvider 后无法配置服务。");
    }

    MeterProviderBuilder IDeferredMeterProviderBuilder.Configure(Action<IServiceProvider, MeterProviderBuilder> configure)
        => this.ConfigureBuilder(configure);

    // 仪器注册结构
    internal readonly struct InstrumentationRegistration
    {
        public readonly string Name;
        public readonly string Version;
        public readonly object? Instance;

        internal InstrumentationRegistration(string name, string version, object? instance)
        {
            this.Name = name;
            this.Version = version;
            this.Instance = instance;
        }
    }
}
