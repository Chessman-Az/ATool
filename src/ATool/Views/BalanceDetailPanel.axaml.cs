using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ATool.ViewModels;

namespace ATool.Views;

public partial class BalanceDetailPanel : UserControl
{
    public BalanceDetailPanel() => InitializeComponent();

    /// <summary>打开充值明细窗口（DI 解析 VM，展示余额增加记录与实际金额设置）。</summary>
    private void OpenRecharge(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Program.Services?.GetService<RechargeViewModel>() is { } vm)
        {
            vm.Load(); // 每次打开重新识别充值记录
            new RechargeWindow { DataContext = vm }.Show();
        }
    }
}
