using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ATool.Data;
using ATool.Models;

namespace ATool.ViewModels;

/// <summary>提醒列表：状态筛选（待提醒/已完成）+ 增删改入口。编辑面板由 MainWindow 呈现。</summary>
public partial class ReminderListViewModel : ObservableObject
{
    private readonly ReminderRepository _repo;
    private readonly ReminderEditViewModel _edit;

    public ObservableCollection<Reminder> Items { get; } = [];

    [ObservableProperty]
    private ReminderStatus _filter = ReminderStatus.Pending;

    [ObservableProperty]
    private Reminder? _selected;

    /// <summary>请求打开编辑面板（参数：null=新建，Reminder=编辑）。</summary>
    public event Action<Reminder?>? EditRequested;

    /// <summary>请求删除确认（视图层弹 ConfirmDialog，确认后调 ConfirmDelete）。</summary>
    public event Action<Reminder>? DeleteRequested;

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
            Items.Add(r);
    }

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
        if (Selected is { } r) EditRequested?.Invoke(r);
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is { } r) DeleteRequested?.Invoke(r);
    }

    /// <summary>视图层确认后的实际删除。</summary>
    public void ConfirmDelete(Reminder r)
    {
        _repo.Delete(r.Id);
        Items.Remove(r);
        ListChanged?.Invoke();
    }
}
