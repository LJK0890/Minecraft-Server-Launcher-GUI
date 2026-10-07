using Avalonia.Media;
using MinecraftServerLauncher.Models.Domain;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 界面日志条目：把一次 <c>IOutputWriter.Write</c> 调用转换成一个可绑定、可着色的行。
/// </summary>
public sealed class LogEntryViewModel
{
    /// <summary>记录时间。</summary>
    public DateTime Timestamp { get; init; }
    /// <summary>日志上下文（服务器名、命令名等）。</summary>
    public string Context { get; init; } = string.Empty;
    /// <summary>日志级别，供界面按级别着色。</summary>
    public LogLevel Level { get; init; }
    /// <summary>日志正文。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>时间部分的短文本（HH:mm:ss）。</summary>
    public string TimeText => Timestamp.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>固定宽度的级别缩写，便于日志对齐。</summary>
    public string LevelText => Level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Info => "INF",
        LogLevel.Success => "OK ",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        LogLevel.Fatal => "FTL",
        _ => "---"
    };

    /// <summary>整行文本，供纯文本显示（如复制日志）。</summary>
    public string Display => $"[{TimeText}] [{LevelText}] [{Context}] {Message}";

    // 着色与 MSL-CLI 的 ConsoleOutputWriter.GetAnsiColor 一一对应：
    //   Debug    \x1b[90m    亮黑
    //   Info     \x1b[32m    绿
    //   Success  \x1b[92m    亮绿
    //   Warning  \x1b[33m    黄
    //   Error    \x1b[31m    红
    //   Critical \x1b[31;1m  亮红加粗
    //   Fatal    \x1b[35m    品红
    // 取色时保持了同一套色相，只是把 ANSI 的原始值调到在深色底上更易读；
    // 命令行把颜色码加在整行前面，所以界面这里也是整行着色，而不是只染前缀。
    private static readonly IBrush DebugBrush = new SolidColorBrush(Color.Parse("#7A7A7A"));
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.Parse("#3FB950"));
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#56D364"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#D29922"));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#F85149"));
    private static readonly IBrush CriticalBrush = new SolidColorBrush(Color.Parse("#FF7B72"));
    private static readonly IBrush FatalBrush = new SolidColorBrush(Color.Parse("#DB61A2"));

    /// <summary>级别对应的前景色，供控制台按级别着色（整行）。</summary>
    public IBrush LevelBrush => Level switch
    {
        LogLevel.Debug => DebugBrush,
        LogLevel.Info => InfoBrush,
        LogLevel.Success => SuccessBrush,
        LogLevel.Warning => WarningBrush,
        LogLevel.Error => ErrorBrush,
        LogLevel.Critical => CriticalBrush,
        LogLevel.Fatal => FatalBrush,
        _ => InfoBrush
    };

    /// <summary>
    /// 级别对应的字重：与命令行一致，只有 Critical 加粗（ANSI 的 <c>31;1</c>）。
    /// </summary>
    public FontWeight LevelWeight => Level == LogLevel.Critical ? FontWeight.Bold : FontWeight.Normal;
}
