using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ATool.Views;

/// <summary>通用二次确认对话框（code-behind 直用，不走 MVVM）。</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public string MessageText
    {
        set => MsgText.Text = value;
    }

    /// <summary>用户点「确认」时触发；点「取消」/关闭不触发。</summary>
    public event Action? Confirmed;

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        Confirmed?.Invoke();
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
