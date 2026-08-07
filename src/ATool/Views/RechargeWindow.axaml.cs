using Avalonia.Controls;
using ATool.ViewModels;

namespace ATool.Views;

public partial class RechargeWindow : Window
{
    public RechargeWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is RechargeViewModel vm)
                vm.DeleteRequested += ConfirmDeleteItem;
        };
    }

    /// <summary>删除按钮 → 二次确认（确认后调 VM.ConfirmDelete）。</summary>
    private void ConfirmDeleteItem(RechargeItemVm item)
    {
        var dlg = new ConfirmDialog
        {
            MessageText = $"确定删除 {item.QueriedAt} 的充值记录（{item.DeltaText}）吗？"
        };
        dlg.Confirmed += () =>
        {
            if (DataContext is RechargeViewModel vm)
                vm.ConfirmDelete(item);
        };
        dlg.ShowDialog(this);
    }
}
