using Avalonia.Controls;
using ATool.Services;

namespace ATool.Views;

/// <summary>中控台快捷启动工具选择对话框（code-behind 直用，不走 MVVM）。</summary>
public partial class QuickToolsDialog : Window
{
    private sealed record ToolChoice(string Name, string Executable) { public bool Selected { get; set; } }

    private readonly List<ToolChoice> _choices = [];

    public QuickToolsDialog(IEnumerable<string> enabledExecutables)
    {
        InitializeComponent();
        foreach (var t in ToolCatalog.All)
            _choices.Add(new ToolChoice(t.Name, t.Executable) { Selected = enabledExecutables.Contains(t.Executable) });
        ToolList.ItemsSource = _choices;
    }

    /// <summary>用户点「保存」时触发；点「取消」/关闭不触发。</summary>
    public event Action<List<string>>? Confirmed;

    private void OnConfirm(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Confirmed?.Invoke(_choices.Where(c => c.Selected).Select(c => c.Executable).ToList());
        Close();
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
