using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinecraftServerLauncher.Models.Domain;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 单个 AI 实例的呈现模型：AI 配置页的每张卡片、以及控制台目标下拉框的一项。
///
/// <see cref="EnableChat"/> 与 <see cref="EnableAgent"/> 是即改即存的开关；
/// 其余字段（地址、模型、密钥、迭代上限、三段提示词）在卡片展开后编辑，
/// 点各自的“应用”才写盘。
///
/// 写入方式：直接读改写配置文件后调用 <c>Agent.ReloadConfig</c>，
/// 而不是拼 <c>$ai cfg set</c> 命令——提示词里必然有空格与换行，
/// 命令层按空格切分会破坏内容。
/// </summary>
public partial class AiConfigItemViewModel : ViewModelBase, IRefreshablePage
{
    /// <summary>应用外壳，用于写日志与刷新其它页面。</summary>
    private readonly IAppShell _shell;

    /// <summary>聊天开关的存储字段。</summary>
    private bool _enableChat;
    /// <summary>代理开关的存储字段。</summary>
    private bool _enableAgent;

    /// <summary>批量刷新时的抑制标志：避免刷新赋值被当成用户操作写回配置。</summary>
    private bool _suppressWrite;

    /// <summary>
    /// 构造 AI 实例呈现模型。
    /// </summary>
    /// <param name="name">配置名称。</param>
    /// <param name="config">对应的 AI 配置。</param>
    /// <param name="isDefault">是否为当前默认 AI 配置。</param>
    /// <param name="shell">应用外壳。</param>
    public AiConfigItemViewModel(string name, AIConfig config, bool isDefault, IAppShell shell)
    {
        _shell = shell;
        Name = name;
        _url = config.Url;
        _model = config.Model;
        _isDefault = isDefault;

        // 直接写字段：绕开生成的 setter，否则构造期间就会触发“写回配置”
        _enableChat = config.EnableChat;
        _enableAgent = config.EnableAgent;

        LoadEditorFields(config);
    }

    /// <summary>配置名称（唯一标识）。</summary>
    public string Name { get; }

    /// <summary>API 地址（卡片头上的只读展示）。</summary>
    [ObservableProperty]
    private string _url;

    /// <summary>模型名称（卡片头上的只读展示）。</summary>
    [ObservableProperty]
    private string _model;

    /// <summary>是否为当前默认 AI 配置。</summary>
    [ObservableProperty]
    private bool _isDefault;

    /// <summary>是否展开编辑区。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandGlyph))]
    private bool _isExpanded;

    /// <summary>展开指示符，供卡片头部提示点击可展开 / 收起。</summary>
    public string ExpandGlyph => IsExpanded ? "▾" : "▸";

    /// <summary>是否启用聊天功能。</summary>
    public bool EnableChat
    {
        get => _enableChat;
        set
        {
            if (!SetProperty(ref _enableChat, value)) return;
            OnPropertyChanged(nameof(CapabilityText));
            if (!_suppressWrite) WriteFlag(nameof(AIConfig.EnableChat), value);
        }
    }

    /// <summary>是否启用代理（Agent）功能。</summary>
    public bool EnableAgent
    {
        get => _enableAgent;
        set
        {
            if (!SetProperty(ref _enableAgent, value)) return;
            OnPropertyChanged(nameof(CapabilityText));
            if (!_suppressWrite) WriteFlag(nameof(AIConfig.EnableAgent), value);
        }
    }

    // ------------------------------------------------------------------
    // 编辑区字段
    // ------------------------------------------------------------------

    /// <summary>API 地址输入。</summary>
    [ObservableProperty]
    private string _urlInput = string.Empty;

    /// <summary>模型名称输入。</summary>
    [ObservableProperty]
    private string _modelInput = string.Empty;

    /// <summary>是否从环境变量读取密钥。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyFieldHint))]
    private bool _useApiKeyEnv = true;

    /// <summary>明文密钥输入（仅在未使用环境变量时有意义）。</summary>
    [ObservableProperty]
    private string _apiKeyInput = string.Empty;

    /// <summary>密钥环境变量名输入。</summary>
    [ObservableProperty]
    private string _apiKeyEnvInput = string.Empty;

    /// <summary>代理最大迭代次数输入。</summary>
    [ObservableProperty]
    private string _maxIterationsInput = string.Empty;

    /// <summary>上下文压缩阈值输入。</summary>
    [ObservableProperty]
    private string _maxContextTokensInput = string.Empty;

    /// <summary>聊天提示词输入。</summary>
    [ObservableProperty]
    private string _chatPromptInput = string.Empty;

    /// <summary>代理提示词输入。</summary>
    [ObservableProperty]
    private string _agentPromptInput = string.Empty;

    /// <summary>上下文压缩提示词输入。</summary>
    [ObservableProperty]
    private string _compressPromptInput = string.Empty;

    /// <summary>密钥输入区的说明文字。</summary>
    public string KeyFieldHint => UseApiKeyEnv
        ? "从环境变量读取密钥：填写变量名（留空表示不设置）。"
        : "直接把密钥写入配置文件（明文保存）：填写密钥本身。";

    /// <summary>
    /// 展开时把配置里的最新值填进编辑框。
    /// </summary>
    /// <param name="value">是否展开。</param>
    partial void OnIsExpandedChanged(bool value)
    {
        if (!value) return;

        var config = _shell.Services?.Config.AIConfigs.GetValueOrDefault(Name);
        if (config != null) LoadEditorFields(config);
    }

    /// <summary>把配置中的可编辑字段填进输入框。</summary>
    /// <param name="config">最新的 AI 配置。</param>
    private void LoadEditorFields(AIConfig config)
    {
        UrlInput = config.Url;
        ModelInput = config.Model;
        UseApiKeyEnv = config.UseApiKeyEnv;
        ApiKeyInput = config.UseApiKeyEnv ? string.Empty : config.ApiKey;
        ApiKeyEnvInput = config.ApiKeyEnv ?? string.Empty;
        MaxIterationsInput = config.MaxIterations.ToString(CultureInfo.InvariantCulture);
        MaxContextTokensInput = config.MaxContextTokens.ToString(CultureInfo.InvariantCulture);
        ChatPromptInput = config.ChatPrompt;
        AgentPromptInput = config.AgentPrompt;
        CompressPromptInput = config.ContextCompressPrompt;
    }

    /// <summary>
    /// 用最新的配置刷新卡片。
    /// 展开状态下不动编辑框，避免把正在输入的内容覆盖掉。
    /// </summary>
    /// <param name="config">最新的 AI 配置。</param>
    /// <param name="isDefault">是否为当前默认配置。</param>
    public void Update(AIConfig config, bool isDefault)
    {
        _suppressWrite = true;
        try
        {
            EnableChat = config.EnableChat;
            EnableAgent = config.EnableAgent;
            IsDefault = isDefault;
            Url = config.Url;
            Model = config.Model;

            if (!IsExpanded) LoadEditorFields(config);
        }
        finally
        {
            _suppressWrite = false;
        }
    }

    /// <summary>配置变化后的兜底刷新（外壳统一调用）。</summary>
    public void RefreshFromConfig()
    {
        var config = _shell.Services?.Config.AIConfigs.GetValueOrDefault(Name);
        if (config != null) Update(config, IsDefault);
    }

    // ------------------------------------------------------------------
    // 写回配置
    // ------------------------------------------------------------------

    /// <summary>
    /// 读改写配置文件并热更新到运行中的 AI 服务。
    /// </summary>
    /// <param name="what">本次修改的说明，用于日志。</param>
    /// <param name="mutate">对配置对象的修改动作。</param>
    private void MutateAndSave(string what, Action<AIConfig> mutate)
    {
        var services = _shell.Services;
        if (services == null)
        {
            Log(LogLevel.Error, "服务尚未初始化完成，无法保存 AI 配置");
            return;
        }

        try
        {
            var config = services.ConfigStore.LoadConfig();
            if (!config.AIConfigs.TryGetValue(Name, out var ai) || ai == null)
            {
                Log(LogLevel.Error, $"AI 配置 '{Name}' 不存在，可能已被删除");
                return;
            }

            mutate(ai);
            services.ConfigStore.SaveConfig(config);
            services.Agent.ReloadConfig(config);
            _shell.ReloadConfigAndRefresh();
            Log(LogLevel.Success, $"{what} 已保存");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"{what} 保存失败: {ex.Message}");
        }
    }

    /// <summary>写回能力开关。</summary>
    /// <param name="property">配置属性名（EnableChat / EnableAgent）。</param>
    /// <param name="value">新的值。</param>
    private void WriteFlag(string property, bool value)
    {
        var label = property == nameof(AIConfig.EnableChat) ? "对话" : "Agent";
        MutateAndSave($"{label} 开关", ai =>
        {
            if (property == nameof(AIConfig.EnableChat)) ai.EnableChat = value;
            else ai.EnableAgent = value;
        });
    }

    /// <summary>应用地址与模型。</summary>
    [RelayCommand]
    private void ApplyBasics()
    {
        var url = UrlInput?.Trim() ?? string.Empty;
        var model = ModelInput?.Trim() ?? string.Empty;

        if (url.Length == 0 || model.Length == 0)
        {
            Log(LogLevel.Error, "API 地址与模型名称都不能为空");
            return;
        }

        MutateAndSave("API 地址与模型", ai =>
        {
            ai.Url = url;
            ai.Model = model;
        });
    }

    /// <summary>应用密钥设置。</summary>
    [RelayCommand]
    private void ApplyKey()
    {
        var useEnv = UseApiKeyEnv;
        var envName = ApiKeyEnvInput?.Trim() ?? string.Empty;
        var key = ApiKeyInput?.Trim() ?? string.Empty;

        if (useEnv && envName.Length == 0)
            Log(LogLevel.Warning, "选择从环境变量读取，但变量名为空，密钥将为空");
        else if (!useEnv && key.Length == 0)
            Log(LogLevel.Warning, "未填写密钥，明文密钥将为空");

        MutateAndSave("密钥设置", ai =>
        {
            ai.UseApiKeyEnv = useEnv;
            ai.ApiKeyEnv = envName;
            if (!useEnv) ai.ApiKey = key;
        });
    }

    /// <summary>应用迭代上限与上下文阈值。</summary>
    [RelayCommand]
    private void ApplyLimits()
    {
        if (!int.TryParse(MaxIterationsInput?.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var maxIterations) || maxIterations <= 0)
        {
            Log(LogLevel.Error, "最大迭代次数必须是大于 0 的整数");
            return;
        }

        if (!int.TryParse(MaxContextTokensInput?.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var maxContextTokens) || maxContextTokens < 0)
        {
            Log(LogLevel.Error, "上下文压缩阈值必须是不小于 0 的整数（0 表示关闭自动压缩）");
            return;
        }

        MutateAndSave("迭代与上下文设置", ai =>
        {
            ai.MaxIterations = maxIterations;
            ai.MaxContextTokens = maxContextTokens;
        });
    }

    /// <summary>应用三段提示词。</summary>
    [RelayCommand]
    private void ApplyPrompts()
        => MutateAndSave("提示词", ai =>
        {
            ai.ChatPrompt = ChatPromptInput ?? string.Empty;
            ai.AgentPrompt = AgentPromptInput ?? string.Empty;
            ai.ContextCompressPrompt = CompressPromptInput ?? string.Empty;
        });

    /// <summary>清空三段提示词的自定义内容，恢复内置默认。</summary>
    [RelayCommand]
    private void ResetPrompts()
        => MutateAndSave("提示词（恢复内置默认）", ai =>
        {
            var defaults = new AIConfig();
            ai.ChatPrompt = defaults.ChatPrompt;
            ai.AgentPrompt = defaults.AgentPrompt;
            ai.ContextCompressPrompt = string.Empty;
        });

    /// <summary>把该配置设为默认 AI 配置。</summary>
    [RelayCommand]
    private async Task SetDefaultAsync()
    {
        await _shell.RunCommandAsync($"$ai default {Name}");
    }

    /// <summary>删除该 AI 配置。</summary>
    [RelayCommand]
    private async Task RemoveAsync()
    {
        await _shell.RunCommandAsync($"$ai rm {Name}");
    }

    /// <summary>
    /// 该实例当前可用的对话能力描述，供界面提示“为什么某项被锁定”。
    /// </summary>
    public string CapabilityText
    {
        get
        {
            var chat = EnableChat ? "对话可用" : "对话已禁用";
            var agent = EnableAgent ? "Agent 可用" : "Agent 已禁用";
            return $"{chat} · {agent}";
        }
    }

    /// <summary>写一条带 AI 配置名前缀的日志。</summary>
    /// <param name="level">日志级别。</param>
    /// <param name="message">日志正文。</param>
    private void Log(LogLevel level, string message)
        => _shell.WriteLog($"AI:{Name}", level, message);
}
