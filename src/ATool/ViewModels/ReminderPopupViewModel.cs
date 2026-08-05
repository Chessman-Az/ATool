using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>提醒弹窗 VM：聚合多条同时触发的提醒，每项独立完成/延迟。</summary>
public partial class ReminderPopupViewModel : ObservableObject
{
    private readonly ReminderPopupService _service;

    [ObservableProperty]
    private string _title = "";

    public ObservableCollection<PopupItemViewModel> Items { get; } = [];

    /// <summary>全部项处理完毕时触发（视图订阅后关闭窗口）。</summary>
    public event Action? Closed;

    public ReminderPopupViewModel(ReminderPopupService service, IReadOnlyList<ReminderPopupItem> items)
    {
        _service = service;
        foreach (var it in items)
            Items.Add(new PopupItemViewModel(this, it));
        Title = Items.Count == 1
            ? $"提醒：{Items[0].Item.Reminder.Title}"
            : $"{Items.Count} 条提醒";
    }

    public void CompleteItem(PopupItemViewModel vm)
    {
        _service.Complete(vm.Item.Reminder);
        Items.Remove(vm);
        CheckEmpty();
    }

    public void SnoozeItem(PopupItemViewModel vm, int minutes)
    {
        _service.Snooze(vm.Item.Reminder, minutes);
        Items.Remove(vm);
        CheckEmpty();
    }

    private void CheckEmpty()
    {
        if (Items.Count == 0) Closed?.Invoke();
    }
}

/// <summary>弹窗内单条提醒项。</summary>
public partial class PopupItemViewModel : ObservableObject
{
    private readonly ReminderPopupViewModel _owner;

    public ReminderPopupItem Item { get; }

    public string DisplayTime => Item.Reminder.TriggerTime;

    public string MissedText => Item.MissedCount > 1 ? $"（错过 {Item.MissedCount} 次）" : "";

    public PopupItemViewModel(ReminderPopupViewModel owner, ReminderPopupItem item)
    {
        _owner = owner;
        Item = item;
    }

    [RelayCommand]
    private void Complete() => _owner.CompleteItem(this);

    [RelayCommand]
    private void Snooze5() => Snooze(5);

    [RelayCommand]
    private void Snooze15() => Snooze(15);

    [RelayCommand]
    private void Snooze30() => Snooze(30);

    [RelayCommand]
    private void Snooze60() => Snooze(60);

    [RelayCommand]
    private void SnoozeCustom(string? minutes)
    {
        if (int.TryParse(minutes, out var m) && m is > 0 and <= 1440) Snooze(m);
    }

    private void Snooze(int minutes) => _owner.SnoozeItem(this, minutes);
}
