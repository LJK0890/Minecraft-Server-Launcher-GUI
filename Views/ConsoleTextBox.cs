using Avalonia.Controls;
using Avalonia.Input;

namespace MinecraftServerLauncher.Views;

/// <summary>
/// 控制台输入框。
///
/// 存在的唯一理由：Enter 要“发送”而不是“换行”。
/// <see cref="TextBox"/> 在 <c>AcceptsReturn=true</c> 时会在自己的 <c>OnKeyDown</c> 里
/// 直接把 Enter 处理掉（插入换行并标记已处理），
/// 外部再挂 KeyDown 处理器时要么轮不到、要么已经晚了。
/// 这里改为在派生类里重写 <see cref="OnKeyDown"/>：命中发送条件就拦下并且不调用基类，
/// 其余按键原样交给 <see cref="TextBox"/>。
/// </summary>
public class ConsoleTextBox : TextBox
{
    /// <summary>
    /// 复用 <see cref="TextBox"/> 的主题样式。
    ///
    /// Avalonia 的自定义控件默认按**自己的类型名**去查主题样式，
    /// 而 Fluent 主题里只有 <c>TextBox</c> 的模板，于是派生类会拿不到模板、
    /// 以 0 高度渲染——表现就是“输入框不见了”。覆写这一项即按 TextBox 取样式。
    /// </summary>
    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <summary>用户按下 Enter 请求发送时触发。</summary>
    public event EventHandler? SendRequested;

    /// <summary>
    /// Enter 触发发送；Shift+Enter 与 Ctrl+Enter 仍然换行。
    /// </summary>
    /// <param name="e">按键事件参数。</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter &&
            !e.KeyModifiers.HasFlag(KeyModifiers.Shift) &&
            !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // 不调用基类，TextBox 就不会插入换行
            e.Handled = true;
            SendRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.OnKeyDown(e);
    }
}
