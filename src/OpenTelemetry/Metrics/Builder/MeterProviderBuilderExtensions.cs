// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics;

/// <summary>
/// 包含 <see cref="MeterProviderBuilder"/> 类的扩展方法。
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// 向提供程序添加读取器。
    /// </summary>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="reader"><see cref="MetricReader"/>.</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder AddReader(this MeterProviderBuilder meterProviderBuilder, MetricReader reader)
    {
        // 检查 reader 是否为 null
        Guard.ThrowIfNull(reader);

        // 配置构建器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            // 如果构建器是 MeterProviderBuilderSdk 类型，则添加读取器
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.AddReader(reader);
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 向提供程序添加读取器。
    /// </summary>
    /// <remarks>
    /// 注意：由 <typeparamref name="T"/> 指定的类型将作为单例服务注册到应用程序服务中。
    /// </remarks>
    /// <typeparam name="T">读取器类型。</typeparam>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder AddReader<
#if NET
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
    T>(this MeterProviderBuilder meterProviderBuilder)
        where T : MetricReader
    {
        // 配置服务，添加单例服务
        meterProviderBuilder.ConfigureServices(services => services.TryAddSingleton<T>());

        // 配置构建器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            // 如果构建器是 MeterProviderBuilderSdk 类型，则添加读取器
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.AddReader(sp.GetRequiredService<T>());
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 向提供程序添加读取器。
    /// </summary>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="implementationFactory">创建服务的工厂。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder AddReader(
        this MeterProviderBuilder meterProviderBuilder,
        Func<IServiceProvider, MetricReader> implementationFactory)
    {
        // 检查 implementationFactory 是否为 null
        Guard.ThrowIfNull(implementationFactory);

        // 配置构建器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            // 如果构建器是 MeterProviderBuilderSdk 类型，则添加读取器
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.AddReader(implementationFactory(sp));
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 添加度量视图，可用于自定义 SDK 输出的度量。视图按添加顺序应用。
    /// </summary>
    /// <remarks>查看规范：https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view。</remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="instrumentName">仪器名称，用作仪器选择标准的一部分。</param>
    /// <param name="name">视图名称。这将用作生成的度量流的名称。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder AddView(this MeterProviderBuilder meterProviderBuilder, string instrumentName, string name)
    {
        // 检查视图名称是否有效
        if (!MeterProviderBuilderSdk.IsValidInstrumentName(name))
        {
            throw new ArgumentException($"自定义视图名称 {name} 无效。", nameof(name));
        }

        // 检查仪器名称是否包含通配符
        if (instrumentName.IndexOf('*') != -1)
        {
            throw new ArgumentException(
                $"仪器选择标准无效。仪器名称 '{instrumentName}' 包含通配符字符。使用视图重命名度量流时不允许这样做，因为这会导致度量流名称冲突。",
                nameof(instrumentName));
        }

        // 添加视图
        meterProviderBuilder.AddView(instrumentName, new MetricStreamConfiguration { Name = name });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 添加度量视图，可用于自定义 SDK 输出的度量。视图按添加顺序应用。
    /// </summary>
    /// <remarks>查看规范：https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view。</remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="instrumentName">仪器名称，用作仪器选择标准的一部分。</param>
    /// <param name="metricStreamConfiguration">用于生成度量流的聚合配置。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder AddView(this MeterProviderBuilder meterProviderBuilder, string instrumentName, MetricStreamConfiguration metricStreamConfiguration)
    {
        // 检查 instrumentName 和 metricStreamConfiguration 是否为 null 或空白
        Guard.ThrowIfNullOrWhitespace(instrumentName);
        Guard.ThrowIfNull(metricStreamConfiguration);

        // 检查仪器名称是否包含通配符
        if (metricStreamConfiguration.Name != null && instrumentName.IndexOf('*') != -1)
        {
            throw new ArgumentException(
                $"仪器选择标准无效。仪器名称 '{instrumentName}' 包含通配符字符。使用视图重命名度量流时不允许这样做，因为这会导致度量流名称冲突。",
                nameof(instrumentName));
        }

        // 配置构建器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            // 如果构建器是 MeterProviderBuilderSdk 类型，则添加视图
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                // 如果仪器名称包含通配符，则使用正则表达式匹配
                if (instrumentName.IndexOf('*') != -1)
                {
                    var pattern = '^' + Regex.Escape(instrumentName).Replace("\\*", ".*");
                    var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    meterProviderBuilderSdk.AddView(instrument => regex.IsMatch(instrument.Name) ? metricStreamConfiguration : null);
                }
                else
                {
                    meterProviderBuilderSdk.AddView(instrument => instrument.Name.Equals(instrumentName, StringComparison.OrdinalIgnoreCase) ? metricStreamConfiguration : null);
                }
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 添加度量视图，可用于自定义 SDK 输出的度量。视图按添加顺序应用。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>注意：从 <paramref name="viewConfig"/> 返回的无效 <see cref="MetricStreamConfiguration"/> 将导致视图被忽略，运行时不会抛出错误。</item>
    /// <item>查看规范：https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view。</item>
    /// </list>
    /// </remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="viewConfig">根据仪器配置聚合的函数。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder AddView(this MeterProviderBuilder meterProviderBuilder, Func<Instrument, MetricStreamConfiguration?> viewConfig)
    {
        // 检查 viewConfig 是否为 null
        Guard.ThrowIfNull(viewConfig);

        // 配置构建器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            // 如果构建器是 MeterProviderBuilderSdk 类型，则添加视图
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.AddView(viewConfig);
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 设置 MeterProvider 支持的最大度量流数。
    /// 当未配置视图时，每个仪器将产生一个度量流，因此这控制了支持的仪器数量。
    /// 当配置视图时，单个仪器可以产生多个度量流，因此这控制了流的数量。
    /// </summary>
    /// <remarks>
    /// 如果创建了一个仪器，但稍后被释放，它仍将计入限制。这可能会在将来发生变化。
    /// </remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="maxMetricStreams">允许的最大度量流数。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder SetMaxMetricStreams(this MeterProviderBuilder meterProviderBuilder, int maxMetricStreams)
    {
        // 检查 maxMetricStreams 是否在范围内
        Guard.ThrowIfOutOfRange(maxMetricStreams, min: 1);

        // 配置构建器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            // 如果构建器是 MeterProviderBuilderSdk 类型，则设置度量限制
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.SetMetricLimit(maxMetricStreams);
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 设置每个度量流允许的最大 MetricPoints 数量。这限制了用于报告测量值的键/值对的唯一组合的数量。
    /// </summary>
    /// <remarks>
    /// 如果某个特定的键/值对组合至少使用过一次，它将在进程的生命周期内计入限制。这可能会在将来发生变化。查看：https://github.com/open-telemetry/opentelemetry-dotnet/issues/2360。
    /// </remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="maxMetricPointsPerMetricStream">每个度量流允许的最大度量点数。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    [Obsolete("使用 MetricStreamConfiguration.CardinalityLimit 通过 AddView API 代替。此方法在版本 1.10.0 中标记为过时，并将在未来版本中删除。")]
    public static MeterProviderBuilder SetMaxMetricPointsPerMetricStream(this MeterProviderBuilder meterProviderBuilder, int maxMetricPointsPerMetricStream)
    {
        // 检查 maxMetricPointsPerMetricStream 是否在范围内
        Guard.ThrowIfOutOfRange(maxMetricPointsPerMetricStream, min: 1);

        // 配置构建器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            // 如果构建器是 MeterProviderBuilderSdk 类型，则设置默认基数限制
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.SetDefaultCardinalityLimit(maxMetricPointsPerMetricStream);
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 设置与此提供程序关联的资源的 <see cref="ResourceBuilder"/>。覆盖当前设置的 ResourceBuilder。
    /// 通常应使用 <see cref="ConfigureResource(MeterProviderBuilder, Action{ResourceBuilder})"/> 代替（如果需要，请调用 <see cref="ResourceBuilder.Clear"/>）。
    /// </summary>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>，从中将构建资源。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder SetResourceBuilder(this MeterProviderBuilder meterProviderBuilder, ResourceBuilder resourceBuilder)
    {
        // 检查 resourceBuilder 是否为 null
        Guard.ThrowIfNull(resourceBuilder);

        // 配置构建器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            // 如果构建器是 MeterProviderBuilderSdk 类型，则设置资源构建器
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.SetResourceBuilder(resourceBuilder);
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 修改与此提供程序关联的资源的 <see cref="ResourceBuilder"/>。
    /// </summary>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="configure">一个操作，用于修改提供的 <see cref="ResourceBuilder"/>。</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder ConfigureResource(this MeterProviderBuilder meterProviderBuilder, Action<ResourceBuilder> configure)
    {
        // 检查 configure 是否为 null
        Guard.ThrowIfNull(configure);

        // 配置构建器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            // 如果构建器是 MeterProviderBuilderSdk 类型，则配置资源
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.ConfigureResource(configure);
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// 运行给定的操作以初始化 <see cref="MeterProvider"/>。
    /// </summary>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <returns><see cref="MeterProvider"/>.</returns>
    public static MeterProvider Build(this MeterProviderBuilder meterProviderBuilder)
    {
        // 如果 meterProviderBuilder 是 MeterProviderBuilderBase 类型，则调用 InvokeBuild 方法
        if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
        {
            return meterProviderBuilderBase.InvokeBuild();
        }

        // 否则抛出不支持的异常
        throw new NotSupportedException($"在 '{meterProviderBuilder?.GetType().FullName ?? "null"}' 实例上不支持 Build。");
    }

    /// <summary>
    /// 设置提供程序的默认 <see cref="ExemplarFilterType"/>。
    /// </summary>
    /// <remarks>
    /// <para>注意：
    /// <list type="bullet">
    /// <item>配置的 <see cref="ExemplarFilterType"/> 控制如何将测量值提供给 <see cref="ExemplarReservoir"/>，后者负责在度量上存储 <see cref="Exemplar"/>。</item>
    /// <item>默认提供程序配置为 <see cref="ExemplarFilterType.AlwaysOff"/>。</item>
    /// <item>使用 <see cref="ExemplarFilterType.TraceBased"/> 或 <see cref="ExemplarFilterType.AlwaysOn"/> 启用提供程序管理的所有度量的 <see cref="Exemplar"/>。</item>
    /// <item>如果通过配置的 <see cref="ExemplarFilterType"/> 在提供程序上启用了 <see cref="Exemplar"/>，则将使用规范中描述的默认值在度量上配置 <see cref="ExemplarReservoir"/>：<see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplar-defaults"/>。要更改度量的 <see cref="ExemplarReservoir"/>，请使用 <c>AddView</c> API 和 <see cref="MetricStreamConfiguration.ExemplarReservoirFactory"/>。</item>
    /// </list>
    /// </para>
    /// <para>规范参考：<see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplarfilter"/>。</para>
    /// </remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="exemplarFilter"><see cref="ExemplarFilterType"/>.</param>
    /// <returns>返回 <see cref="MeterProviderBuilder"/> 以便链式调用。</returns>
    public static MeterProviderBuilder SetExemplarFilter(
        this MeterProviderBuilder meterProviderBuilder,
        ExemplarFilterType exemplarFilter)
    {
        // 配置构建器
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            // 如果构建器是 MeterProviderBuilderSdk 类型，则设置示例过滤器
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                switch (exemplarFilter)
                {
                    case ExemplarFilterType.AlwaysOn:
                    case ExemplarFilterType.AlwaysOff:
                    case ExemplarFilterType.TraceBased:
                        meterProviderBuilderSdk.SetExemplarFilter(exemplarFilter);
                        break;
                    default:
                        throw new NotSupportedException($"不支持的 ExemplarFilterType '{exemplarFilter}'。");
                }
            }
        });

        return meterProviderBuilder;
    }
}
