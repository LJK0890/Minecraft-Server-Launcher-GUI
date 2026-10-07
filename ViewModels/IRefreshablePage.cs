namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 页面在配置发生变化后需要重新读取展示状态的标记接口。
/// 主窗口在每次命令执行或配置重载后统一调用，避免各页面各自订阅事件。
/// </summary>
public interface IRefreshablePage
{
    /// <summary>依据当前配置刷新页面展示状态。</summary>
    void RefreshFromConfig();
}
