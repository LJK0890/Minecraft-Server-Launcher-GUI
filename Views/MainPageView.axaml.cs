using System.Collections.Specialized;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MinecraftServerLauncher.ViewModels;

namespace MinecraftServerLauncher.Views;

/// <summary>
/// 主页面视图：只负责几件纯界面的事——
/// 控制台日志自动滚到底部、Enter 发送 / Shift+Enter 换行、
/// 日志多选后复制、双击实例块跳到它的配置卡片。
/// </summary>
public partial class MainPageView : UserControl
{
    /// <summary>当前已订阅的日志集合，切换 DataContext 时需要退订。</summary>
    private INotifyCollectionChanged? _observedLogs;

    /// <summary>
    /// 初始化视图并挂接输入框、日志滚动与日志复制。
    /// </summary>
    public MainPageView()
    {
        InitializeComponent();

        var input = this.FindControl<TextBox>("ConsoleInput");
        if (input is ConsoleTextBox consoleInput)
        {
            // Enter 的处理放在 ConsoleTextBox 内部（见该类的说明）：
            // TextBox 在 AcceptsReturn 下会先吃掉 Enter，外部挂 KeyDown 轮不到
            consoleInput.SendRequested += (_, _) =>
            {
                if (DataContext is MainPageViewModel vm && vm.SendCommand.CanExecute(null))
                    vm.SendCommand.Execute(null);
            };
        }

        var copyButton = this.FindControl<Button>("CopyLogsButton");
        if (copyButton != null)
            copyButton.Click += async (_, _) => await CopySelectedLogsAsync();

        var serverList = this.FindControl<ListBox>("ServerList");
        if (serverList != null)
            serverList.DoubleTapped += OnServerListDoubleTapped;

        // 上拉列表：选中一项后自动收起
        var targetList = this.FindControl<ListBox>("TargetList");
        if (targetList != null)
            targetList.SelectionChanged += OnTargetSelectionChanged;

        DataContextChanged += (_, _) => AttachLogAutoScroll();
    }

    /// <summary>
    /// 目标列表选中项变化：写回视图模型并收起上拉列表。
    /// 之所以不用双向绑定，是因为列表初始化时会把选中项写回 null，
    /// 导致刚展开的列表立刻被关掉。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">选择变化参数。</param>
    private void OnTargetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox list) return;
        if (list.SelectedItem is not ConsoleTarget target) return;
        if (DataContext is not MainPageViewModel vm) return;

        vm.SelectedTarget = target;
    }

    /// <summary>
    /// Esc 收起上拉目标列表。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">按键事件参数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape && DataContext is MainPageViewModel vm && vm.IsTargetListOpen)
        {
            vm.IsTargetListOpen = false;
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------------
    // 日志滚动
    // ------------------------------------------------------------------

    /// <summary>
    /// 订阅日志集合的变化，新增一行时滚动到底部。
    /// </summary>
    private void AttachLogAutoScroll()
    {
        if (_observedLogs != null)
        {
            _observedLogs.CollectionChanged -= OnLogsChanged;
            _observedLogs = null;
        }

        if (DataContext is MainPageViewModel vm)
        {
            _observedLogs = vm.Logs;
            _observedLogs.CollectionChanged += OnLogsChanged;
            ScrollToEnd();
        }
    }

    /// <summary>
    /// 日志集合变化：新行到达时滚动到底部。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">集合变化参数。</param>
    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        ScrollToEnd();
    }

    /// <summary>
    /// 把控制台列表滚动到最后一行。
    /// 用 Background 优先级投递：此时新行尚未完成布局，立即滚动会停在倒数第二行。
    /// </summary>
    private void ScrollToEnd()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var list = this.FindControl<ListBox>("ConsoleList");
            if (list == null || DataContext is not MainPageViewModel vm || vm.Logs.Count == 0) return;

            list.ScrollIntoView(vm.Logs[^1]);
        }, DispatcherPriority.Background);
    }

    // ------------------------------------------------------------------
    // 日志多选与复制
    // ------------------------------------------------------------------

    /// <summary>
    /// 把控制台中选中的日志行复制到剪贴板。
    /// </summary>
    private async Task CopySelectedLogsAsync()
    {
        var list = this.FindControl<ListBox>("ConsoleList");
        if (list == null || DataContext is not MainPageViewModel vm) return;

        var selected = list.SelectedItems;
        var builder = new StringBuilder();
        var count = 0;

        if (selected != null)
        {
            foreach (var item in selected)
            {
                if (item is not LogEntryViewModel entry) continue;
                builder.AppendLine(entry.Display);
                count++;
            }
        }

        if (count == 0)
        {
            vm.ReportCopyResult(false, "没有选中任何日志行（按住 Ctrl 或 Shift 可多选）");
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null)
        {
            vm.ReportCopyResult(false, "剪贴板不可用，复制失败");
            return;
        }

        try
        {
            await clipboard.SetTextAsync(builder.ToString());
            vm.ReportCopyResult(true, $"已复制 {count} 行日志到剪贴板");
        }
        catch (Exception ex)
        {
            vm.ReportCopyResult(false, $"复制失败: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // 双击实例块 → 打开该实例的配置卡片
    // ------------------------------------------------------------------

    /// <summary>
    /// 双击实例块：跳到服务器实例配置页并展开对应卡片。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">点击事件参数。</param>
    private void OnServerListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainPageViewModel vm) return;

        // 事件源可能落在实例块内部的某个控件上，往上找承载它的列表项
        var item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        if (item?.DataContext is not ServerItemViewModel server) return;

        vm.OpenServerConfig(server.Name);
    }
}
