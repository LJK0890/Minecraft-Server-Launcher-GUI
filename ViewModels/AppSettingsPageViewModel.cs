using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinecraftServerLauncher.Models.Domain;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// “APP 设置”页：图形化地读写应用级配置。
/// 所有改动都经命令层落盘（<c>$app cfg set ...</c>），与命令行版本保持一致。
/// </summary>
public partial class AppSettingsPageViewModel : ViewModelBase, IRefreshablePage
{
    /// <summary>应用外壳。</summary>
    private readonly IAppShell _shell;

    /// <summary>刷新期间的抑制标志：避免程序赋值被当成用户操作写回配置。</summary>
    private bool _suppressWrite;

    /// <summary>
    /// 构造 APP 设置页。
    /// </summary>
    /// <param name="shell">应用外壳。</param>
    public AppSettingsPageViewModel(IAppShell shell)
    {
        _shell = shell;
        AiConfigs = shell.AiConfigs;
    }

    /// <summary>可选的 AI 实例（用于选择默认 AI 配置）。</summary>
    public ObservableCollection<AiConfigItemViewModel> AiConfigs { get; }

    /// <summary>服务器实例状态（玩家/CPU/内存）刷新间隔的输入文本，单位毫秒。</summary>
    [ObservableProperty]
    private string _queryIntervalText = AppConfig.DefaultStatusQueryIntervalMs.ToString(CultureInfo.InvariantCulture);

    /// <summary>是否启用 AI 功能（对应配置项 EnableAI）。</summary>
    public bool EnableAi
    {
        get => _enableAi;
        set
        {
            if (!SetProperty(ref _enableAi, value)) return;
            if (!_suppressWrite)
                _ = _shell.RunCommandAsync($"$app cfg set EnableAI {(value ? "true" : "false")}");
        }
    }
    private bool _enableAi = true;

    /// <summary>当前的默认 AI 实例。</summary>
    public AiConfigItemViewModel? SelectedDefaultAi
    {
        get => _selectedDefaultAi;
        set
        {
            if (!SetProperty(ref _selectedDefaultAi, value)) return;
            if (!_suppressWrite && value != null)
                _ = _shell.RunCommandAsync($"$ai default {value.Name}");
        }
    }
    private AiConfigItemViewModel? _selectedDefaultAi;

    /// <summary>本次运行实际使用的配置文件路径。</summary>
    [ObservableProperty]
    private string _configPath = string.Empty;

    /// <summary>间隔设置的取值范围提示。</summary>
    public string IntervalRangeHint =>
        $"取值范围 {AppConfig.MinStatusQueryIntervalMs} ~ {AppConfig.MaxStatusQueryIntervalMs} 毫秒；" +
        $"默认 {AppConfig.DefaultStatusQueryIntervalMs} 毫秒。玩家数量经 Query 协议获取，CPU 与内存取自服务器进程。";

    /// <summary>应用刷新间隔设置。</summary>
    [RelayCommand]
    private async Task ApplyIntervalAsync()
    {
        var text = QueryIntervalText?.Trim() ?? string.Empty;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
        {
            _shell.WriteLog("Config", LogLevel.Error, $"刷新间隔 '{text}' 不是合法整数");
            return;
        }

        if (ms < AppConfig.MinStatusQueryIntervalMs || ms > AppConfig.MaxStatusQueryIntervalMs)
        {
            _shell.WriteLog("Config", LogLevel.Error,
                $"刷新间隔需在 {AppConfig.MinStatusQueryIntervalMs}~{AppConfig.MaxStatusQueryIntervalMs} 毫秒之间");
            return;
        }

        await _shell.RunCommandAsync(
            $"$app cfg set StatusQueryIntervalMs {ms.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>重新加载配置文件。</summary>
    [RelayCommand]
    private async Task ReloadConfigAsync() => await _shell.RunCommandAsync("$app reload");

    /// <summary>
    /// 在文件资源管理器中打开配置所在目录，便于手工编辑。
    /// </summary>
    [RelayCommand]
    private void OpenConfigFolder()
    {
        var path = ConfigPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var folder = System.IO.Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(folder)) return;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _shell.WriteLog("Config", LogLevel.Error, $"打开配置目录失败: {ex.Message}");
        }
    }

    /// <summary>依据当前配置刷新页面展示状态。</summary>
    public void RefreshFromConfig()
    {
        var services = _shell.Services;

        _suppressWrite = true;
        try
        {
            if (services != null)
            {
                var config = services.Config;
                EnableAi = config.EnableAI;
                QueryIntervalText = config.StatusQueryIntervalMs.ToString(CultureInfo.InvariantCulture);
                SelectedDefaultAi = AiConfigs.FirstOrDefault(
                    a => string.Equals(a.Name, config.DefaultAIConfig, StringComparison.OrdinalIgnoreCase));
                ConfigPath = services.UserConfigPath;
            }
        }
        finally
        {
            _suppressWrite = false;
        }
    }
}
