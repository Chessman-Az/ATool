using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ATool.Data;
using ATool.Models;

namespace ATool.ViewModels;

/// <summary>提醒列表：状态筛选（待提醒/已完成）+ 增删改入口 + 圆圈点击完成。编辑面板由 MainWindow 呈现。</summary>
public partial class ReminderListViewModel : ObservableObject
{
    private readonly ReminderRepository _repo;
    private readonly ReminderEditViewModel _edit;

    public ObservableCollection<ReminderItemVm> Items { get; } = [];

    [ObservableProperty]
    private ReminderStatus _filter = ReminderStatus.Pending;

    [ObservableProperty]
    private ReminderItemVm? _selected;

    /// <summary>请求打开编辑面板（参数：null=新建，Reminder=编辑）。</summary>
    public event Action<Reminder?>? EditRequested;

    /// <summary>请求删除确认（视图层弹 ConfirmDialog，确认后调 ConfirmDelete）。</summary>
    public event Action<ReminderItemVm>? DeleteRequested;

    public event Action? ListChanged;

    public ReminderListViewModel(ReminderRepository repo, ReminderEditViewModel edit)
    {
        _repo = repo;
        _edit = edit;
        _edit.Saved += () => { IsEditorOpen = false; Reload(); };
        _edit.Cancelled += () => { IsEditorOpen = false; };
    }

    /// <summary>编辑面板 VM（MainWindow 绑定）。</summary>
    public ReminderEditViewModel Editor => _edit;

    /// <summary>打开编辑面板。</summary>
    public void OpenEditor(Reminder? r)
    {
        if (r is null) _edit.BeginNew();
        else _edit.BeginEdit(r);
        IsEditorOpen = true;
    }

    [ObservableProperty]
    private bool _isEditorOpen;

    [RelayCommand]
    private void CloseEditor()
    {
        IsEditorOpen = false;
        Reload();
    }

    public void Reload()
    {
        Items.Clear();
        foreach (var r in _repo.GetAll(Filter))
            Items.Add(new ReminderItemVm(r, OnCompleteRequested));
    }

    /// <summary>圆圈点击 → 在「完成 / 未完成」间切换；完成后刷新列表（与当前筛选一致）。</summary>
    public void ToggleComplete(ReminderItemVm item)
    {
        var newStatus = item.IsDone ? ReminderStatus.Pending : ReminderStatus.Done;
        _repo.SetStatus(item.Reminder.Id, newStatus);
        item.Reminder.Status = newStatus;
        Reload();
    }

    private void OnCompleteRequested(ReminderItemVm item) => ToggleComplete(item);

    [RelayCommand]
    private void FilterPending() => SetFilter(ReminderStatus.Pending);

    [RelayCommand]
    private void FilterDone() => SetFilter(ReminderStatus.Done);

    private void SetFilter(ReminderStatus status)
    {
        Filter = status;
        Reload();
    }

    [RelayCommand]
    private void Add() => EditRequested?.Invoke(null);

    [RelayCommand]
    private void Edit()
    {
        if (Selected is { } item) EditRequested?.Invoke(item.Reminder);
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is { } item) DeleteRequested?.Invoke(item);
    }

    /// <summary>视图层确认后的实际删除。</summary>
    public void ConfirmDelete(ReminderItemVm item)
    {
        _repo.Delete(item.Reminder.Id);
        Items.Remove(item);
        ListChanged?.Invoke();
    }
}

/// <summary>提醒列表项包装：圆圈完成命令 + 展示文本。</summary>
public partial class ReminderItemVm : ObservableObject
{
    private readonly Action<ReminderItemVm> _onComplete;

    public Reminder Reminder { get; }
    public string Title => Reminder.Title;
    public string TriggerTime => Reminder.TriggerTime;
    public string TriggeredCountText => $"已触发 {Reminder.TriggeredCount} 次";
    public string RepeatText => Reminder.RepeatType switch
    {
        RepeatType.Single => "单次",
        RepeatType.Daily => "每日",
        _ => "每周几"
    };
    public bool IsDone => Reminder.Status == ReminderStatus.Done;
    public string StatusText => IsDone ? "已完成" : "待提醒";

    public ReminderItemVm(Reminder r, Action<ReminderItemVm> onComplete)
    {
        Reminder = r;
        _onComplete = onComplete;
    }

    [RelayCommand]
    private void Complete() => _onComplete(this);
}
