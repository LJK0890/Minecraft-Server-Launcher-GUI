using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinecraftServerLauncher.Models.Domain;
using MinecraftServerLauncher.Models.Ports;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 单个服务器实例在“服务器实例配置”页里的编辑卡片。
///
/// 直接读写领域模型而不是拼 <c>$server arg set</c> / <c>$server cfg set</c> 命令：
/// 命令层按空格切分参数，含空格的启动参数（如 <c>-Dfoo=bar baz</c>）会被拆成两项；
/// 这里按下标单个写回（<c>SetJvmArgAt</c> / <c>SetAppendArgAt</c>），值原样保留。
/// 每次修改都会写盘（<c>user_jvm_args.txt</c> / 启动脚本 / <c>server.properties</c>）并记一条日志。
/// </summary>
public partial class ServerEditorViewModel : ViewModelBase, IRefreshablePage
{
    /// <summary>应用外壳，用于写日志与刷新其它页面。</summary>
    private readonly IAppShell _shell;

    /// <summary>当前被包装的服务器实例。</summary>
    private IServer _server;

    /// <summary>未经过滤的全部 server.properties 项。</summary>
    private readonly List<PropertyItemViewModel> _allProperties = new();

    /// <summary>只换包装对象时的抑制标志：避免把用户正在编辑的输入重读覆盖。</summary>
    private bool _suppressDetailReload;

    /// <summary>
    /// 构造编辑卡片。
    /// </summary>
    /// <param name="item">实例块的呈现模型。</param>
    /// <param name="shell">应用外壳。</param>
    public ServerEditorViewModel(ServerItemViewModel item, IAppShell shell)
    {
        _shell = shell;
        _server = item.Server;
        // 直接写字段，避免构造期间触发 OnItemChanged 里的重复加载
        _item = item;

        // 详情不在这里读取：卡片默认收起，展开时才加载启动参数与 server.properties，
        // 否则启动时就要为每个实例读一遍配置文件
    }

    /// <summary>实例块的呈现模型（名称、状态与启停/锁定/隐藏/移除命令）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name))]
    [NotifyPropertyChangedFor(nameof(Path))]
    private ServerItemViewModel _item;

    /// <summary>服务器名称。</summary>
    public string Name => Item.Name;

    /// <summary>服务器目录。</summary>
    public string Path => Item.Path;

    /// <summary>是否展开编辑区（默认收起，展开时才读取启动参数与配置文件）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandGlyph))]
    private bool _isExpanded;

    /// <summary>展开指示符，供卡片头部提示点击可展开 / 收起。</summary>
    public string ExpandGlyph => IsExpanded ? "▾" : "▸";

    // ----- 启动参数 -----

    /// <summary>Java 可执行文件路径（javaPath，不可为空）。</summary>
    [ObservableProperty]
    private string _javaPath = string.Empty;

    /// <summary>JAR / 参数文件参数（jarArgs，不可为空）。</summary>
    [ObservableProperty]
    private string _jarArgs = string.Empty;

    /// <summary>JVM 参数列表。</summary>
    public ObservableCollection<ArgumentItemViewModel> JvmArgs { get; } = new();

    /// <summary>附加参数列表。</summary>
    public ObservableCollection<ArgumentItemViewModel> AppendArgs { get; } = new();

    /// <summary>新增 JVM 参数的输入框。</summary>
    [ObservableProperty]
    private string _newJvmArg = string.Empty;

    /// <summary>新增附加参数的输入框。</summary>
    [ObservableProperty]
    private string _newAppendArg = string.Empty;

    /// <summary>完整启动命令预览。</summary>
    [ObservableProperty]
    private string _startPreview = string.Empty;

    // ----- server.properties -----

    /// <summary>过滤后的配置项（界面上显示的内容）。</summary>
    public ObservableCollection<PropertyItemViewModel> Properties { get; } = new();

    /// <summary>过滤关键字（匹配键或值）。</summary>
    [ObservableProperty]
    private string _propertyFilter = string.Empty;

    /// <summary>新增配置项的键。</summary>
    [ObservableProperty]
    private string _newPropertyKey = string.Empty;

    /// <summary>新增配置项的值。</summary>
    [ObservableProperty]
    private string _newPropertyValue = string.Empty;

    /// <summary>“显示条数 / 总条数”提示。</summary>
    [ObservableProperty]
    private string _propertyCountText = string.Empty;

    /// <summary>server.properties 是否有内容（没有时界面给出提示）。</summary>
    [ObservableProperty]
    private bool _hasProperties;

    // ------------------------------------------------------------------
    // 加载
    // ------------------------------------------------------------------

    /// <summary>
    /// 换成新的实例块呈现模型（外壳重建集合后调用），指向同一个服务器。
    /// </summary>
    /// <param name="item">新的呈现模型。</param>
    public void Update(ServerItemViewModel item)
    {
        if (ReferenceEquals(Item, item)) return;

        if (ReferenceEquals(_server, item.Server))
        {
            // 底层还是同一个服务器实例，只是外壳重建了包装对象：
            // 只换包装，不重读详情，否则用户正在输入的内容会被一次刷新清掉
            _suppressDetailReload = true;
            try
            {
                Item = item;
            }
            finally
            {
                _suppressDetailReload = false;
            }

            return;
        }

        // 底层实例被换掉了（新增 / 移除实例导致注册表重建），旧内容已无意义
        Item = item;
    }

    /// <summary>实例块变化时跟随切换到新的 <see cref="IServer"/> 实例。</summary>
    /// <param name="value">新的呈现模型。</param>
    partial void OnItemChanged(ServerItemViewModel value)
    {
        _server = value.Server;
        if (_suppressDetailReload) return;
        if (IsExpanded) ReloadDetails();
    }

    /// <summary>展开时再读取详情，避免为几十个实例白读一遍配置文件。</summary>
    /// <param name="value">是否展开。</param>
    partial void OnIsExpandedChanged(bool value)
    {
        if (value) ReloadDetails();
    }

    /// <summary>过滤条件变化时重新筛选。</summary>
    /// <param name="value">新的过滤文本。</param>
    partial void OnPropertyFilterChanged(string value) => ApplyPropertyFilter();

    /// <summary>
    /// 重新从领域层读取启动参数与 server.properties。
    /// </summary>
    public void ReloadDetails()
    {
        try
        {
            var argument = _server.Argument;
            JavaPath = argument.JavaPath;
            JarArgs = argument.JarArgs;

            JvmArgs.Clear();
            foreach (var value in argument.JvmArgs)
                JvmArgs.Add(new ArgumentItemViewModel(value, true, SaveArgumentAsync, RemoveArgumentAsync));

            AppendArgs.Clear();
            foreach (var value in argument.AppendArgs)
                AppendArgs.Add(new ArgumentItemViewModel(value, false, SaveArgumentAsync, RemoveArgumentAsync));

            StartPreview = argument.GetStartArguments();
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"读取启动参数失败: {ex.Message}");
        }

        ReloadProperties();
    }

    /// <summary>读取 server.properties 的全部键值对。</summary>
    private void ReloadProperties()
    {
        _allProperties.Clear();
        try
        {
            foreach (var pair in _server.Properties.Entries.OrderBy(p => p.Key, StringComparer.Ordinal))
                _allProperties.Add(new PropertyItemViewModel(pair.Key, pair.Value, SavePropertyAsync, RemovePropertyAsync));
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"读取 server.properties 失败: {ex.Message}");
        }

        ApplyPropertyFilter();
    }

    /// <summary>按当前过滤条件重建可见列表。</summary>
    private void ApplyPropertyFilter()
    {
        var filter = PropertyFilter?.Trim() ?? string.Empty;

        Properties.Clear();
        foreach (var item in _allProperties)
        {
            if (filter.Length == 0 ||
                item.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (item.Value?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                Properties.Add(item);
            }
        }

        HasProperties = _allProperties.Count > 0;
        PropertyCountText = filter.Length == 0
            ? $"共 {_allProperties.Count} 项"
            : $"匹配 {Properties.Count} / {_allProperties.Count} 项";
    }

    /// <summary>重新读取按钮。</summary>
    [RelayCommand]
    private void Reload() => ReloadDetails();

    // ------------------------------------------------------------------
    // 启动参数
    // ------------------------------------------------------------------

    /// <summary>应用 Java 路径。</summary>
    [RelayCommand]
    private void ApplyJavaPath()
    {
        var value = JavaPath?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            Log(LogLevel.Error, "Java 路径不能为空");
            return;
        }

        try
        {
            _server.Argument.SetJavaPath(value);
            JavaPath = _server.Argument.JavaPath;
            UpdateStartPreview();
            Log(LogLevel.Success, $"Java 路径已设为 {JavaPath}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"设置 Java 路径失败: {ex.Message}");
        }
    }

    /// <summary>应用 jar 参数。</summary>
    [RelayCommand]
    private void ApplyJarArgs()
    {
        var value = JarArgs?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            Log(LogLevel.Error, "jar 参数不能为空");
            return;
        }

        try
        {
            _server.Argument.SetJarArgs(value);
            JarArgs = _server.Argument.JarArgs;
            UpdateStartPreview();
            Log(LogLevel.Success, $"jar 参数已设为 {JarArgs}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"设置 jar 参数失败: {ex.Message}");
        }
    }

    /// <summary>新增一条 JVM 参数。</summary>
    [RelayCommand]
    private void AddJvmArg()
    {
        var value = NewJvmArg?.Trim() ?? string.Empty;
        if (value.Length == 0) return;

        try
        {
            if (!_server.Argument.AddJvmArg(value))
            {
                Log(LogLevel.Warning, $"JVM 参数 '{value}' 已存在，未重复添加");
                return;
            }

            NewJvmArg = string.Empty;
            ReloadDetails();
            Log(LogLevel.Success, $"已添加 JVM 参数 {value}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"添加 JVM 参数失败: {ex.Message}");
        }
    }

    /// <summary>新增一条附加参数。</summary>
    [RelayCommand]
    private void AddAppendArg()
    {
        var value = NewAppendArg?.Trim() ?? string.Empty;
        if (value.Length == 0) return;

        try
        {
            if (!_server.Argument.AddAppendArg(value))
            {
                Log(LogLevel.Warning, $"附加参数 '{value}' 已存在，未重复添加");
                return;
            }

            NewAppendArg = string.Empty;
            ReloadDetails();
            Log(LogLevel.Success, $"已添加附加参数 {value}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"添加附加参数失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 按元素在列表中的位置单独写回（下标由当前集合实时定位，因此删掉别的项也不会写错）。
    /// </summary>
    /// <param name="item">要保存的参数项。</param>
    private Task SaveArgumentAsync(ArgumentItemViewModel item)
    {
        try
        {
            var list = item.IsJvm ? JvmArgs : AppendArgs;
            var index = list.IndexOf(item);
            if (index < 0) return Task.CompletedTask;

            var ok = item.IsJvm
                ? _server.Argument.SetJvmArgAt(index, item.Value)
                : _server.Argument.SetAppendArgAt(index, item.Value);

            if (!ok)
            {
                Log(LogLevel.Error, $"参数位置 {index} 已失效，请点“重新读取”后再改");
                return Task.CompletedTask;
            }

            item.MarkSaved();
            UpdateStartPreview();
            Log(LogLevel.Success, $"启动参数已更新: {item.Value}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"保存启动参数失败: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>删除一条启动参数。</summary>
    /// <param name="item">要删除的参数项。</param>
    private Task RemoveArgumentAsync(ArgumentItemViewModel item)
    {
        try
        {
            var list = item.IsJvm ? JvmArgs : AppendArgs;
            var index = list.IndexOf(item);
            if (index < 0) return Task.CompletedTask;

            var removed = item.IsJvm
                ? _server.Argument.RemoveJvmArgAt(index)
                : _server.Argument.RemoveAppendArgAt(index);

            if (removed)
            {
                ReloadDetails();
                Log(LogLevel.Success, $"已删除启动参数 {item.Value}");
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"删除启动参数失败: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>刷新完整启动命令预览。</summary>
    private void UpdateStartPreview()
    {
        try { StartPreview = _server.Argument.GetStartArguments(); }
        catch (Exception ex) { Log(LogLevel.Error, $"刷新启动命令预览失败: {ex.Message}"); }
    }

    // ------------------------------------------------------------------
    // server.properties
    // ------------------------------------------------------------------

    /// <summary>保存一条配置项。</summary>
    /// <param name="item">要保存的配置项。</param>
    private Task SavePropertyAsync(PropertyItemViewModel item)
    {
        try
        {
            _server.Properties.SetValue(item.Key, item.Value ?? string.Empty);
            item.MarkSaved();
            Log(LogLevel.Success, $"配置已更新: {item.Key}={item.Value}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"保存配置 {item.Key} 失败: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>删除一条配置项（写回 server.properties 时移除该行）。</summary>
    /// <param name="item">要删除的配置项。</param>
    private Task RemovePropertyAsync(PropertyItemViewModel item)
    {
        try
        {
            if (!_server.Properties.TryRemoveValue(item.Key))
            {
                Log(LogLevel.Warning, $"配置 {item.Key} 不存在，未删除");
                return Task.CompletedTask;
            }

            _allProperties.Remove(item);
            ApplyPropertyFilter();
            Log(LogLevel.Success, $"已删除配置 {item.Key}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"删除配置 {item.Key} 失败: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>新增一条配置项。</summary>
    [RelayCommand]
    private void AddProperty()
    {
        var key = NewPropertyKey?.Trim() ?? string.Empty;
        var value = NewPropertyValue ?? string.Empty;

        if (key.Length == 0)
        {
            Log(LogLevel.Error, "配置键不能为空");
            return;
        }

        try
        {
            _server.Properties.SetValue(key, value);

            var existing = _allProperties.FirstOrDefault(
                p => string.Equals(p.Key, key, StringComparison.Ordinal));
            if (existing != null)
            {
                existing.Value = value;
                existing.MarkSaved();
            }
            else
            {
                _allProperties.Add(new PropertyItemViewModel(key, value, SavePropertyAsync, RemovePropertyAsync));
                _allProperties.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            }

            NewPropertyKey = string.Empty;
            NewPropertyValue = string.Empty;
            ApplyPropertyFilter();
            Log(LogLevel.Success, $"已写入配置 {key}={value}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"新增配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新展示状态：外壳重建实例块集合后会调用，把卡片重新指向新的服务器实例。
    /// </summary>
    public void RefreshFromConfig()
    {
        if (Item.Server != _server)
        {
            _server = Item.Server;
            if (IsExpanded) ReloadDetails();
        }
    }

    /// <summary>写一条带服务器名前缀的日志。</summary>
    /// <param name="level">日志级别。</param>
    /// <param name="message">日志正文。</param>
    private void Log(LogLevel level, string message)
        => _shell.WriteLog($"{Name}/配置", level, message);
}
