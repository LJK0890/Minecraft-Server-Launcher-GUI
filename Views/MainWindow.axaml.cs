using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace MinecraftServerLauncher.Views;

/// <summary>
/// 主窗口：无系统边框的自绘窗口。
/// 这里只处理窗口自身的交互（拖动、缩放、最大化/最小化/关闭），
/// 业务逻辑全部在 <see cref="ViewModels.MainWindowViewModel"/> 中。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 初始化窗口并挂接标题栏按钮事件。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        var minimizeBtn = this.FindControl<Button>("MinimizeButton");
        var maximizeBtn = this.FindControl<Button>("MaximizeButton");
        var closeBtn = this.FindControl<Button>("CloseButton");

        if (minimizeBtn != null) minimizeBtn.Click += (_, _) => WindowState = WindowState.Minimized;
        if (maximizeBtn != null) maximizeBtn.Click += (_, _) => ToggleMaximize();
        if (closeBtn != null) closeBtn.Click += (_, _) => Close();
    }

    /// <summary>
    /// 在最大化与还原之间切换。
    /// </summary>
    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>
    /// 窗口首次显示前把尺寸夹到屏幕可用区域内并居中。
    ///
    /// 高分屏（如 200% 缩放）下逻辑桌面可能只有 850×530 左右，
    /// 而默认窗口是 1200×740：不夹的话窗口会超出屏幕，
    /// 控制台的输入行和窗口按钮会跑到屏幕外面点不到。
    /// </summary>
    /// <param name="e">事件参数。</param>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ClampToWorkingArea();
    }

    /// <summary>
    /// 把窗口尺寸限制在屏幕可用区域内（并留出一点边距），然后居中。
    /// 屏幕信息不可用时保持 XAML 中声明的尺寸。
    /// </summary>
    private void ClampToWorkingArea()
    {
        try
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
            if (screen == null) return;

            // Scaling 是物理像素与逻辑像素的比值；Width/Height 用逻辑像素
            var scale = screen.Scaling <= 0 ? 1.0 : screen.Scaling;
            var area = screen.WorkingArea;
            var workWidth = area.Width / scale;
            var workHeight = area.Height / scale;

            const double margin = 16;
            var targetWidth = Math.Max(MinWidth, Math.Min(Width, workWidth - margin));
            var targetHeight = Math.Max(MinHeight, Math.Min(Height, workHeight - margin));

            // 只有确实放不下时才调整，避免每次启动都写一条无意义的日志
            if (Math.Abs(targetWidth - Width) > 0.5 || Math.Abs(targetHeight - Height) > 0.5)
            {
                if (DataContext is ViewModels.MainWindowViewModel vm)
                {
                    vm.WriteLog("Window", Models.Domain.LogLevel.Info,
                        $"屏幕可用区域 {workWidth:F0}×{workHeight:F0}，窗口初始尺寸已调整为 {targetWidth:F0}×{targetHeight:F0}");
                }
            }

            Width = targetWidth;
            Height = targetHeight;

            // 夹完之后按逻辑尺寸居中，避免 WindowStartupLocation 用旧尺寸计算
            var physicalWidth = targetWidth * scale;
            var physicalHeight = targetHeight * scale;
            Position = new PixelPoint(
                area.X + (int)Math.Max(0, (area.Width - physicalWidth) / 2),
                area.Y + (int)Math.Max(0, (area.Height - physicalHeight) / 2));
        }
        catch (Exception ex)
        {
            // 屏幕信息获取失败不应影响窗口显示
            System.Diagnostics.Debug.WriteLine($"调整窗口初始尺寸失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 拖动标题栏移动窗口；双击切换最大化。
    /// 标题栏上的按钮与页面标签会先处理点击事件，因此不会误触发拖动。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">指针事件参数。</param>
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 事件源自按钮或页面标签时交给它们自己处理
        if (e.Source is Visual visual &&
            (visual.FindAncestorOfType<Button>(includeSelf: true) != null ||
             visual.FindAncestorOfType<ListBoxItem>(includeSelf: true) != null))
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        BeginMoveDrag(e);
    }

    /// <summary>
    /// 拖动窗口边缘调整大小。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">指针事件参数。</param>
    private void Grip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;

        WindowEdge? edge = control.Name switch
        {
            "GripTop" => WindowEdge.North,
            "GripBottom" => WindowEdge.South,
            "GripLeft" => WindowEdge.West,
            "GripRight" => WindowEdge.East,
            "GripTopLeft" => WindowEdge.NorthWest,
            "GripTopRight" => WindowEdge.NorthEast,
            "GripBottomLeft" => WindowEdge.SouthWest,
            "GripBottomRight" => WindowEdge.SouthEast,
            _ => null
        };

        if (edge.HasValue)
            BeginResizeDrag(edge.Value, e);
    }
}
