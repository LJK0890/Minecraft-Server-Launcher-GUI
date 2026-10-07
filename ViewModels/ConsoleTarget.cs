namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 控制台输入的一个目标：要么是某个服务器实例，要么是某个 AI 实例。
/// 两类目标合并在同一个下拉框里，靠 <see cref="Display"/> 的
/// <c>AI-</c> / <c>服务器-</c> 前缀区分，取代了原来的“服务器/AI”切换按钮。
/// </summary>
public sealed class ConsoleTarget
{
    /// <summary>
    /// 构造控制台目标。
    /// </summary>
    /// <param name="display">下拉框中显示的文字。</param>
    /// <param name="isAi">是否为 AI 实例。</param>
    /// <param name="name">服务器名或 AI 配置名。</param>
    /// <param name="server">服务器实例块（非服务器目标时为 null）。</param>
    /// <param name="ai">AI 实例（非 AI 目标时为 null）。</param>
    private ConsoleTarget(
        string display,
        bool isAi,
        string name,
        ServerItemViewModel? server,
        AiConfigItemViewModel? ai)
    {
        Display = display;
        IsAi = isAi;
        Name = name;
        Server = server;
        Ai = ai;
    }

    /// <summary>下拉框中显示的文字，形如 <c>AI-deepseek</c> 或 <c>服务器-survival</c>。</summary>
    public string Display { get; }

    /// <summary>是否为 AI 实例目标。</summary>
    public bool IsAi { get; }

    /// <summary>服务器名或 AI 配置名。</summary>
    public string Name { get; }

    /// <summary>服务器实例块；AI 目标时为 null。</summary>
    public ServerItemViewModel? Server { get; }

    /// <summary>AI 实例；服务器目标时为 null。</summary>
    public AiConfigItemViewModel? Ai { get; }

    /// <summary>构造服务器目标。</summary>
    /// <param name="server">服务器实例块。</param>
    /// <returns>对应的控制台目标。</returns>
    public static ConsoleTarget ForServer(ServerItemViewModel server)
        => new($"服务器-{server.Name}", false, server.Name, server, null);

    /// <summary>构造 AI 目标。</summary>
    /// <param name="ai">AI 实例。</param>
    /// <returns>对应的控制台目标。</returns>
    public static ConsoleTarget ForAi(AiConfigItemViewModel ai)
        => new($"AI-{ai.Name}", true, ai.Name, null, ai);

    /// <summary>无 ItemTemplate 时下拉框显示的文本。</summary>
    /// <returns>显示文字。</returns>
    public override string ToString() => Display;
}
