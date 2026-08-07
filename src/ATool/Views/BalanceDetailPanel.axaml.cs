using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ATool.ViewModels;

namespace ATool.Views;

public partial class BalanceDetailPanel : UserControl
{
    public BalanceDetailPanel() => InitializeComponent();

    /// <summary>打开充值明细窗口（DI 解析 VM，展示余额增加记录与实际金额设置）；关闭后刷新总充值/总消费。</summary>
    private void OpenRecharge(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Program.Services?.GetService<RechargeViewModel>() is { } vm)
        {
            vm.Load(); // 每次打开重新识别充值记录
            if (DataContext is BalanceDetailViewModel detail)
                vm.SelectAlias(detail.CurrentKeyAlias); // 初始筛选跟随当前选中 Key（不显示其他别名的记录）
            var win = new RechargeWindow { DataContext = vm };
            win.Closed += (_, _) =>
            {
                if (DataContext is BalanceDetailViewModel detail)
                    detail.Load(); // 手动添加后总充值金额联动
            };
            win.Show();
        }
    }
}
