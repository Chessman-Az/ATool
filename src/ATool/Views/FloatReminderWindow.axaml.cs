using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace ATool.Views;

/// <summary>桌面提醒浮窗窗口：无边框、不抢焦点、常驻角落（位置/显隐由 FloatReminderService 控制）。</summary>
public partial class FloatReminderWindow : Window
{
    public ObservableCollection<string> Items { get; } = [];

    public FloatReminderWindow()
    {
        InitializeComponent();
        ReminderList.ItemsSource = Items;
    }

    /// <summary>刷新提醒列表内容（UI 线程调用）。</summary>
    public void SetReminders(IEnumerable<string> titles)
    {
        Items.Clear();
        foreach (var t in titles) Items.Add(t);
        EmptyText.IsVisible = Items.Count == 0;
    }

    /// <summary>仅背景透明度（0-1）：白色背景与边框带 alpha，文字/圆圈保持不透明。</summary>
    public void ApplyBackgroundOpacity(double opacity)
    {
        var a = (byte)(opacity * 255);
        RootBorder.Background = new SolidColorBrush(Color.FromArgb(a, 255, 255, 255));
        RootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(a, 228, 232, 240));
    }
}
