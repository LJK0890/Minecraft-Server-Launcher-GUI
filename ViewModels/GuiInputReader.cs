using MinecraftServerLauncher.Models.Ports;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 界面输入适配器：实现 <see cref="IInputReader"/>，把“向操作员提问并等待作答”
/// （代理命令授权）从控制台搬到界面上。
///
/// 安全默认：界面上没有挂接提问处理者时，<see cref="ReadLineAsync"/> 立即返回空字符串，
/// 授权网关会把空作答按“拒绝”处理，因此不会出现“提问无人应答却默认放行”的情况。
/// </summary>
public sealed class GuiInputReader : IInputReader
{
    /// <summary>保护挂起提问与其状态的锁。</summary>
    private readonly Lock _gate = new();

    /// <summary>当前挂起的提问；为 null 表示没有提问在等待作答。</summary>
    private TaskCompletionSource<string>? _pending;

    /// <summary>
    /// 当读取到新的用户输入时触发。界面直接输入的内容走此事件，
    /// 语义与 MSL-CLI 的控制台输入保持一致。
    /// </summary>
    public event EventHandler<string>? OnInputReceived;

    /// <summary>
    /// 提问状态变化通知：true 表示已开始等待作答，false 表示提问已结束。
    /// 界面据此显示/隐藏作答输入框。
    /// </summary>
    public event Action<bool>? PromptStateChanged;

    /// <summary>
    /// 当前是否有提问在等待作答。
    /// </summary>
    public bool HasPendingPrompt
    {
        get { lock (_gate) return _pending != null; }
    }

    /// <summary>
    /// 开始持续读取用户输入。界面输入由视图主动投递（见 <see cref="PostInput"/>），
    /// 因此这里无需启动后台读取，保留方法以满足端口契约。
    /// </summary>
    public void StartReading()
    {
        // 无需后台读取：界面输入由视图通过 PostInput 投递
    }

    /// <summary>
    /// 停止读取用户输入；同时取消挂起的提问（按拒绝处理）。
    /// </summary>
    public void StopReading() => Complete(string.Empty);

    /// <summary>
    /// 由视图投递一行界面输入，等价于控制台读到一行。
    /// </summary>
    /// <param name="line">输入文本。</param>
    public void PostInput(string line) => OnInputReceived?.Invoke(this, line ?? string.Empty);

    /// <summary>
    /// 请求独占读取一行输入（授权提问），等待界面作答。
    /// </summary>
    /// <param name="cancellationToken">取消令牌，取消时立即按拒绝返回空字符串。</param>
    /// <param name="timeout">可选的等待超时；超时后按拒绝返回空字符串。</param>
    /// <returns>操作员输入的整行文本；无人应答、超时、取消或已有提问挂起时为空字符串。</returns>
    public Task<string> ReadLineAsync(CancellationToken cancellationToken = default, TimeSpan? timeout = null)
    {
        var handler = PromptStateChanged;
        // 界面上没有提问处理者：直接按拒绝返回，绝不放行
        if (handler == null)
            return Task.FromResult(string.Empty);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            // 同一时刻只允许一个待确认提问：重复请求直接拒绝
            if (_pending != null)
                return Task.FromResult(string.Empty);
            _pending = tcs;
        }

        // 通知界面进入“等待作答”状态
        try { handler(true); } catch { /* 界面通知失败不应影响授权流程 */ }

        // 超时按拒绝处理，避免代理任务永久挂起
        if (timeout is { } wait)
            _ = ExpireAsync(tcs, wait);

        if (cancellationToken.CanBeCanceled)
            cancellationToken.Register(() => Complete(string.Empty));

        return tcs.Task;
    }

    /// <summary>
    /// 提交操作员的作答；没有挂起提问时返回 false。
    /// </summary>
    /// <param name="response">作答文本（y / a / n 等），由授权网关解释。</param>
    /// <returns>是否成功提交给了某个挂起提问。</returns>
    public bool SubmitResponse(string response)
    {
        lock (_gate)
        {
            if (_pending == null) return false;
        }

        Complete(response ?? string.Empty);
        return true;
    }

    /// <summary>
    /// 等待超时后按拒绝结束提问。
    /// </summary>
    /// <param name="tcs">挂起提问的任务源。</param>
    /// <param name="wait">等待时长。</param>
    private async Task ExpireAsync(TaskCompletionSource<string> tcs, TimeSpan wait)
    {
        try { await Task.Delay(wait).ConfigureAwait(false); }
        catch { return; }
        Complete(string.Empty);
    }

    /// <summary>
    /// 结束挂起提问并通知界面恢复常态。
    /// </summary>
    /// <param name="result">作答文本；空字符串表示拒绝。</param>
    private void Complete(string result)
    {
        TaskCompletionSource<string>? pending;
        lock (_gate)
        {
            pending = _pending;
            _pending = null;
        }

        if (pending == null) return;

        pending.TrySetResult(result);
        try { PromptStateChanged?.Invoke(false); } catch { /* 界面通知失败不影响授权结果 */ }
    }
}
