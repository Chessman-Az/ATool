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
            // 选中 Key 或余额刷新（TotalBalance 更新）时重算总充值/总消费
            if (e.PropertyName is nameof(ApiKeysViewModel.SelectedKey) or nameof(ApiKeysViewModel.TotalBalance))
                Load();
        };
    }

    public void Load()
    {        Rows.Clear();
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

        // 总充值 = 余额相邻差累计 + 手动补录（手动补录按选中别名归属，各别名单独统计）；
        // 总消费 = 总充值 − 当前实时余额（与 DeepSeek 余额严格对应）
        var (recharge, _) = _history.GetTotals(_keys.SelectedKey is { } k ? k.Key.Id : null);
        var manualTotal = _keys.SelectedKey is { } selected ? _recharge.GetManualTotal(selected.Alias) : _recharge.GetManualTotal();
        var totalRecharge = recharge + manualTotal;
        var balance = _keys.SelectedKey is { } sel && sel.Balance is { } b ? b : _keys.TotalBalance;
        var consume = Math.Max(0, totalRecharge - balance);
        TotalRechargeText = $"¥{totalRecharge:F2}";
        TotalConsumeText = $"¥{consume:F2}";
    }

    public bool HasRows => Rows.Count > 0;

    /// <summary>当前选中 Key 的别名（充值明细窗口初始筛选用；未选中返回 null）。</summary>
    public string? CurrentKeyAlias => _keys.SelectedKey?.Alias;
}
