using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ATool.Data;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>余额变动明细（内嵌面板）：联动选中 Key，展示该 Key 的查询记录与变动金额。</summary>
public partial class BalanceDetailViewModel : ObservableObject
{
    private readonly BalanceHistoryRepository _history;
    private readonly ApiKeysViewModel _keys;

    public ObservableCollection<BalanceHistoryDetailService.HistoryRow> Rows { get; } = [];

    [ObservableProperty]
    private string _emptyHint = "选择左侧 Key 查看余额变动明细";

    public BalanceDetailViewModel(BalanceHistoryRepository history, ApiKeysViewModel keys)
    {
        _history = history;
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
    }

    public bool HasRows => Rows.Count > 0;
}
