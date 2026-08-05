using System.Collections.ObjectModel;
using Avalonia.Controls;
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
}
