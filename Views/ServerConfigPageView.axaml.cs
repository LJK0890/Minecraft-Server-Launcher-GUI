using Avalonia.Controls;
using Avalonia.Input;
using MinecraftServerLauncher.ViewModels;

namespace MinecraftServerLauncher.Views;

/// <summary>
/// 服务器实例配置页视图。
/// 唯一的交互逻辑：点击卡片头部这一行展开 / 收起该实例的编辑区。
/// </summary>
public partial class ServerConfigPageView : UserControl
{
    /// <summary>初始化视图。</summary>
    public ServerConfigPageView() => InitializeComponent();

    /// <summary>
    /// 点击卡片头部：切换展开状态。
    /// 该处理器挂在卡片左侧的整块区域上，右侧按钮是它的兄弟节点，
    /// 因此点按钮不会冒泡到这里，不会连带展开 / 收起。
    /// </summary>
    /// <param name="sender">事件源（头部区域）。</param>
    /// <param name="e">点击事件参数。</param>
    private void OnCardHeaderTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.DataContext is not ServerEditorViewModel editor) return;

        editor.IsExpanded = !editor.IsExpanded;
    }
}
