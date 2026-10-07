using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinecraftServerLauncher.Models.Domain;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 主窗口视图模型：应用外壳本身。
///
/// 结构对应界面：标题栏（左侧标题、中间页面选择、右侧窗口按钮）下面是页面内容。
/// 本类负责组装服务、维护各页面共享的数据（服务器实例块、AI 实例、日志），
/// 并按配置的间隔周期刷新实例状态。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IAppShell, IDisposable
{
    /// <summary>日志汇聚点，界面日志集合的来源。</summary>
    private readonly LogSink _logSink;
    /// <summary>组装好的服务集合；初始化失败时为 null（界面仍可显示错误日志）。</summary>
    private AppServices? _services;
    /// <summary>界面输入适配器，用于提交授权提问的作答。</summary>
    private GuiInputReader? _inputReader;
    /// <summary>周期性刷新服务器状态的定时器。</summary>
    private DispatcherTimer? _statusTimer;
    /// <summary>当前定时器使用的间隔（毫秒），用于判断是否需要按新配置重建。</summary>
    private int _statusTimerIntervalMs;
    /// <summary>上一轮状态刷新是否仍在进行，避免慢速 Query 造成轮询堆积。</summary>
    private bool _metricsBusy;
    /// <summary>进程退出事件处理器，保证文件日志与 AI 消费者被正确收尾。</summary>
    private EventHandler? _processExitHandler;
    /// <summary>全部页面（用于统一刷新）。</summary>
    private readonly List<IRefreshablePage> _pages = new();
    /// <summary>主页面视图模型；刷新实例块集合时需要直接访问它的选中项。</summary>
    private MainPageViewModel? _mainPage;
    /// <summary>是否已释放。</summary>
    private bool _disposed;

    /// <summary>
    /// 构造视图模型并完成服务初始化。
    /// 视图（App.axaml.cs 与 XAML 预览器）都使用这个无参构造，因此这里必须自给自足。
    /// </summary>
    public MainWindowViewModel() : this(null)
    {
    }

    /// <summary>
    /// 构造视图模型并指定启动时打开的页面。
    /// </summary>
    /// <param name="initialPage">
    /// 启动页；null 表示主页面。由命令行参数 <c>--page &lt;名称&gt;</c> 指定。
    /// </param>
    public MainWindowViewModel(AppPage? initialPage)
    {
        _logSink = new LogSink();
        Logs = _logSink.Entries;

        // 设计时预览不启动任何后台服务：避免预览器里读配置、起进程、开日志文件
        var designMode = Design.IsDesignMode;
        if (!designMode)
            Initialize();

        BuildPages();

        if (initialPage is { } page)
        {
            var tab = Pages.FirstOrDefault(p => p.Page == page);
            if (tab != null) SelectedPage = tab;
        }
    }

    // ------------------------------------------------------------------
    // 共享数据（IAppShell）
    // ------------------------------------------------------------------

    /// <summary>组装好的服务集合；初始化失败时为 null。</summary>
    public AppServices? Services => _services;

    /// <summary>界面日志汇聚点。</summary>
    public LogSink Log => _logSink;

    /// <summary>服务器实例块集合。</summary>
    public ObservableCollection<ServerItemViewModel> Servers { get; } = new();

    /// <summary>服务器实例编辑卡片集合（配置页每个实例一栏）。</summary>
    public ObservableCollection<ServerEditorViewModel> ServerEditors { get; } = new();

    /// <summary>AI 实例集合。</summary>
    public ObservableCollection<AiConfigItemViewModel> AiConfigs { get; } = new();

    /// <summary>控制台日志行集合。</summary>
    public ObservableCollection<LogEntryViewModel> Logs { get; }

    // ------------------------------------------------------------------
    // 页面导航
    // ------------------------------------------------------------------

    /// <summary>标题栏上的页面标签集合。</summary>
    public ObservableCollection<PageTab> Pages { get; } = new();

    /// <summary>当前选中的页面标签。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPageContent))]
    [NotifyPropertyChangedFor(nameof(CurrentPageTitle))]
    private PageTab? _selectedPage;

    /// <summary>当前页面内容（交给 ViewLocator 解析成对应的 View）。</summary>
    public object? CurrentPageContent => SelectedPage?.ViewModel;

    /// <summary>当前页面标题。</summary>
    public string CurrentPageTitle => SelectedPage?.Title ?? string.Empty;

    // ------------------------------------------------------------------
    // 标题栏状态
    // ------------------------------------------------------------------

    /// <summary>顶部标语；初始化后显示已加载的服务器与 AI 配置数量。</summary>
    [ObservableProperty]
    private string _greeting = "正在初始化 ...";

    /// <summary>当前高亮（控制台输入转发目标）的服务器名。</summary>
    [ObservableProperty]
    private string? _highlightedServerName;

    // ------------------------------------------------------------------
    // 代理授权提问
    // ------------------------------------------------------------------

    /// <summary>是否有授权提问在等待作答。</summary>
    [ObservableProperty]
    private bool _isPromptPending;

    /// <summary>操作员对授权提问的作答（y / a / n）。</summary>
    [ObservableProperty]
    private string _promptResponse = string.Empty;

    // ------------------------------------------------------------------
    // 初始化
    // ------------------------------------------------------------------

    /// <summary>
    /// 组装服务、挂接输入与提问回调，并启动状态刷新定时器。
    /// 任何初始化异常都转为一条致命日志，避免界面直接崩溃。
    /// </summary>
    private void Initialize()
    {
        try
        {
            _services = new AppServices(_logSink);
        }
        catch (Exception ex)
        {
            Greeting = "初始化失败，请查看日志";
            _logSink.Add("App", LogLevel.Fatal, $"初始化失败: {ex.Message}");
            return;
        }

        // 输入适配器：授权提问状态变化 + 界面直接输入
        if (_services.Input is GuiInputReader reader)
        {
            _inputReader = reader;
            reader.PromptStateChanged += OnPromptStateChanged;
        }

        // 进程退出时释放服务
        _processExitHandler = (_, _) => Dispose();
        AppDomain.CurrentDomain.ProcessExit += _processExitHandler;

        // 命令行版本要求先 $server hl 指定目标；图形界面里实例块与下拉框总要有默认选中项，
        // 否则会出现“下拉框显示着 A，但高亮服务器为空”的不一致。这里补一个默认高亮。
        if (_services.Registry.HighlightedServerName == null)
        {
            var first = _services.Registry.All.Keys.OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
            if (first != null) _services.Registry.SwitchHighlight(first);
        }

        WriteLog("Config", LogLevel.Info, $"配置文件: {_services.UserConfigPath}");
        WriteLog("Command", LogLevel.Info, $"已注册 {_services.Parser.GetCommandDescriptions().Count} 个命令");

        SyncServers();
        SyncAiConfigs();
        RestartStatusTimer();

        Greeting = $"已加载 {Servers.Count} 台服务器 / {AiConfigs.Count} 个 AI 配置";
        WriteLog("GLOBAL", LogLevel.Success, "MSL-CLI 图形前端已就绪，可直接使用 $ 命令与 AI 面板");
    }

    /// <summary>
    /// 按标题栏顺序创建各页面。
    /// </summary>
    private void BuildPages()
    {
        var main = new MainPageViewModel(this);
        var serverConfig = new ServerConfigPageViewModel(this);
        var aiConfig = new AiConfigPageViewModel(this);
        var appSettings = new AppSettingsPageViewModel(this);
        var about = new AboutPageViewModel(this);

        _mainPage = main;

        Pages.Add(new PageTab(AppPage.Main, "主页面", main));
        Pages.Add(new PageTab(AppPage.ServerConfig, "服务器实例配置", serverConfig));
        Pages.Add(new PageTab(AppPage.AiConfig, "AI 实例配置", aiConfig));
        Pages.Add(new PageTab(AppPage.AppSettings, "APP 设置", appSettings));
        Pages.Add(new PageTab(AppPage.About, "关于", about));

        _pages.Add(main);
        _pages.Add(serverConfig);
        _pages.Add(aiConfig);
        _pages.Add(appSettings);
        _pages.Add(about);

        SelectedPage = Pages.FirstOrDefault();
        ReloadConfigAndRefresh();
    }

    // ------------------------------------------------------------------
    // IAppShell
    // ------------------------------------------------------------------

    /// <summary>
    /// 写一条界面日志。
    /// </summary>
    /// <param name="context">日志上下文。</param>
    /// <param name="level">日志级别。</param>
    /// <param name="message">日志正文。</param>
    public void WriteLog(string context, LogLevel level, string message)
    {
        var output = _services?.Output;
        if (output != null)
            output.Write(context, level, message);
        else
            _logSink.Add(context, level, message);
    }

    /// <summary>
    /// 执行一条 <c>$</c> 命令，随后重新加载配置并刷新所有页面。
    /// </summary>
    /// <param name="line">命令文本。</param>
    public async Task RunCommandAsync(string line)
    {
        if (_services == null || string.IsNullOrWhiteSpace(line)) return;

        try
        {
            var result = await _services.Executor.ExecuteAsync(line, _services.Output);

            // 命令可能改写了配置文件（cfg set / server lk / ai add 等），刷新界面状态
            ReloadConfigAndRefresh();

            // 执行器请求退出（$app exit）：关闭应用
            if (result is { ExitRequested: true })
                RequestExit();
        }
        catch (Exception ex)
        {
            WriteLog("Command", LogLevel.Error, $"执行异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 重新从磁盘加载配置并重建全部共享集合。
    /// </summary>
    public void ReloadConfigAndRefresh()
    {
        if (_services == null)
        {
            RefreshPages();
            return;
        }

        try
        {
            _services.ReloadConfig();
        }
        catch (Exception ex)
        {
            WriteLog("Config", LogLevel.Error, $"重新加载配置失败: {ex.Message}");
            return;
        }

        SyncServers();
        SyncAiConfigs();
        RestartStatusTimer();
        RefreshPages();
        HighlightedServerName = _services.Registry.HighlightedServerName;
    }

    /// <summary>
    /// 重建服务器实例块集合，尽量保留当前选中项（按名称）。
    /// </summary>
    private void SyncServers()
    {
        if (_services == null) return;

        var config = _services.Config;
        var registry = _services.Registry;
        var highlighted = registry.HighlightedServerName;

        Servers.Clear();
        foreach (var pair in registry.All.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var item = new ServerItemViewModel(pair.Value, registry, this);
            item.Refresh(config, highlighted);
            Servers.Add(item);
        }

        // 主页面的选中项与目标下拉框由它自己在 RefreshFromConfig 里按名称恢复
        SyncServerEditors();
    }

    /// <summary>
    /// 让服务器编辑卡片与实例块集合保持一致：同名卡片沿用原对象
    /// （保住展开状态与正在编辑的输入），只把内部的服务器实例换掉。
    /// </summary>
    private void SyncServerEditors()
    {
        // 先剔除已不存在的实例
        for (var i = ServerEditors.Count - 1; i >= 0; i--)
        {
            if (!Servers.Any(s => string.Equals(s.Name, ServerEditors[i].Name, StringComparison.Ordinal)))
                ServerEditors.RemoveAt(i);
        }

        for (var i = 0; i < Servers.Count; i++)
        {
            var item = Servers[i];
            var existing = ServerEditors.FirstOrDefault(
                e => string.Equals(e.Name, item.Name, StringComparison.Ordinal));

            if (existing == null)
            {
                ServerEditors.Insert(Math.Min(i, ServerEditors.Count), new ServerEditorViewModel(item, this));
                continue;
            }

            existing.Update(item);

            var current = ServerEditors.IndexOf(existing);
            if (current != i) ServerEditors.Move(current, i);
        }
    }

    /// <summary>
    /// 重建 AI 实例集合，尽量保留各页面原先选中的实例（按名称）。
    /// </summary>
    private void SyncAiConfigs()
    {
        if (_services == null) return;

        var config = _services.Config;
        var ordered = config.AIConfigs.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();

        // 剔除已被删除的实例
        for (var i = AiConfigs.Count - 1; i >= 0; i--)
        {
            if (!config.AIConfigs.ContainsKey(AiConfigs[i].Name))
                AiConfigs.RemoveAt(i);
        }

        // 同名实例沿用原对象并原地更新：这样卡片的展开状态、以及控制台下拉框的选中项
        // 都不会因为一次配置重载而丢失
        for (var i = 0; i < ordered.Count; i++)
        {
            var name = ordered[i].Key;
            var aiConfig = ordered[i].Value;
            var isDefault = string.Equals(name, config.DefaultAIConfig, StringComparison.OrdinalIgnoreCase);

            var existing = AiConfigs.FirstOrDefault(
                a => string.Equals(a.Name, name, StringComparison.Ordinal));

            if (existing == null)
            {
                AiConfigs.Insert(Math.Min(i, AiConfigs.Count),
                    new AiConfigItemViewModel(name, aiConfig, isDefault, this));
                continue;
            }

            existing.Update(aiConfig, isDefault);

            var current = AiConfigs.IndexOf(existing);
            if (current != i) AiConfigs.Move(current, i);
        }
    }

    /// <summary>
    /// 通知全部页面按新配置刷新展示状态。
    /// </summary>
    private void RefreshPages()
    {
        foreach (var page in _pages)
        {
            try
            {
                page.RefreshFromConfig();
            }
            catch (Exception ex)
            {
                WriteLog("GLOBAL", LogLevel.Error, $"刷新页面状态失败: {ex.Message}");
            }
        }
    }

    // ------------------------------------------------------------------
    // 状态刷新
    // ------------------------------------------------------------------

    /// <summary>
    /// 按当前配置的间隔（重新）启动状态刷新定时器。
    /// 间隔来自 <see cref="AppConfig.StatusQueryIntervalMs"/>，默认 2500 毫秒。
    /// </summary>
    private void RestartStatusTimer()
    {
        if (_services == null) return;

        var intervalMs = _services.Config.StatusQueryIntervalMs;
        if (_statusTimer != null && intervalMs == _statusTimerIntervalMs) return;

        if (_statusTimer != null)
        {
            _statusTimer.Stop();
            _statusTimer.Tick -= OnStatusTimerTick;
        }

        _statusTimerIntervalMs = intervalMs;
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
        _statusTimer.Tick += OnStatusTimerTick;
        _statusTimer.Start();
    }

    /// <summary>
    /// 定时刷新服务器状态：运行状态与锁定/隐藏标记每次刷新，
    /// 玩家数量（Query）与 CPU / 内存（进程）按同一间隔更新。
    /// 上一轮尚未结束时跳过本轮，避免慢速 Query 造成任务堆积。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private async void OnStatusTimerTick(object? sender, EventArgs e)
    {
        if (_services == null || _metricsBusy) return;

        _metricsBusy = true;
        try
        {
            var config = _services.Config;
            var highlighted = _services.Registry.HighlightedServerName;

            // 先做一遍廉价的本地状态同步，界面不会因为后面对 Query 的等待而延迟刷新
            foreach (var server in Servers.ToList())
                server.Refresh(config, highlighted);

            HighlightedServerName = highlighted;
            if (_mainPage != null) _mainPage.HighlightedServerName = highlighted;

            foreach (var server in Servers.ToList())
                await server.RefreshMetricsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"刷新服务器状态失败: {ex.Message}");
        }
        finally
        {
            _metricsBusy = false;
        }
    }

    // ------------------------------------------------------------------
    // 代理授权提问
    // ------------------------------------------------------------------

    /// <summary>
    /// 授权提问状态变化：界面据此显示/隐藏作答输入框。
    /// 提问正文由授权网关写入日志，因此这里只需要一个作答入口。
    /// </summary>
    /// <param name="pending">是否已开始等待作答。</param>
    private void OnPromptStateChanged(bool pending)
    {
        IsPromptPending = pending;
        if (!pending) PromptResponse = string.Empty;
    }

    /// <summary>
    /// 提交对授权提问的作答（y=仅本次允许、a=总是允许、n=拒绝）。
    /// </summary>
    [RelayCommand]
    private void SubmitPrompt()
    {
        if (_inputReader == null) return;

        _inputReader.SubmitResponse(PromptResponse ?? string.Empty);
        PromptResponse = string.Empty;
    }

    /// <summary>
    /// 切换到指定页面。
    /// </summary>
    /// <param name="page">目标页面。</param>
    public void NavigateTo(AppPage page)
    {
        var tab = Pages.FirstOrDefault(p => p.Page == page);
        if (tab != null) SelectedPage = tab;
    }

    /// <summary>
    /// 跳到服务器实例配置页并展开指定实例的卡片。
    /// </summary>
    /// <param name="serverName">要展开的服务器名。</param>
    public void OpenServerEditor(string serverName)
    {
        var editor = ServerEditors.FirstOrDefault(
            e => string.Equals(e.Name, serverName, StringComparison.Ordinal));

        NavigateTo(AppPage.ServerConfig);

        if (editor != null) editor.IsExpanded = true;
    }

    /// <summary>
    /// 处理退出请求：释放服务并关闭 Avalonia 应用。
    /// </summary>
    public void RequestExit()
    {
        WriteLog("GLOBAL", LogLevel.Info, "收到退出指令，正在关闭...");
        Dispose();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    /// <summary>
    /// 释放视图模型：停止定时器、解除事件订阅并释放全部服务。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_statusTimer != null)
        {
            _statusTimer.Stop();
            _statusTimer.Tick -= OnStatusTimerTick;
            _statusTimer = null;
        }

        if (_inputReader != null)
        {
            _inputReader.PromptStateChanged -= OnPromptStateChanged;
            _inputReader = null;
        }

        if (_processExitHandler != null)
        {
            AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
            _processExitHandler = null;
        }

        _services?.Dispose();
        _services = null;
    }
}

/// <summary>
/// 标题栏上的一个页面标签：标题 + 对应的页面视图模型。
/// </summary>
/// <param name="Page">页面枚举值。</param>
/// <param name="Title">标签文本。</param>
/// <param name="ViewModel">页面视图模型。</param>
public sealed record PageTab(AppPage Page, string Title, ViewModelBase ViewModel);
