using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ATool.Data;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>余额变动明细（内嵌面板）：联动选中 Key，展示该 Key 的查询记录与变动金额。</summary>
public partial class BalanceDetailViewModel : ObservableObject
{
    private readonly BalanceHistoryRepository _history;
    private readonly RechargeRepository _recharge;
    private readonly ApiKeysViewModel _keys;

    public ObservableCollection<BalanceHistoryDetailService.HistoryRow> Rows { get; } = [];

    [ObservableProperty]
    private string _emptyHint = "选择左侧 Key 查看余额变动明细";

    /// <summary>总充值金额（相邻余额差 &gt;0 累计；按当前选中 Key）。</summary>
    [ObservableProperty]
    private string _totalRechargeText = "¥0.00";

    /// <summary>总消费金额（相邻余额差 &lt;0 绝对值累计；按当前选中 Key）。</summary>
    [ObservableProperty]
    private string _totalConsumeText = "¥0.00";

    public BalanceDetailViewModel(BalanceHistoryRepository history, RechargeRepository recharge, ApiKeysViewModel keys)
    {
        _history = history;
        _recharge = recharge;
        _keys = keys;
        _keys.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ApiKeysViewModel.SelectedKey))
                Load();
        };
    }

    public void Load()
    {
        Rows.Clear();
        var all = _history.GetAllWithAlias();
        if (_keys.SelectedKey is { } item)
        {
            all = all.Where(r => r.ApiKeyId == item.Key.Id).ToList();
            EmptyHint = "该 Key 暂无余额记录（每次刷新自动记录）";
        }
        else
        {
            EmptyHint = "选择左侧 Key 查看余额变动明细";
        }
        foreach (var row in BalanceHistoryDetailService.BuildRows(all))
            Rows.Add(row);
        OnPropertyChanged(nameof(HasRows));

        // 总充值 / 总消费（联动当前选中 Key）；总充值含手动补录记录
        var (recharge, consume) = _history.GetTotals(_keys.SelectedKey is { } k ? k.Key.Id : null);
        var manual = _recharge.GetManualTotal();
        TotalRechargeText = $"¥{recharge + manual:F2}";
        TotalConsumeText = $"¥{consume:F2}";
    }

    public bool HasRows => Rows.Count > 0;
}
