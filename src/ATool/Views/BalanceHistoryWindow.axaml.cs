using Avalonia.Controls;
using ATool.Data;
using ATool.ViewModels;

namespace ATool.Views;

/// <summary>余额变动明细窗口：repo 由 MainWindow 侧从 DI 容器解析后传入。</summary>
public partial class BalanceHistoryWindow : Window
{
    public BalanceHistoryWindow(BalanceHistoryRepository history, long? apiKeyId = null, string? keyAlias = null)
    {
        InitializeComponent();
        var vm = new BalanceHistoryViewModel(history, apiKeyId, keyAlias);
        DataContext = vm;
        Opened += (_, _) => vm.Load();
    }
}
