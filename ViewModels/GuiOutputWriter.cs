using MinecraftServerLauncher.Models.Domain;
using MinecraftServerLauncher.Models.Ports;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 界面输出适配器：把领域层的 <see cref="IOutputWriter"/> 端口接到界面日志集合上。
/// 移植后的 ServerManager / 代理服务 / 命令都通过该端口输出，因此界面日志与
/// MSL-CLI 的控制台输出内容一致。
/// </summary>
public sealed class GuiOutputWriter : IOutputWriter
{
    /// <summary>日志汇聚点。</summary>
    private readonly LogSink _sink;

    /// <summary>
    /// 初始化输出适配器。
    /// </summary>
    /// <param name="sink">日志汇聚点。</param>
    public GuiOutputWriter(LogSink sink) => _sink = sink;

    /// <summary>
    /// 写入一条日志。
    /// </summary>
    /// <param name="context">日志上下文。</param>
    /// <param name="level">日志级别。</param>
    /// <param name="message">日志正文。</param>
    /// <param name="includeTimestamp">是否记录时间戳。</param>
    public void Write(string context, LogLevel level, string message, bool includeTimestamp = true)
        => _sink.Add(context, level, message, includeTimestamp);
}
