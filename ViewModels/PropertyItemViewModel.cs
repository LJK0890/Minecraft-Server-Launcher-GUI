using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MinecraftServerLauncher.ViewModels;

/// <summary>
/// server.properties 中的一条键值对。
/// 键不可改（改键等于删旧增新），值可直接编辑并单独保存。
/// </summary>
public partial class PropertyItemViewModel : ViewModelBase
{
    /// <summary>保存回调（由所属编辑器提供）。</summary>
    private readonly Func<PropertyItemViewModel, Task> _save;
    /// <summary>删除回调（由所属编辑器提供）。</summary>
    private readonly Func<PropertyItemViewModel, Task> _remove;

    /// <summary>最近一次落盘的值，用于判断是否被改过。</summary>
    private string _savedValue;

    /// <summary>
    /// 构造配置项。
    /// </summary>
    /// <param name="key">配置键。</param>
    /// <param name="value">配置值。</param>
    /// <param name="save">保存回调。</param>
    /// <param name="remove">删除回调。</param>
    public PropertyItemViewModel(
        string key,
        string value,
        Func<PropertyItemViewModel, Task> save,
        Func<PropertyItemViewModel, Task> remove)
    {
        Key = key;
        _value = value;
        _savedValue = value;
        _save = save;
        _remove = remove;
    }

    /// <summary>配置键。</summary>
    public string Key { get; }

    /// <summary>配置值（可编辑）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _value;

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
