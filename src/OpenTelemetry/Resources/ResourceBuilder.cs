// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources;

/// <summary>
/// 包含用于构建 <see cref="Resource"/> 实例的方法。
/// </summary>
public class ResourceBuilder
{
    // 存储资源检测器的列表
    internal readonly List<IResourceDetector> ResourceDetectors = new();
    // 默认资源
    private static readonly Resource DefaultResource;

    // 静态构造函数，用于初始化默认资源
    static ResourceBuilder()
    {
        var defaultServiceName = "unknown_service";

        try
        {
            var processName = Process.GetCurrentProcess().ProcessName;
            if (!string.IsNullOrWhiteSpace(processName))
            {
                defaultServiceName = $"{defaultServiceName}:{processName}";
            }
        }
        catch
        {
            // GetCurrentProcess 可能会抛出 PlatformNotSupportedException
        }

        DefaultResource = new Resource(new Dictionary<string, object>
        {
            [ResourceSemanticConventions.AttributeServiceName] = defaultServiceName,
        });
    }

    // 私有构造函数，防止直接实例化
    private ResourceBuilder()
    {
    }

    // 服务提供者
    internal IServiceProvider? ServiceProvider { get; set; }

    /// <summary>
    /// 创建一个 <see cref="ResourceBuilder"/> 实例，并添加默认属性。
    /// 参见 <a href="https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#semantic-attributes-with-sdk-provided-default-value">资源语义约定</a> 了解详情。
    /// 此外，它还会将从 OTEL_RESOURCE_ATTRIBUTES、OTEL_SERVICE_NAME 环境变量解析的资源属性添加到 <see cref="ResourceBuilder"/>，遵循 <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable">资源 SDK</a>。
    /// </summary>
    /// <returns>创建的 <see cref="ResourceBuilder"/>。</returns>
    public static ResourceBuilder CreateDefault()
        => new ResourceBuilder()
            .AddResource(DefaultResource)
            .AddTelemetrySdk()
            .AddEnvironmentVariableDetector();

    /// <summary>
    /// 创建一个空的 <see cref="ResourceBuilder"/> 实例。
    /// </summary>
    /// <returns>创建的 <see cref="ResourceBuilder"/>。</returns>
    public static ResourceBuilder CreateEmpty()
        => new();

    /// <summary>
    /// 清除添加到构建器的 <see cref="Resource"/>。
    /// </summary>
    /// <returns>用于链式调用的 <see cref="ResourceBuilder"/>。</returns>
    public ResourceBuilder Clear()
    {
        this.ResourceDetectors.Clear();

        return this;
    }

    /// <summary>
    /// 从添加到构建器的所有 <see cref="Resource"/> 构建一个合并的 <see cref="Resource"/>。
    /// </summary>
    /// <returns><see cref="Resource"/>。</returns>
    public Resource Build()
    {
        Resource finalResource = Resource.Empty;

        foreach (IResourceDetector resourceDetector in this.ResourceDetectors)
        {
            if (resourceDetector is ResolvingResourceDetector resolvingResourceDetector)
            {
                resolvingResourceDetector.Resolve(this.ServiceProvider);
            }

            var resource = resourceDetector.Detect();
            if (resource != null)
            {
                finalResource = finalResource.Merge(resource);
            }
        }

        return finalResource;
    }

    /// <summary>
    /// 向构建器添加一个 <see cref="IResourceDetector"/>。
    /// </summary>
    /// <param name="resourceDetector"><see cref="IResourceDetector"/>。</param>
    /// <returns>用于链式调用的 <see cref="ResourceBuilder"/>。</returns>
    public ResourceBuilder AddDetector(IResourceDetector resourceDetector)
    {
        Guard.ThrowIfNull(resourceDetector);

        this.ResourceDetectors.Add(resourceDetector);

        return this;
    }

    /// <summary>
    /// 向构建器添加一个 <see cref="IResourceDetector"/>，该检测器将使用应用程序的 <see cref="IServiceProvider"/> 进行解析。
    /// </summary>
    /// <param name="resourceDetectorFactory">资源检测器工厂。</param>
    /// <returns>用于链式调用的 <see cref="ResourceBuilder"/>。</returns>
    public ResourceBuilder AddDetector(Func<IServiceProvider, IResourceDetector> resourceDetectorFactory)
    {
        Guard.ThrowIfNull(resourceDetectorFactory);

        return this.AddDetectorInternal(sp =>
        {
            if (sp == null)
            {
                throw new NotSupportedException("IResourceDetector 工厂模式在直接调用 ResourceBuilder.Build() 时不支持。");
            }

            return resourceDetectorFactory(sp);
        });
    }

    // 内部方法，添加一个资源检测器工厂
    internal ResourceBuilder AddDetectorInternal(Func<IServiceProvider?, IResourceDetector> resourceDetectorFactory)
    {
        Guard.ThrowIfNull(resourceDetectorFactory);

        this.ResourceDetectors.Add(new ResolvingResourceDetector(resourceDetectorFactory));

        return this;
    }

    // 内部方法，添加一个资源
    internal ResourceBuilder AddResource(Resource resource)
    {
        Guard.ThrowIfNull(resource);

        this.ResourceDetectors.Add(new WrapperResourceDetector(resource));

        return this;
    }

    // 包装资源检测器类
    internal sealed class WrapperResourceDetector : IResourceDetector
    {
        private readonly Resource resource;

        public WrapperResourceDetector(Resource resource)
        {
            this.resource = resource;
        }

        public Resource Detect() => this.resource;
    }

    // 解析资源检测器类
    private sealed class ResolvingResourceDetector : IResourceDetector
    {
        private readonly Func<IServiceProvider?, IResourceDetector> resourceDetectorFactory;
        private IResourceDetector? resourceDetector;

        public ResolvingResourceDetector(Func<IServiceProvider?, IResourceDetector> resourceDetectorFactory)
        {
            this.resourceDetectorFactory = resourceDetectorFactory;
        }

        public void Resolve(IServiceProvider? serviceProvider)
        {
            this.resourceDetector = this.resourceDetectorFactory(serviceProvider)
                ?? throw new InvalidOperationException("ResourceDetector 工厂未返回 ResourceDetector 实例。");
        }

        public Resource Detect()
        {
            var detector = this.resourceDetector;

            Debug.Assert(detector != null, "detector 为 null");

            return detector?.Detect() ?? Resource.Empty;
        }
    }
}
