using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// “AI 实例配置”页：管理 AI 实例的连接信息与能力开关（对话 / Agent）。
/// 新增与删除走命令层（<c>$ai add</c> / <c>$ai rm</c>），
/// 两个能力开关则由 <see cref="AiConfigItemViewModel"/> 经 <c>$ai cfg set</c> 写回。
/// </summary>
public partial class AiConfigPageViewModel : ViewModelBase, IRefreshablePage
{
    /// <summary>应用外壳。</summary>
    private readonly IAppShell _shell;

    /// <summary>
    /// 构造 AI 实例配置页。
    /// </summary>
    /// <param name="shell">应用外壳。</param>
    public AiConfigPageViewModel(IAppShell shell)
    {
        _shell = shell;
        AiConfigs = shell.AiConfigs;
    }

    /// <summary>AI 实例集合（与控制台下拉框共享）。</summary>
    public ObservableCollection<AiConfigItemViewModel> AiConfigs { get; }

    /// <summary>新增实例的名称。</summary>
    [ObservableProperty]
    private string _newAiName = string.Empty;

    /// <summary>新增实例的 API 地址。</summary>
    [ObservableProperty]
    private string _newAiUrl = string.Empty;

    /// <summary>新增实例的模型名称。</summary>
    [ObservableProperty]
    private string _newAiModel = string.Empty;

    /// <summary>新增实例的密钥环境变量名（可留空，稍后用配置页或命令填写密钥）。</summary>
    [ObservableProperty]
    private string _newAiApiKeyEnv = string.Empty;

    /// <summary>当前默认 AI 实例的名称，供提示文本使用。</summary>
    [ObservableProperty]
    private string _defaultAiName = string.Empty;

    /// <summary>新增 AI 实例。</summary>
    [RelayCommand]
    private async Task AddAiAsync()
    {
        var name = NewAiName?.Trim() ?? string.Empty;
        var url = NewAiUrl?.Trim() ?? string.Empty;
        var model = NewAiModel?.Trim() ?? string.Empty;
        var env = NewAiApiKeyEnv?.Trim() ?? string.Empty;

        if (name.Length == 0 || url.Length == 0 || model.Length == 0)
        {
            _shell.WriteLog("AI", Models.Domain.LogLevel.Error,
                "请先填写配置名称、API 地址与模型名称");
            return;
        }

        var line = env.Length > 0
            ? $"$ai add {name} {url} {model} {env}"
            : $"$ai add {name} {url} {model}";

        await _shell.RunCommandAsync(line);

        NewAiName = string.Empty;
        NewAiUrl = string.Empty;
        NewAiModel = string.Empty;
        NewAiApiKeyEnv = string.Empty;
    }

    /// <summary>重新从磁盘加载配置并刷新列表。</summary>
    [RelayCommand]
    private void Reload() => _shell.ReloadConfigAndRefresh();

    /// <summary>展开全部卡片。</summary>
    [RelayCommand]
    private void ExpandAllCards()
    {
        foreach (var ai in AiConfigs) ai.IsExpanded = true;
    }

    /// <summary>收起全部卡片。</summary>
    [RelayCommand]
    private void CollapseAllCards()
    {
        foreach (var ai in AiConfigs) ai.IsExpanded = false;
    }

    /// <summary>依据当前配置刷新页面展示状态。</summary>
    public void RefreshFromConfig()
    {
        DefaultAiName = _shell.Services?.Config.DefaultAIConfig ?? string.Empty;

        foreach (var ai in AiConfigs)
            ai.RefreshFromConfig();
    }
}
