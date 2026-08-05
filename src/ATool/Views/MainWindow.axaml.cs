using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ATool.Models;
using ATool.ViewModels;

namespace ATool.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;
    private bool _quitting;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (_vm is not null) return;
        _vm = vm;

        // 托盘：显隐主窗口 / 退出（二次确认）
        vm.ShowWindowRequested += () =>
        {
            Show();
            Activate();
        };
        vm.QuitRequested += ConfirmQuit;

        // 提醒：编辑面板打开 / 删除二次确认 / 日历日期联动
        vm.Reminders.EditRequested += r => vm.Reminders.OpenEditor(r);
        vm.Reminders.DeleteRequested += ConfirmDeleteReminder;
        vm.Calendar.SelectedDateChanged += d => vm.Reminders.SetDateFilter(d);

        // Key：删除二次确认
        vm.ApiKeys.DeleteRequested += ConfirmDeleteKey;

        vm.LoadAll();
        vm.Calendar.Load(); // 日历默认当月数据（此前未接线导致日历空白）
    }

    /// <summary>关闭主窗口 → 隐藏并驻留托盘（退出走托盘菜单二次确认）。</summary>
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_quitting) return;
        e.Cancel = true;
        Hide();
    }

    private void ConfirmQuit()
    {
        var dlg = new ConfirmDialog
        {
            MessageText = "确定要退出 A工具 吗？退出后提醒将不再触发。"
        };
        dlg.Confirmed += () =>
        {
            _quitting = true;
            Close();
        };
        dlg.ShowDialog(this);
    }

    private void ConfirmDeleteReminder(ReminderItemVm item)
    {
        var dlg = new ConfirmDialog { MessageText = $"确定删除提醒「{item.Title}」吗？" };
        dlg.Confirmed += () => _vm?.Reminders.ConfirmDelete(item);
        dlg.ShowDialog(this);
    }

    private void ConfirmDeleteKey(ApiKeyItemVm item)
    {
        var dlg = new ConfirmDialog { MessageText = $"确定删除 Key「{item.Alias}」吗？其余额历史将一并清除。" };
        dlg.Confirmed += () => _vm?.ApiKeys.ConfirmDelete(item);
        dlg.ShowDialog(this);
    }
}
