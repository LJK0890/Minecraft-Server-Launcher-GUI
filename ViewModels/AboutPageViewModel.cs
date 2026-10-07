using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using MinecraftServerLauncher.Models.Domain;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// “关于”页：显示版本、运行环境与当前数据规模。
/// </summary>
public partial class AboutPageViewModel : ViewModelBase, IRefreshablePage
{
    /// <summary>应用外壳。</summary>
    private readonly IAppShell _shell;

    /// <summary>
    /// 构造关于页。
    /// </summary>
    /// <param name="shell">应用外壳。</param>
    public AboutPageViewModel(IAppShell shell)
    {
        _shell = shell;

        var assembly = Assembly.GetExecutingAssembly();
        Version = assembly.GetName().Version?.ToString() ?? "未知";
        Runtime = $".NET {Environment.Version} / {System.Runtime.InteropServices.RuntimeInformation.OSDescription}";
        _commandCount = shell.Services?.Parser.GetCommandDescriptions().Count ?? 0;
    }

    /// <summary>应用名称。</summary>
    public string AppName => "Minecraft Server Launcher";

    /// <summary>程序集版本。</summary>
    [ObservableProperty]
    private string _version;

    /// <summary>运行时与操作系统描述。</summary>
    [ObservableProperty]
    private string _runtime;

    /// <summary>本次运行实际使用的配置文件路径。</summary>
    [ObservableProperty]
    private string _configPath = "（未初始化）";

    /// <summary>已登记的服务器数量。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataSummary))]
    private int _serverCount;

    /// <summary>已登记的 AI 实例数量。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataSummary))]
    private int _aiCount;

    /// <summary>已注册的命令数量。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataSummary))]
    private int _commandCount;

    /// <summary>数据规模的一行摘要。</summary>
    public string DataSummary =>
        $"服务器实例 {ServerCount} 个 / AI 实例 {AiCount} 个 / 已注册命令 {CommandCount} 个";

    /// <summary>
    /// 命令行的定位：本界面是 MSL-CLI 的图形前端，控制台输入框底部
    /// 与命令行版本共用同一套命令与配置文件。
    /// </summary>
    public string Description =>
        "Minecraft 服务器管理器（MSL-CLI 的图形前端）。左侧服务器实例块用于观察与控制实例，" +
        "右侧控制台与命令行版本等效：以 $ 开头的输入按命令执行，其余按当前模式发送给服务器或 AI。" +
        "控制台输入框切换到 AI 模式后可直接与 AI 对话或下发 Agent 任务。";

    /// <summary>
    /// 记录一次剪贴板操作的结果（由视图在复制完成后调用）。
    /// </summary>
    /// <param name="success">是否成功。</param>
    /// <param name="message">要写入日志的说明。</param>
    public void ReportClipboardResult(bool success, string message)
        => _shell.WriteLog("About", success ? LogLevel.Success : LogLevel.Error, message);

    /// <summary>依据当前配置刷新页面展示状态。</summary>
    public void RefreshFromConfig()
    {
        var services = _shell.Services;
        ConfigPath = services?.UserConfigPath ?? "（未初始化）";
        ServerCount = _shell.Servers.Count;
        AiCount = _shell.AiConfigs.Count;
        CommandCount = services?.Parser.GetCommandDescriptions().Count ?? 0;
    }

    /// <summary>
    /// 组装用于反馈问题的环境信息文本（剪贴板写入由视图完成，
    /// 因为剪贴板属于 TopLevel，视图模型拿不到）。
    /// </summary>
    /// <returns>多行环境信息。</returns>
    public string BuildEnvironmentText()
        => string.Join(Environment.NewLine, new[]
        {
            $"{AppName} {Version}",
            Runtime,
            $"配置文件: {ConfigPath}",
            $"服务器实例: {ServerCount} / AI 实例: {AiCount} / 已注册命令: {CommandCount}"
        });
}
