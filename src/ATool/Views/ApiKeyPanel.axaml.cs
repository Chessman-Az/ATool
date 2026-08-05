using Avalonia.Controls;
using Avalonia.Interactivity;
using Serilog;

namespace ATool.Views;

public partial class ApiKeyPanel : UserControl
{
    public ApiKeyPanel() => InitializeComponent();

    /// <summary>探针：区分「按钮点击未发生」与「Command 未触发」。</summary>
    private void OnRefreshAllClick(object? sender, RoutedEventArgs e)
    {
        Log.Information("立即刷新全部按钮被点击（探针）");
    }
}
