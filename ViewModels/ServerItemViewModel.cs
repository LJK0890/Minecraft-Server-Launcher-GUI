using System.Diagnostics;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinecraftServerLauncher.Models.Domain;
using MinecraftServerLauncher.Models.Ports;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 单个服务器实例块的呈现模型，对应界面左上角的“服务器实例块”（三行表格）：
/// <list type="bullet">
/// <item>第一行：图标 / 名称 / 启停；</item>
/// <item>第二行：端口 / 玩家 / 显隐；</item>
/// <item>第三行：CPU 占用 / 内存占用 / 锁定。</item>
/// </list>
///
/// 数据来源分工：
/// <list type="bullet">
/// <item>玩家数量：Query 协议（<see cref="RefreshMetricsAsync"/>）；</item>
/// <item>CPU 与内存：服务器进程状态（同一个方法）；</item>
/// <item>未运行时三项一律显示 <c>/</c>。</item>
/// </list>
/// </summary>
public partial class ServerItemViewModel : ViewModelBase
{
    /// <summary>被包装的服务器实例。</summary>
    private readonly IServer _server;
    /// <summary>服务器注册表，用于高亮切换。</summary>
    private readonly IServerRegistry _registry;
    /// <summary>应用外壳，用于执行 <c>$server ...</c> 命令（复用命令层的锁定校验与日志）。</summary>
    private readonly IAppShell _shell;

    /// <summary>上一次采样到的进程累计 CPU 时间，用于两次采样之间求差值。</summary>
    private TimeSpan _lastCpuTime;
    /// <summary>上一次采样时刻（UTC）；为 default 表示还没有基线样本。</summary>
    private DateTime _lastCpuSampleAt;
    /// <summary>图标文件的最后写入时间，用于判断是否需要重新加载。</summary>
    private DateTime _iconStamp = DateTime.MinValue;
    /// <summary>是否已经尝试加载过图标（避免每次都打磁盘）。</summary>
    private bool _iconProbed;

    /// <summary>
    /// 构造服务器呈现模型。
    /// </summary>
    /// <param name="server">服务器实例。</param>
    /// <param name="registry">服务器注册表。</param>
    /// <param name="shell">应用外壳，用于执行命令。</param>
    public ServerItemViewModel(IServer server, IServerRegistry registry, IAppShell shell)
    {
        _server = server;
        _registry = registry;
        _shell = shell;
        _status = server.Status;
        _portText = ReadPort();
    }

    /// <summary>服务器名称（唯一标识）。</summary>
    public string Name => _server.Name;

    /// <summary>服务器目录路径。</summary>
    public string Path => _server.Path;

    /// <summary>服务器实例块左侧的图标；服务器目录下没有 server-icon.png 时为 null。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    [NotifyPropertyChangedFor(nameof(HasNoIcon))]
    private Bitmap? _icon;

    /// <summary>当前运行状态。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(StartStopText))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    private ServerStatus _status;

    /// <summary>是否为当前高亮（选中）的服务器；控制台输入会转发给它。</summary>
    [ObservableProperty]
    private bool _isHighlighted;

    /// <summary>是否已锁定（禁止 run/stop/send/rm/del）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LockText))]
    private bool _isLocked;

    /// <summary>是否已在控制台隐藏输出。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HideText))]
    private bool _isHidden;

    /// <summary>端口文本（server-port）。</summary>
    [ObservableProperty]
    private string _portText;

    /// <summary>玩家数量文本，形如 <c>3/20</c>；未运行或 Query 不可用时为 <c>/</c>。</summary>
    [ObservableProperty]
    private string _playerText = NotRunningPlaceholder;

    /// <summary>CPU 占用文本，形如 <c>12.3%</c>；未运行时为 <c>/</c>。</summary>
    [ObservableProperty]
    private string _cpuText = NotRunningPlaceholder;

    /// <summary>内存占用文本，形如 <c>1024 MB</c>；未运行时为 <c>/</c>。</summary>
    [ObservableProperty]
    private string _memoryText = NotRunningPlaceholder;

    /// <summary>未运行时状态项的占位文本。</summary>
    public const string NotRunningPlaceholder = "/";

    /// <summary>已停止：中性灰。</summary>
    private static readonly IBrush StoppedBrush = new SolidColorBrush(Color.Parse("#8A8A8E"));
    /// <summary>启动中 / 停止中：琥珀色。</summary>
    private static readonly IBrush TransitionBrush = new SolidColorBrush(Color.Parse("#D08700"));
    /// <summary>运行中：绿色。</summary>
    private static readonly IBrush RunningBrush = new SolidColorBrush(Color.Parse("#2E9E5B"));

    /// <summary>是否已加载到 server-icon.png（界面据此决定显示图片还是占位图标）。</summary>
    public bool HasIcon => Icon != null;

    /// <summary>是否缺少 server-icon.png。</summary>
    public bool HasNoIcon => Icon == null;

    /// <summary>状态的中文文本，供列表显示。</summary>
    public string StatusText => Status switch
    {
        ServerStatus.Stopped => "已停止",
        ServerStatus.Starting => "启动中",
        ServerStatus.Running => "运行中",
        ServerStatus.Stopping => "停止中",
        _ => Status.ToString()
    };

    /// <summary>是否正在运行。</summary>
    public bool IsRunning => Status == ServerStatus.Running;

    /// <summary>启停按钮上的文字。</summary>
    public string StartStopText => Status switch
    {
        ServerStatus.Stopped => "启动",
        ServerStatus.Starting => "启动中",
        ServerStatus.Running => "停止",
        ServerStatus.Stopping => "停止中",
        _ => "启动"
    };

    /// <summary>锁定按钮上的文字。</summary>
    public string LockText => IsLocked ? "解锁" : "锁定";

    /// <summary>显隐按钮上的文字。</summary>
    public string HideText => IsHidden ? "显示" : "隐藏";

    /// <summary>
    /// 状态对应的着色，供实例块按运行状态变色。
    /// </summary>
    public IBrush StatusColor => Status switch
    {
        ServerStatus.Running => RunningBrush,
        ServerStatus.Starting or ServerStatus.Stopping => TransitionBrush,
        _ => StoppedBrush
    };

    /// <summary>
    /// 从领域层与配置刷新展示状态（不含 CPU / 内存 / 玩家，那些由 <see cref="RefreshMetricsAsync"/> 更新）。
    /// </summary>
    /// <param name="config">当前应用配置，提供锁定/隐藏名单。</param>
    /// <param name="highlightedName">当前高亮的服务器名。</param>
    public void Refresh(AppConfig config, string? highlightedName)
    {
        Status = _server.Status;
        IsLocked = config.LockedServers.Any(n => string.Equals(n, Name, StringComparison.OrdinalIgnoreCase));
        IsHidden = config.HiddenServers.Any(n => string.Equals(n, Name, StringComparison.OrdinalIgnoreCase));
        IsHighlighted = string.Equals(highlightedName, Name, StringComparison.OrdinalIgnoreCase);
        PortText = ReadPort();
        RefreshIcon();
    }

    /// <summary>
    /// 读取 server.properties 中的端口。
    /// </summary>
    /// <returns>端口文本；读取失败时回退到 25565。</returns>
    private string ReadPort()
    {
        try
        {
            return _server.Properties.GetValue("server-port", "25565");
        }
        catch
        {
            return "25565";
        }
    }

    /// <summary>
    /// 按需加载服务器图标（服务器目录下的 server-icon.png）。
    /// 文件不存在或读取失败时保持 null，界面回退到内置占位图标。
    /// </summary>
    private void RefreshIcon()
    {
        try
        {
            var iconPath = System.IO.Path.Combine(Path, "server-icon.png");
            if (!File.Exists(iconPath))
            {
                if (Icon != null) Icon = null;
                _iconStamp = DateTime.MinValue;
                _iconProbed = true;
                return;
            }

            var stamp = File.GetLastWriteTimeUtc(iconPath);
            if (_iconProbed && stamp == _iconStamp) return;

            // 先读进内存再解码：Bitmap 直接持有文件流会锁住文件，
            // 服务器更换图标时会因“文件被占用”而失败
            var bytes = File.ReadAllBytes(iconPath);
            using var stream = new MemoryStream(bytes);
            Icon = new Bitmap(stream);
            _iconStamp = stamp;
            _iconProbed = true;
        }
        catch (Exception ex)
        {
            // 图标只是装饰，任何失败都不应影响实例块
            System.Diagnostics.Debug.WriteLine($"加载服务器 '{Name}' 的图标失败: {ex.Message}");
            _iconProbed = true;
        }
    }

    /// <summary>
    /// 刷新玩家数量（Query）与 CPU / 内存（进程状态）。
    /// 未运行时三项显示 <c>/</c>，并清空 CPU 采样基线，
    /// 避免下次启动时把停机期间的时间差算进 CPU 占用。
    /// </summary>
    public async Task RefreshMetricsAsync()
    {
        if (Status != ServerStatus.Running)
        {
            PlayerText = NotRunningPlaceholder;
            CpuText = NotRunningPlaceholder;
            MemoryText = NotRunningPlaceholder;
            _lastCpuSampleAt = default;
            _lastCpuTime = TimeSpan.Zero;
            return;
        }

        RefreshProcessMetrics();
        await RefreshPlayerCountAsync();
    }

    /// <summary>
    /// 从服务器进程读取内存与 CPU 占用。
    /// </summary>
    private void RefreshProcessMetrics()
    {
        try
        {
            if (_server.GetProcess() is not Process process)
            {
                MemoryText = NotRunningPlaceholder;
                CpuText = NotRunningPlaceholder;
                return;
            }

            process.Refresh();
            if (process.HasExited)
            {
                MemoryText = NotRunningPlaceholder;
                CpuText = NotRunningPlaceholder;
                return;
            }

            MemoryText = $"{process.WorkingSet64 / (1024 * 1024)} MB";

            // CPU 占用 = 两次采样之间进程 CPU 时间的增量 / 墙钟增量 / 逻辑核心数。
            // 除以核心数后，数值含义是“占整机 CPU 的百分比”，与任务管理器一致。
            var now = DateTime.UtcNow;
            var cpuTime = process.TotalProcessorTime;
            if (_lastCpuSampleAt == default)
            {
                // 第一次采样没有基线，无法算出速率，先显示占位符
                _lastCpuSampleAt = now;
                _lastCpuTime = cpuTime;
                CpuText = NotRunningPlaceholder;
                return;
            }

            var wallDelta = (now - _lastCpuSampleAt).TotalMilliseconds;
            var cpuDelta = (cpuTime - _lastCpuTime).TotalMilliseconds;
            _lastCpuSampleAt = now;
            _lastCpuTime = cpuTime;

            if (wallDelta <= 0)
            {
                CpuText = NotRunningPlaceholder;
                return;
            }

            var cores = Environment.ProcessorCount;
            var percent = cpuDelta / wallDelta / cores * 100.0;
            if (percent < 0) percent = 0;
            CpuText = percent.ToString("F1", CultureInfo.InvariantCulture) + "%";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"读取服务器 '{Name}' 的进程状态失败: {ex.Message}");
            MemoryText = NotRunningPlaceholder;
            CpuText = NotRunningPlaceholder;
        }
    }

    /// <summary>
    /// 通过 Query 协议读取在线玩家数量，结果格式化为 <c>在线/上限</c>。
    /// 使用静默模式：服务器未启用 Query 时不写日志，否则每次刷新都会刷屏。
    /// </summary>
    private async Task RefreshPlayerCountAsync()
    {
        try
        {
            var info = await _server.GetQueryInfoAsync(quiet: true);
            if (info == null ||
                !info.TryGetValue("numplayers", out var online) ||
                !info.TryGetValue("maxplayers", out var max))
            {
                PlayerText = NotRunningPlaceholder;
                return;
            }

            PlayerText = $"{online}/{max}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"查询服务器 '{Name}' 的玩家数量失败: {ex.Message}");
            PlayerText = NotRunningPlaceholder;
        }
    }

    /// <summary>
    /// 把该服务器设为高亮（控制台输入将转发给它）。
    /// </summary>
    [RelayCommand]
    private void Highlight()
    {
        if (_registry.SwitchHighlight(Name))
        {
            IsHighlighted = true;
            _shell.ReloadConfigAndRefresh();
        }
    }

    /// <summary>
    /// 切换启动 / 停止。走命令层是为了复用“已锁定禁止启停”的校验与统一日志。
    /// </summary>
    [RelayCommand]
    private async Task ToggleStartStopAsync()
    {
        var verb = Status == ServerStatus.Stopped ? "run" : "stop";
        await _shell.RunCommandAsync($"$server {verb} {Name}");
    }

    /// <summary>切换锁定状态（锁定后禁止 run/stop/send/rm/del）。</summary>
    [RelayCommand]
    private async Task ToggleLockAsync()
    {
        var verb = IsLocked ? "ulk" : "lk";
        await _shell.RunCommandAsync($"$server {verb} {Name}");
    }

    /// <summary>切换控制台输出显隐。</summary>
    [RelayCommand]
    private async Task ToggleHideAsync()
    {
        var verb = IsHidden ? "uhd" : "hd";
        await _shell.RunCommandAsync($"$server {verb} {Name}");
    }

    /// <summary>
    /// 从配置中移除该服务器实例（仅解除登记，不删除磁盘上的服务器目录）。
    /// </summary>
    [RelayCommand]
    private async Task RemoveAsync()
    {
        await _shell.RunCommandAsync($"$server rm {Name}");
    }

    /// <summary>
    /// 向该服务器发送一条 Minecraft 命令。
    /// </summary>
    /// <param name="command">命令文本（不含前导斜杠）。</param>
    /// <returns>发送任务。</returns>
    public async Task SendAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        try
        {
            await _server.SendCommandAsync(command);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"向服务器 '{Name}' 发送命令失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 查询服务器状态信息（版本、在线人数等）。
    /// </summary>
    /// <returns>查询结果；查询失败时返回 null。</returns>
    public Task<Dictionary<string, string>?> QueryAsync() => _server.GetQueryInfoAsync();

    /// <summary>
    /// 读取输出缓冲区内容（不清空）。
    /// </summary>
    /// <returns>缓冲区文本。</returns>
    public string GetBufferContent() => _server.GetBufferContent();

    /// <summary>
    /// 底层服务器实例，供需要访问领域端口的页面使用。
    /// </summary>
    public IServer Server => _server;
}
