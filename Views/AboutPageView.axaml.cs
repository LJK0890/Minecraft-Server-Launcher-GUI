using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace MinecraftServerLauncher.Views;

/// <summary>
/// 关于页视图。
/// 只有“复制环境信息”需要视图参与：剪贴板挂在 TopLevel 上，视图模型取不到。
/// </summary>
public partial class AboutPageView : UserControl
{
    /// <summary>初始化视图并挂接复制按钮。</summary>
    public AboutPageView()
    {
        InitializeComponent();

        var copyButton = this.FindControl<Button>("CopyEnvironmentButton");
        if (copyButton != null)
            copyButton.Click += async (_, _) => await CopyEnvironmentAsync();
    }

    /// <summary>
    /// 把环境信息写入剪贴板。
    /// </summary>
    private async Task CopyEnvironmentAsync()
    {
        if (DataContext is not ViewModels.AboutPageViewModel vm) return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null)
        {
            vm.ReportClipboardResult(false, "剪贴板不可用");
            return;
        }

        try
        {
            await clipboard.SetTextAsync(vm.BuildEnvironmentText());
            vm.ReportClipboardResult(true, "已复制环境信息到剪贴板");
        }
        catch (Exception ex)
        {
            vm.ReportClipboardResult(false, $"复制失败: {ex.Message}");
        }
    }
}
