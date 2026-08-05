using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ATool.Data;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>余额变动明细页：按时间倒序展示全部（或选中 Key 的）查询记录与变动金额。</summary>
public partial class BalanceHistoryViewModel : ObservableObject
{
    private readonly BalanceHistoryRepository _history;
    private readonly long? _apiKeyId;

    public ObservableCollection<BalanceHistoryDetailService.HistoryRow> Rows { get; } = [];

    [ObservableProperty]
    private string _title = "余额变动明细";

    public BalanceHistoryViewModel(BalanceHistoryRepository history, long? apiKeyId, string? keyAlias)
    {
        _history = history;
        _apiKeyId = apiKeyId;
        if (keyAlias is not null) Title = $"余额变动明细 · {keyAlias}";
    }

    public void Load()
    {
        Rows.Clear();
        var all = _history.GetAllWithAlias();
        if (_apiKeyId is not null) all = all.Where(r => r.ApiKeyId == _apiKeyId).ToList();
        foreach (var row in BalanceHistoryDetailService.BuildRows(all))
            Rows.Add(row);
    }
}
