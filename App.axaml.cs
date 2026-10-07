using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using MinecraftServerLauncher.ViewModels;
using MinecraftServerLauncher.Views;
using System.Linq;

namespace MinecraftServerLauncher
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(ParseInitialPage(desktop.Args)),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// 解析命令行参数 <c>--page &lt;页面名&gt;</c>，得到启动时要打开的页面。
        /// 页面名使用 <see cref="ViewModels.AppPage"/> 的枚举名（不区分大小写），
        /// 例如 <c>--page ServerConfig</c>；无法识别或未提供时返回 null（主页面）。
        /// </summary>
        /// <param name="args">命令行参数。</param>
        /// <returns>启动页；未指定时返回 null。</returns>
        private static ViewModels.AppPage? ParseInitialPage(string[]? args)
        {
            if (args == null) return null;

            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "--page", StringComparison.OrdinalIgnoreCase)) continue;
                if (i + 1 >= args.Length) return null;

                return Enum.TryParse<ViewModels.AppPage>(args[i + 1], ignoreCase: true, out var page)
                    ? page
                    : null;
            }

            return null;
        }
    }
}