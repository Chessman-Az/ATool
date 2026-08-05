using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace ATool.Views;

/// <summary>浮窗列表项：提醒 Id + 标题 + 是否已完成（点击圆圈按 Id 切换完成状态）。</summary>
public sealed record FloatReminderItem(long Id, string Title, bool IsDone);

/// <summary>桌面提醒浮窗窗口：无边框、不抢焦点、常驻角落（位置/显隐由 FloatReminderService 控制）。</summary>
public partial class FloatReminderWindow : Window
{
    public ObservableCollection<FloatReminderItem> Items { get; } = [];

    /// <summary>点击待办圆圈 → 请求标记完成（携带提醒 Id）。</summary>
    public event Action<long>? CompleteRequested;

    public FloatReminderWindow()
    {
        InitializeComponent();
        ReminderList.ItemsSource = Items;
    }

    /// <summary>刷新提醒列表内容（UI 线程调用）。</summary>
    public void SetReminders(IEnumerable<FloatReminderItem> items)
    {
        Items.Clear();
        foreach (var t in items) Items.Add(t);
        EmptyText.IsVisible = Items.Count == 0;
    }

    /// <summary>仅背景透明度（0-1）：白色背景与边框带 alpha，文字/圆圈保持不透明。</summary>
    public void ApplyBackgroundOpacity(double opacity)
    {
        var a = (byte)(opacity * 255);
        RootBorder.Background = new SolidColorBrush(Color.FromArgb(a, 255, 255, 255));
        RootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(a, 228, 232, 240));
    }

    private void OnCircleClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is FloatReminderItem item)
            CompleteRequested?.Invoke(item.Id);
    }
}
