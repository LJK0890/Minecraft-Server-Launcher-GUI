using System.Collections.ObjectModel;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 主窗口向各页面暴露的共享外壳：页面通过它拿到服务、共享集合并执行命令。
///
/// 这样拆分的目的：服务器实例列表、AI 配置列表与日志流在“主页面”和各自的配置页
/// 之间是同一份数据，任何一处修改都会立即反映到其它页面，不需要页面之间互相引用。
/// </summary>
public interface IAppShell
{
    /// <summary>组装好的服务集合；设计时预览或初始化失败时为 null。</summary>
    AppServices? Services { get; }

    /// <summary>界面日志汇聚点。</summary>
    LogSink Log { get; }

    /// <summary>服务器实例呈现模型集合（主页面实例块与服务器配置页共用）。</summary>
    ObservableCollection<ServerItemViewModel> Servers { get; }

    /// <summary>
    /// 服务器实例编辑卡片集合（“服务器实例配置”页每个实例一栏）。
    /// 与 <see cref="Servers"/> 一一对应且按名称保持对象稳定，
    /// 这样外壳重建实例块时不会把展开状态与编辑中的输入丢掉。
    /// </summary>
    ObservableCollection<ServerEditorViewModel> ServerEditors { get; }

    /// <summary>AI 配置呈现模型集合（控制台下拉框与 AI 配置页共用）。</summary>
    ObservableCollection<AiConfigItemViewModel> AiConfigs { get; }

    /// <summary>控制台日志行集合。</summary>
    ObservableCollection<LogEntryViewModel> Logs { get; }

    /// <summary>
    /// 执行一条 <c>$</c> 命令，随后重新加载配置并刷新所有页面。
    /// 界面上的按钮（启停、锁定、隐藏、增删配置）统一走这里，
    /// 以便复用命令层已有的校验、锁定规则与日志。
    /// </summary>
    /// <param name="line">以 <c>$</c> 开头的命令文本。</param>
    Task RunCommandAsync(string line);

    /// <summary>
    /// 重新从磁盘加载配置并重建全部共享集合（不执行命令）。
    /// </summary>
    void ReloadConfigAndRefresh();

    /// <summary>写一条界面日志。</summary>
    /// <param name="context">日志上下文。</param>
    /// <param name="level">日志级别。</param>
    /// <param name="message">日志正文。</param>
    void WriteLog(string context, Models.Domain.LogLevel level, string message);

    /// <summary>
    /// 切换到指定页面（例如实例块列表末尾的“+”块跳转到服务器实例配置页）。
    /// </summary>
    /// <param name="page">目标页面。</param>
    void NavigateTo(AppPage page);

    /// <summary>
    /// 跳到服务器实例配置页，并展开指定实例的编辑卡片
    /// （主页面双击实例块时调用）。
    /// </summary>
    /// <param name="serverName">要展开的服务器名。</param>
    void OpenServerEditor(string serverName);

    /// <summary>请求退出应用。</summary>
    void RequestExit();
}
