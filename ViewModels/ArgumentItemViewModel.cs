using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// 启动参数列表中的一项（jvmArgs 或 appendArgs）。
/// 每个元素保持独立一行，可以单独编辑、保存或删除——按索引写回模型，
/// 因此含空格的参数（如 <c>-Dfoo=bar baz</c>）不会被拆成两项。
/// </summary>
public partial class ArgumentItemViewModel : ViewModelBase
{
    /// <summary>保存回调（由所属编辑器提供）。</summary>
    private readonly Func<ArgumentItemViewModel, Task> _save;
    /// <summary>删除回调（由所属编辑器提供）。</summary>
    private readonly Func<ArgumentItemViewModel, Task> _remove;

    /// <summary>最近一次落盘的值，用于判断是否被改过。</summary>
    private string _savedValue;

    /// <summary>
    /// 构造参数项。
    /// </summary>
    /// <param name="value">参数文本。</param>
    /// <param name="isJvm">true 表示 JVM 参数，false 表示附加参数。</param>
    /// <param name="save">保存回调。</param>
    /// <param name="remove">删除回调。</param>
    public ArgumentItemViewModel(
        string value,
        bool isJvm,
        Func<ArgumentItemViewModel, Task> save,
        Func<ArgumentItemViewModel, Task> remove)
    {
        _value = value;
        _savedValue = value;
        _save = save;
        _remove = remove;
        IsJvm = isJvm;
    }

    /// <summary>参数文本。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _value;

    /// <summary>是否为 JVM 参数（写入 user_jvm_args.txt）；false 为附加参数。</summary>
    public bool IsJvm { get; }

    /// <summary>是否与已保存的值不同。</summary>
    public bool IsDirty => !string.Equals(Value, _savedValue, StringComparison.Ordinal);

    /// <summary>标记当前值已保存。</summary>
    public void MarkSaved()
    {
        _savedValue = Value;
        OnPropertyChanged(nameof(IsDirty));
    }

    /// <summary>保存该项。</summary>
    [RelayCommand]
    private Task SaveAsync() => _save(this);

    /// <summary>删除该项。</summary>
    [RelayCommand]
    private Task RemoveAsync() => _remove(this);
}
