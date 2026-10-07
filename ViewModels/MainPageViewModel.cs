using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinecraftServerLauncher.Models.Domain;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 主页面视图模型：左侧服务器实例块列表 + 右侧控制台。
///
/// 控制台输入的两行结构：
/// <list type="bullet">
/// <item>上行：可随内容变长的多行输入框（Enter 发送，Shift/Ctrl+Enter 换行）；</item>
/// <item>下行：目标下拉框（AI 实例与服务器实例合并在一个列表里，用
/// <c>AI-</c> / <c>服务器-</c> 前缀区分，向上弹出）→（AI 目标时）启用 Agent 复选框 → 发送。</item>
/// </list>
///
/// 与命令行版本保持一致的一条约定：输入以 <c>$</c> 开头时一律按命令执行，
/// 不受目标下拉框影响；否则按当前目标发送给服务器或 AI。
/// </summary>
public partial class MainPageViewModel : ViewModelBase, IRefreshablePage
{
    /// <summary>应用外壳。</summary>
    private readonly IAppShell _shell;

    /// <summary>
    /// 程序化设置选中项时的抑制标志。
    /// 外壳重建集合后会重新指定选中项，若不抑制就会把“恢复选中”
    /// 当成用户操作，进而触发高亮命令并再次触发重建（递归）。
    /// </summary>
    private bool _syncingSelection;

    /// <summary>
    /// 构造主页面视图模型。
    /// </summary>
    /// <param name="shell">应用外壳。</param>
    public MainPageViewModel(IAppShell shell)
    {
        _shell = shell;
        Servers = shell.Servers;
        AiConfigs = shell.AiConfigs;
        Logs = shell.Logs;
    }

    /// <summary>服务器实例块集合（与配置页共用同一份数据）。</summary>
    public ObservableCollection<ServerItemViewModel> Servers { get; }

    /// <summary>AI 实例集合。</summary>
    public ObservableCollection<AiConfigItemViewModel> AiConfigs { get; }

    /// <summary>控制台日志行集合。</summary>
    public ObservableCollection<LogEntryViewModel> Logs { get; }

    /// <summary>输入框内容。</summary>
    [ObservableProperty]
    private string _inputText = string.Empty;

    /// <summary>控制台目标的候选列表（AI 实例在前，服务器实例在后）。</summary>
    public ObservableCollection<ConsoleTarget> Targets { get; } = new();

    /// <summary>当前选中的控制台目标。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAiMode))]
    [NotifyPropertyChangedFor(nameof(CanToggleAgent))]
    [NotifyPropertyChangedFor(nameof(AgentLockReason))]
    private ConsoleTarget? _selectedTarget;

    /// <summary>上拉目标列表是否展开。</summary>
    [ObservableProperty]
    private bool _isTargetListOpen;

    /// <summary>
    /// 左侧实例块列表中选中的服务器；与目标下拉框保持联动。
    /// </summary>
    [ObservableProperty]
    private ServerItemViewModel? _selectedServer;

    /// <summary>当前目标是否为 AI 实例。</summary>
    public bool IsAiMode => SelectedTarget?.IsAi == true;

    /// <summary>AI 目标下是否以 Agent（可执行命令）方式发送。</summary>
    [ObservableProperty]
    private bool _useAgent;

    /// <summary>是否正在等待 AI 回复（用于禁用发送按钮）。</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>当前高亮（控制台输入转发目标）的服务器名，来自注册表。</summary>
    [ObservableProperty]
    private string? _highlightedServerName;

    /// <summary>输入是否可用（AI 忙时禁用）。</summary>
    public bool IsInputEnabled => !IsBusy;

    /// <summary>
    /// 当前选中的 AI 实例是否允许切换 Agent 开关。
    /// 该实例禁用了 Agent（只能对话）或禁用了对话（只能 Agent）时为 false。
    /// </summary>
    public bool CanToggleAgent
    {
        get
        {
            var ai = SelectedTarget?.Ai;
            if (ai == null) return false;
            return ai.EnableChat && ai.EnableAgent;
        }
    }

    /// <summary>Agent 开关被锁定时给出的说明；未锁定时为空字符串。</summary>
    public string AgentLockReason
    {
        get
        {
            var ai = SelectedTarget?.Ai;
            if (ai == null) return string.Empty;
            if (!ai.EnableAgent) return $"AI 实例 '{ai.Name}' 已禁用 Agent，只能使用对话";
            if (!ai.EnableChat) return $"AI 实例 '{ai.Name}' 已禁用对话，只能使用 Agent";
            return string.Empty;
        }
    }

    /// <summary>
    /// 切换目标时同步左侧选中项与 Agent 开关。
    /// </summary>
    /// <param name="value">新的目标。</param>
    partial void OnSelectedTargetChanged(ConsoleTarget? value)
    {
        // 选中即收起上拉列表
        IsTargetListOpen = false;

        if (_syncingSelection) return;

        _syncingSelection = true;
        try
        {
            SelectedServer = value?.Server;

            var ai = value?.Ai;
            if (ai == null)
                UseAgent = false;
            else if (!ai.EnableAgent)
                UseAgent = false;      // 只允许对话
            else if (!ai.EnableChat)
                UseAgent = true;       // 只允许 Agent
        }
        finally
        {
            _syncingSelection = false;
        }

        // 选中服务器目标时顺带把它设为高亮服务器（命令层的“当前服务器”）
        if (value?.Server is { IsHighlighted: false } server)
            server.HighlightCommand.Execute(null);
    }

    /// <summary>
    /// 在左侧点选实例块时，把控制台目标切到对应的服务器。
    /// </summary>
    /// <param name="value">新选中的服务器。</param>
    partial void OnSelectedServerChanged(ServerItemViewModel? value)
    {
        if (_syncingSelection || value == null) return;

        _syncingSelection = true;
        try
        {
            SelectedTarget = Targets.FirstOrDefault(
                t => !t.IsAi && string.Equals(t.Name, value.Name, StringComparison.Ordinal));
        }
        finally
        {
            _syncingSelection = false;
        }

        if (!value.IsHighlighted) value.HighlightCommand.Execute(null);
    }

    /// <summary>
    /// 重建目标列表并尽量保留当前选择（按“是否 AI + 名称”匹配）。
    /// </summary>
    public void RefreshFromConfig()
    {
        HighlightedServerName = _shell.Services?.Registry.HighlightedServerName;

        var previousIsAi = SelectedTarget?.IsAi;
        var previousName = SelectedTarget?.Name;

        _syncingSelection = true;
        try
        {
            Targets.Clear();
            foreach (var ai in AiConfigs) Targets.Add(ConsoleTarget.ForAi(ai));
            foreach (var server in Servers) Targets.Add(ConsoleTarget.ForServer(server));

            SelectedTarget =
                Targets.FirstOrDefault(t => t.IsAi == previousIsAi &&
                                            string.Equals(t.Name, previousName, StringComparison.Ordinal))
                ?? Targets.FirstOrDefault(t => !t.IsAi && t.Server?.IsHighlighted == true)
                ?? Targets.FirstOrDefault(t => !t.IsAi)
                ?? Targets.FirstOrDefault();

            SelectedServer = SelectedTarget?.Server;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>
    /// 双击实例块时打开该实例的配置卡片。
    /// </summary>
    /// <param name="serverName">要编辑的服务器名。</param>
    public void OpenServerConfig(string serverName) => _shell.OpenServerEditor(serverName);

    /// <summary>
    /// 展开 / 收起上拉目标列表。
    /// </summary>
    [RelayCommand]
    private void ToggleTargetList() => IsTargetListOpen = !IsTargetListOpen;

    /// <summary>
    /// 发送输入框内容。
    /// </summary>
    [RelayCommand]
    private async Task SendAsync()
    {
        var text = InputText?.Trim();
        if (string.IsNullOrEmpty(text) || IsBusy) return;

        InputText = string.Empty;

        // 与命令行版本一致：$ 开头始终是命令，不受目标下拉框影响
        if (text.StartsWith('$'))
        {
            _shell.WriteLog("Console", LogLevel.Info, text);
            await _shell.RunCommandAsync(text);
            return;
        }

        if (IsAiMode)
            await SendToAiAsync(text);
        else
            await SendToServerAsync(text);
    }

    /// <summary>
    /// 把文本发送给当前选中的服务器控制台。
    /// </summary>
    /// <param name="text">要发送的文本。</param>
    private async Task SendToServerAsync(string text)
    {
        var target = SelectedTarget?.Server;
        if (target == null)
        {
            _shell.WriteLog("Console", LogLevel.Warning,
                "没有选中任何服务器实例（下拉框请选择“服务器-xxx”）");
            return;
        }

        if (!target.IsHighlighted)
        {
            var name = target.Name;
            target.HighlightCommand.Execute(null);

            // 高亮会触发配置重载与集合重建，因此按名称重新取一次当前实例块，
            // 避免继续用已被替换掉的旧包装对象
            target = Servers.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal)) ?? target;
        }

        _shell.WriteLog($"{target.Name} ← 控制台", LogLevel.Info, text);
        await target.SendAsync(text);
    }

    /// <summary>
    /// 把文本发送给当前选中的 AI 实例（对话或 Agent）。
    /// </summary>
    /// <param name="text">要发送的文本。</param>
    private async Task SendToAiAsync(string text)
    {
        var services = _shell.Services;
        var config = SelectedTarget?.Ai;

        if (services == null)
        {
            _shell.WriteLog("AI", LogLevel.Error, "服务尚未初始化完成，无法调用 AI");
            return;
        }

        if (config == null)
        {
            _shell.WriteLog("AI", LogLevel.Error, "没有可用的 AI 配置，请先在“AI 实例配置”页新增");
            return;
        }

        // 能力开关的最后一道闸：界面上开关被锁定时，这里再挡一次
        if (UseAgent && !config.EnableAgent)
        {
            _shell.WriteLog("AI", LogLevel.Error, $"AI 实例 '{config.Name}' 已禁用 Agent");
            return;
        }
        if (!UseAgent && !config.EnableChat)
        {
            _shell.WriteLog("AI", LogLevel.Error, $"AI 实例 '{config.Name}' 已禁用对话");
            return;
        }

        _shell.WriteLog(UseAgent ? $"我 → Agent[{config.Name}]" : $"我 → 对话[{config.Name}]",
            LogLevel.Info, text);

        IsBusy = true;
        try
        {
            var (model, response) = UseAgent
                ? await services.Agent.AgentAsync(config.Name, text)
                : await services.Agent.ChatAsync(config.Name, text);

            _shell.WriteLog(UseAgent ? $"Agent({model})" : $"AI({model})", LogLevel.Success, response);
        }
        catch (Exception ex)
        {
            _shell.WriteLog("AI", LogLevel.Error, $"AI 调用失败: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }

        // 智能体可能改写了配置（如新增/删除服务器、改动 AI 开关），刷新一次界面状态
        _shell.ReloadConfigAndRefresh();
    }

    /// <summary>清空控制台显示（不影响已落盘的日志文件）。</summary>
    [RelayCommand]
    private void ClearLogs() => _shell.Log.Clear();

    /// <summary>
    /// 记录一次日志复制的结果（由视图在复制完成后调用）。
    /// </summary>
    /// <param name="success">是否成功。</param>
    /// <param name="message">要写入日志的说明。</param>
    public void ReportCopyResult(bool success, string message)
        => _shell.WriteLog("Console", success ? LogLevel.Success : LogLevel.Warning, message);

    /// <summary>
    /// 实例块列表末尾的“+”块：跳转到服务器实例配置页填写新增表单。
    /// </summary>
    [RelayCommand]
    private void AddServerInstance() => _shell.NavigateTo(AppPage.ServerConfig);
}
