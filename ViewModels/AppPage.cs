namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 标题栏中间的页面选择项。顺序即标题栏上的显示顺序。
/// </summary>
public enum AppPage
{
    /// <summary>主页面：服务器实例块 + 控制台。</summary>
    Main,
    /// <summary>服务器实例配置。</summary>
    ServerConfig,
    /// <summary>AI 实例配置。</summary>
    AiConfig,
    /// <summary>APP 设置。</summary>
    AppSettings,
    /// <summary>关于。</summary>
    About
}
