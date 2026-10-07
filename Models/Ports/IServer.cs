using MinecraftServerLauncher.Models.Domain;
using MinecraftServerLauncher.Models.Infrastructure;

namespace MinecraftServerLauncher.Models.Ports;

/// <summary>
/// 服务器接口，定义单个 Minecraft 服务器的状态信息、属性配置与常用操作能力。
/// </summary>
public interface IServer
{
    /// <summary>
    /// 服务器名称（唯一标识）。
    /// </summary>
    string Name { get; }
    /// <summary>
    /// 服务器所在目录路径。
    /// </summary>
    string Path { get; }
    /// <summary>
    /// 服务器当前运行状态。
    /// </summary>
    ServerStatus Status { get; }
    /// <summary>
    /// 服务器的属性配置（server.properties 文件）。
    /// </summary>
    ServerProperties Properties { get; }
    /// <summary>
    /// 服务器的启动参数配置。
    /// </summary>
    ServerArgument Argument { get; }
    /// <summary>
    /// 异步启动服务器。
    /// </summary>
    Task StartAsync();
    /// <summary>
    /// 异步停止服务器。
    /// </summary>
    /// <param name="force">是否强制停止（跳过优雅关闭流程）。</param>
    Task StopAsync(bool force = false);
    /// <summary>
    /// 异步向服务器发送一条控制台命令。
    /// </summary>
    /// <param name="command">要发送的命令文本。</param>
    Task SendCommandAsync(string command);
    /// <summary>
    /// 异步查询服务器的状态信息（如版本、在线人数等）。
    /// </summary>
    /// <param name="quiet">
    /// 为 true 时不向控制台/日志输出“Query未启用”“Query失败”一类提示。
    /// 界面上的周期刷新使用该模式：服务器未开 Query 或尚未就绪时，
    /// 每次轮询都写一条日志会把控制台刷屏。
    /// </param>
    /// <returns>查询得到的键值对信息；查询失败时返回 null。</returns>
    Task<Dictionary<string, string>?> GetQueryInfoAsync(bool quiet = false);
    /// <summary>
    /// 获取输出缓冲区内容，但不清空缓冲区。
    /// </summary>
    /// <returns>缓冲区的完整文本内容。</returns>
    string GetBufferContent();              // 获取缓冲区内容（不清空）
    /// <summary>
    /// 获取输出缓冲区内容，并清空缓冲区。
    /// </summary>
    /// <returns>清空前的缓冲区文本内容。</returns>
    string GetAndClearBufferContent();      // 获取并清空
    /// <summary>
    /// 返回底层进程对象（用于状态命令展示）。
    /// </summary>
    /// <returns>底层进程对象；服务器未运行时可能为 null。</returns>
    object? GetProcess();                   // 返回 Process 对象（用于状态命令）
}
