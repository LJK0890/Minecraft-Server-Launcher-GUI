using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// “服务器实例配置”页：每个服务器实例一栏（卡片），展开后可编辑启动参数与 server.properties。
/// 新增与移除走命令层（<c>$server add</c> / <c>$server rm</c>），
/// 卡片内的参数/配置编辑直接读写领域模型，见 <see cref="ServerEditorViewModel"/>。
/// </summary>
public partial class ServerConfigPageViewModel : ViewModelBase, IRefreshablePage
{
    /// <summary>应用外壳。</summary>
    private readonly IAppShell _shell;

    /// <summary>
    /// 构造服务器实例配置页。
    /// </summary>
    /// <param name="shell">应用外壳。</param>
    public ServerConfigPageViewModel(IAppShell shell)
    {
        _shell = shell;
        Servers = shell.Servers;
        Editors = shell.ServerEditors;
    }

    /// <summary>已登记的服务器实例集合（与主页面实例块共享）。</summary>
    public ObservableCollection<ServerItemViewModel> Servers { get; }

    /// <summary>实例编辑卡片集合，每个实例一栏。</summary>
    public ObservableCollection<ServerEditorViewModel> Editors { get; }

    /// <summary>新增实例的名称输入。</summary>
    [ObservableProperty]
    private string _newServerName = string.Empty;

    /// <summary>新增实例的目录路径输入。</summary>
    [ObservableProperty]
    private string _newServerPath = string.Empty;

    /// <summary>展开全部卡片。</summary>
    [RelayCommand]
    private void ExpandAllCards()
    {
        foreach (var editor in Editors)
            editor.IsExpanded = true;
    }

    /// <summary>收起全部卡片。</summary>
    [RelayCommand]
    private void CollapseAllCards()
    {
        foreach (var editor in Editors)
            editor.IsExpanded = false;
    }

    /// <summary>新增服务器实例。</summary>
    [RelayCommand]
    private async Task AddServerAsync()
    {
        var name = NewServerName?.Trim() ?? string.Empty;
        var path = NewServerPath?.Trim() ?? string.Empty;

        if (name.Length == 0 || path.Length == 0)
        {
            _shell.WriteLog("Server", Models.Domain.LogLevel.Error,
                "请先填写服务器名称与目录路径");
            return;
        }

        await _shell.RunCommandAsync($"$server add {name} {path}");

        NewServerName = string.Empty;
        NewServerPath = string.Empty;
    }

    /// <summary>重新从磁盘加载配置并刷新列表。</summary>
    [RelayCommand]
    private void Reload() => _shell.ReloadConfigAndRefresh();

    /// <summary>依据当前配置刷新页面展示状态。</summary>
    public void RefreshFromConfig()
    {
        foreach (var editor in Editors)
            editor.RefreshFromConfig();
    }
}
