using System.Collections.ObjectModel;
using Avalonia.Threading;
using MinecraftServerLauncher.Models.Domain;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 日志汇聚点：作为界面层唯一的日志写入目标，供 <see cref="GuiOutputWriter"/> 投递日志。
///
/// 两点必须注意：
/// 1) 服务器进程输出与 AI 消费者都在后台线程上调用，写入必须编组回 UI 线程；
/// 2) 日志写入是“绝不能抛异常”的路径（它跑在进程输出事件线程上），因此所有
///    UI 访问都做兜底，Avalonia 调度器不可用时退化为直接写入。
/// </summary>
public sealed class LogSink
{
    /// <summary>界面最多保留的日志行数；超出后从最旧的一行开始丢弃，避免长时间运行耗尽内存。</summary>
    private const int MaxEntries = 5000;

    /// <summary>绑定到界面的日志行集合（始终在 UI 线程上被修改）。</summary>
    public ObservableCollection<LogEntryViewModel> Entries { get; } = new();

    /// <summary>
    /// 追加一条日志。
    /// </summary>
    /// <param name="context">日志上下文。</param>
    /// <param name="level">日志级别。</param>
    /// <param name="message">日志正文。</param>
    /// <param name="includeTimestamp">是否记录时间戳，保留该参数以与 IOutputWriter 签名一致。</param>
    public void Add(string context, LogLevel level, string message, bool includeTimestamp = true)
    {
        var entry = new LogEntryViewModel
        {
            Timestamp = DateTime.Now,
            Context = context ?? string.Empty,
            Level = level,
            Message = message ?? string.Empty
        };

        RunOnUiThread(() => Append(entry));
    }

    /// <summary>
    /// 清空全部日志行。
    /// </summary>
    public void Clear() => RunOnUiThread(Entries.Clear);

    /// <summary>
    /// 把一行加入集合，并按上限裁剪最旧的记录。
    /// </summary>
    /// <param name="entry">待加入的日志行。</param>
    private void Append(LogEntryViewModel entry)
    {
        Entries.Add(entry);
        while (Entries.Count > MaxEntries)
            Entries.RemoveAt(0);
    }

    /// <summary>
    /// 在 UI 线程上执行动作；已在 UI 线程时同步执行，否则投递到调度器。
    /// 调度器不可用（未初始化或已关闭）时直接执行，保证日志永不抛异常。
    /// </summary>
    /// <param name="action">要执行的动作。</param>
    private static void RunOnUiThread(Action action)
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
                action();
            else
                Dispatcher.UIThread.Post(action);
        }
        catch
        {
            // 无 Avalonia 调度器（如单元测试或关闭过程中）时直接执行，
            // 宁可让集合在非 UI 线程被改，也不能让日志写入抛出异常
            try { action(); } catch { /* 丢弃本条日志 */ }
        }
    }
}
