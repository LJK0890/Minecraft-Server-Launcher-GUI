namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// AI 对话面板中的一条消息，供列表绑定。
/// </summary>
public sealed class AiChatMessageViewModel
{
    /// <summary>说话方标签（如“我”“AI”）。</summary>
    public string Speaker { get; init; } = string.Empty;
    /// <summary>消息正文。</summary>
    public string Text { get; init; } = string.Empty;
    /// <summary>是否为用户发出的消息（界面据此左右对齐或着色）。</summary>
    public bool IsUser { get; init; }
    /// <summary>记录时间。</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>时间文本（HH:mm:ss）。</summary>
    public string TimeText => Timestamp.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>整行文本，便于复制。</summary>
    public string Display => $"[{TimeText}] {Speaker}: {Text}";
}
