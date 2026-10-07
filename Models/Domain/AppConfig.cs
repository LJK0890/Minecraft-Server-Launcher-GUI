namespace MinecraftServerLauncher.Models.Domain;

/// <summary>
/// 应用全局配置模型，保存 AI 功能开关、AI 服务配置与服务器路径映射等顶层配置。
/// </summary>
public class AppConfig
{
    /// <summary>
    /// 是否启用 AI 功能。
    /// </summary>
    public bool EnableAI { get; set; } = true;
    /// <summary>
    /// AI 服务配置字典，键为配置名称，值为对应的 AI 配置。
    /// </summary>
    public Dictionary<string, AIConfig> AIConfigs { get; set; } = new();
    /// <summary>
    /// 服务器路径映射字典，键为服务器名称，值为服务器所在目录路径。
    /// </summary>
    public Dictionary<string, string> ServerPaths { get; set; } = new();
    /// <summary>
    /// 已锁定的服务器名称列表。锁定的服务器禁止 <c>run</c>/<c>stop</c>/<c>send</c>/<c>rm</c>/<c>del</c>
    /// 五类操作，只允许 <c>$server ulk</c> 解锁；其余配置、查看类操作不受影响。
    /// </summary>
    public List<string> LockedServers { get; set; } = new();
    /// <summary>
    /// 在控制台隐藏输出的服务器名称列表（<c>$server hd</c>/<c>uhd</c>）。
    /// 隐藏后该服务器的进程输出不再写到控制台与日志，但仍会进入输出缓冲区，
    /// 因此 <c>$server buf read</c> 与 AI 代理读取日志不受影响。
    /// </summary>
    public List<string> HiddenServers { get; set; } = new();
    /// <summary>
    /// 默认使用的 AI 配置名称，未显式指定 AI 配置时生效。
    /// </summary>
    public string DefaultAIConfig { get; set; } = string.Empty;
    /// <summary>
    /// 界面上服务器实例状态（玩家数量、CPU、内存）的刷新间隔，单位毫秒，默认 2500。
    /// 玩家数量通过 Query 协议获取，CPU 与内存来自服务器进程状态。
    /// 可通过 <c>$app cfg set StatusQueryIntervalMs &lt;毫秒&gt;</c> 修改；
    /// 读盘时会被规范化到 <see cref="MinStatusQueryIntervalMs"/>~<see cref="MaxStatusQueryIntervalMs"/> 之间。
    /// </summary>
    public int StatusQueryIntervalMs { get; set; } = DefaultStatusQueryIntervalMs;

    /// <summary>状态刷新间隔的默认值（毫秒）。</summary>
    public const int DefaultStatusQueryIntervalMs = 2500;
    /// <summary>状态刷新间隔的下限（毫秒）：再快也没有意义，只会徒增 Query 流量。</summary>
    public const int MinStatusQueryIntervalMs = 250;
    /// <summary>状态刷新间隔的上限（毫秒）。</summary>
    public const int MaxStatusQueryIntervalMs = 60000;
    /// <summary>
    /// 代理命令授权配置，保存“总是允许”白名单与强制逐次询问的命令列表。
    /// </summary>
    public AgentPermissions AgentPermissions { get; set; } = new();
}
