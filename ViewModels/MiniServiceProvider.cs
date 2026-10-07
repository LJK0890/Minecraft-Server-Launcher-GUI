namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 极简服务提供器：替代 Microsoft.Extensions.DependencyInjection 的容器。
///
/// 本工程只通过 Avalonia 间接引用了 DependencyInjection.Abstractions（不含容器实现），
/// 而移植过来的代码只需要一个能按类型返回服务的 <see cref="IServiceProvider"/>：
/// 命令通过反射创建（<c>ActivatorUtilities.CreateInstance</c>），服务器注册表按需解析
/// 进程与授权网关。因此这里用几十行实现同一语义，不引入新的包依赖。
/// </summary>
public sealed class MiniServiceProvider : IServiceProvider
{
    /// <summary>类型到实例工厂的映射。工厂模式使瞬态服务（如 IServerProcess）每次解析都得到新实例。</summary>
    private readonly Dictionary<Type, Func<object>> _factories = new();

    /// <summary>
    /// 以单例方式注册一个服务实例。
    /// </summary>
    /// <typeparam name="TService">服务类型（注册时使用该类型作为键）。</typeparam>
    /// <param name="instance">服务实例。</param>
    public void AddSingleton<TService>(TService instance) where TService : class
        => _factories[typeof(TService)] = () => instance;

    /// <summary>
    /// 以工厂方式注册服务；每次解析都会调用工厂，语义等价于瞬态注册。
    /// </summary>
    /// <typeparam name="TService">服务类型（注册时使用该类型作为键）。</typeparam>
    /// <param name="factory">创建服务实例的工厂。</param>
    public void AddFactory<TService>(Func<TService> factory) where TService : class
        => _factories[typeof(TService)] = () => factory();

    /// <summary>
    /// 按类型解析服务。
    /// </summary>
    /// <param name="serviceType">服务类型。</param>
    /// <returns>服务实例；未注册时返回 null。</returns>
    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider)) return this;
        return _factories.TryGetValue(serviceType, out var factory) ? factory() : null;
    }
}
