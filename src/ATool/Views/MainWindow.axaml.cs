using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ATool.Models;
using ATool.ViewModels;

namespace ATool.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;
    private bool _quitting;
    private readonly DispatcherTimer _spinnerTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private double _spinnerAngle;

    public MainWindow()
    {
        InitializeComponent();
        // 版本戳：标题显示构建时间，便于确认运行的是否为最新发布版
        // 注意：单文件发布下 Assembly.Location 为空，需用 ProcessPath
        var exe = Environment.ProcessPath ?? typeof(MainWindow).Assembly.Location;
        var stamp = File.GetLastWriteTime(exe).ToString("MM-dd HH:mm");
        Title = $"A工具 v{stamp}";
        // 刷新中：中央圆圈旋转动画（代码驱动，Avalonia 声明式动画对 RenderTransform 不支持）
        _spinnerTimer.Tick += (_, _) =>
        {
            _spinnerAngle = (_spinnerAngle + 6) % 360;
            if (RefreshSpinner.RenderTransform is RotateTransform rt)
                rt.Angle = _spinnerAngle;
        };
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
        // 刷新动画开关
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.IsRefreshing))
            {
                if (vm.IsRefreshing) _spinnerTimer.Start();
                else { _spinnerTimer.Stop(); _spinnerAngle = 0; }
            }
        };

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
            // 显式 Shutdown：浮窗窗口常驻会阻止默认的 OnLastWindowClose 退出（曾致托盘点一次退不干净）
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
            else
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
