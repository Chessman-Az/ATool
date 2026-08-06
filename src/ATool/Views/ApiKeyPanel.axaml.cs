using Avalonia.Controls;
using Avalonia.Interactivity;
using Serilog;
using ATool.ViewModels;

namespace ATool.Views;

public partial class ApiKeyPanel : UserControl
{
    public ApiKeyPanel() => InitializeComponent();

    /// <summary>「立即刷新全部」：Click 直连 VM。注意：RefreshAsync() 生成的命令名是 RefreshCommand（MVVM 工具包去掉 Async 后缀），此前 XAML 绑定 RefreshAsyncCommand 是错误名导致静默失败。</summary>
    private void OnRefreshAllClick(object? sender, RoutedEventArgs e)
    {
        Log.Information("立即刷新全部按钮被点击（探针）");
        if (DataContext is ApiKeysViewModel vm)
            vm.RefreshCommand.Execute(null);
        else
            Log.Warning("立即刷新全部：DataContext 不是 ApiKeysViewModel");
    }
}
