using Microsoft.Extensions.DependencyInjection;
using MinecraftServerLauncher.Models.Domain;
using MinecraftServerLauncher.Models.Infrastructure;
using MinecraftServerLauncher.Models.Ports;
using MinecraftServerLauncher.Models.UseCases;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 应用服务组合根：把移植过来的 MSL-CLI 各层组装成一个可用的对象图。
///
/// 对应 MSL-CLI 的 <c>Program.cs</c> 中的 DI 组装部分——那一份是控制台入口（含
/// 控制台输入循环），本工程用 Avalonia 界面替换了输入/输出、其余依赖关系保持一致：
/// <list type="bullet">
/// <item>输出：界面日志 + 文件日志（复合写入器），替代控制台 + 文件；</item>
/// <item>输入：界面提问适配器，替代控制台独占读取；</item>
/// <item>其余服务（服务器注册表、命令解析器、授权网关、AI 服务、命令执行器）装配顺序与原实现一致。</item>
/// </list>
/// </summary>
public sealed class AppServices : IDisposable
{
    /// <summary>
    /// 应用数据目录名（%APPDATA% 下的子目录）。
    /// 与 MSL-CLI 保持一致，使界面版与命令行版共用同一份
    /// <c>config.json</c> 与日志目录，命令行里登记过的服务器可直接在界面中管理。
    /// </summary>
    public const string AppDataFolderName = "MSL_CLI";

    /// <summary>文件日志写入器，释放时需要显式关闭。</summary>
    private readonly FileOutputWriter _fileOutput;
    /// <summary>配置存储的具体实现，用于暴露实际使用的配置文件路径。</summary>
    private readonly FileConfigurationStore _store;
    /// <summary>AI 服务，持有后台消费者任务，释放时需要停止。</summary>
    private readonly OpenAiAgentService _agent;
    /// <summary>服务器注册表，持有各服务器的进程包装，释放时需要一并释放。</summary>
    private readonly ServerRegistry _registry;
    /// <summary>是否已释放。</summary>
    private bool _disposed;

    /// <summary>启动时加载的应用配置（运行中可能被 <c>$app reload</c> 改写，需重新读取）。</summary>
    public AppConfig Config { get; private set; }
    /// <summary>配置存储，供界面在命令执行后重新读取配置。</summary>
    public IConfigurationStore ConfigStore { get; }
    /// <summary>本次运行实际使用的配置文件路径，便于排查“改了没生效”一类问题。</summary>
    public string UserConfigPath => _store.UserConfigPath;
    /// <summary>输出写入器（界面 + 文件）。</summary>
    public IOutputWriter Output { get; }
    /// <summary>输入读取器（界面提问适配器）。</summary>
    public IInputReader Input { get; }
    /// <summary>命令解析器，已注册全部命令。</summary>
    public ICommandParser Parser { get; }
    /// <summary>命令执行器，界面执行 <c>$</c> 命令的入口。</summary>
    public ICommandExecutor Executor { get; }
    /// <summary>服务器注册表。</summary>
    public IServerRegistry Registry => _registry;
    /// <summary>AI 代理服务。</summary>
    public IAgentService Agent => _agent;

    /// <summary>
    /// 组装全部服务并注册命令。
    /// </summary>
    /// <param name="sink">界面日志汇聚点。</param>
    public AppServices(LogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        // ----- 输出适配器（界面 + 文件），对应原实现的控制台 + 文件 -----
        var guiOutput = new GuiOutputWriter(sink);
        _fileOutput = new FileOutputWriter(AppDataFolderName);
        Output = new CompositeOutputWriter(guiOutput, _fileOutput);

        // ----- 配置加载 -----
        // 先构造输出器，使配置解析错误也能报告到界面与日志
        _store = new FileConfigurationStore(AppDataFolderName, Output);
        ConfigStore = _store;
        Config = _store.LoadConfig();

        // ----- 输入适配器 -----
        Input = new GuiInputReader();

        // ----- 服务容器 -----
        var provider = new MiniServiceProvider();
        provider.AddSingleton(Output);
        provider.AddSingleton(ConfigStore);
        provider.AddSingleton(Config);
        provider.AddSingleton(Input);
        // 每台服务器需要独立的进程实例，因此按工厂注册（等价于原实现的 AddTransient）
        provider.AddFactory<IServerProcess>(() => new ServerProcess());

        // ----- 命令解析器（下一步注册命令） -----
        Parser = new CommandParser();
        provider.AddSingleton(Parser);

        // ----- 服务器注册表 -----
        // 注意：IAgentService 用惰性工厂传给注册表，避免
        // ICommandExecutor → IServerRegistry → IAgentService → IServerRegistry 的构造循环
        _registry = new ServerRegistry(
            Config,
            Output,
            provider,
            () => provider.GetRequiredService<IAgentService>());
        provider.AddSingleton<IServerRegistry>(_registry);

        // ----- 代理命令授权网关：执行代理命令前向本地操作员提问 -----
        var gateway = new AgentPermissionGateway(Input, Output, ConfigStore, Parser, _registry);
        provider.AddSingleton<IAgentPermissionGateway>(gateway);

        // ----- AI 对话 / 智能体服务 -----
        _agent = new OpenAiAgentService(Config, provider, Output, Parser, gateway, _registry, ConfigStore);
        provider.AddSingleton<IAgentService>(_agent);

        // ----- 命令执行器 -----
        Executor = new CommandExecutor(Parser, _registry, _agent, ConfigStore);
        provider.AddSingleton<ICommandExecutor>(Executor);

        // ----- 通过反射注册全部命令 -----
        Parser.RegisterCommands(provider);
    }

    /// <summary>
    /// 重新从磁盘加载配置。命令（如 <c>$app cfg set</c>、<c>$server lk</c>）会改写配置文件，
    /// 界面在命令执行后调用本方法以刷新锁定/隐藏/服务器列表等展示状态。
    /// </summary>
    /// <returns>最新配置。</returns>
    public AppConfig ReloadConfig()
    {
        Config = ConfigStore.LoadConfig();
        return Config;
    }

    /// <summary>
    /// 释放全部服务：先停 AI 消费者，再释放注册表（含各服务器进程）与文件日志。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 释放顺序与创建顺序相反：AI 消费者可能仍持有命令执行器，先停它
        try { _agent.Dispose(); } catch { /* 清理阶段的异常不应阻断后续释放 */ }
        try { _registry.Dispose(); } catch { /* 同上 */ }
        try { _fileOutput.Dispose(); } catch { /* 同上 */ }
    }
}
