using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ATool.Data;
using ATool.Models;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>API Key 管理：列表（余额+变动显示）、添加（先调余额接口验证）、删除（二次确认）、刷新。</summary>
public partial class ApiKeysViewModel : ObservableObject
{
    private readonly ApiKeyRepository _repo;
    private readonly BalanceService _balance;
    private readonly DeepSeekClient _client;
    private readonly BalanceHistoryRepository _history;

    public ObservableCollection<ApiKeyItemVm> Keys { get; } = [];

    [ObservableProperty]
    private string _newAlias = "";

    [ObservableProperty]
    private string _newKey = "";

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isRefreshing;

    /// <summary>当前选中的 Key（图表联动）。</summary>
    [ObservableProperty]
    private ApiKeyItemVm? _selectedKey;

    /// <summary>请求删除确认（视图层弹 ConfirmDialog，确认后调 ConfirmDelete）。</summary>
    public event Action<ApiKeyItemVm>? DeleteRequested;

    /// <summary>全部 Key 余额合计（开干下方汇总行）。</summary>
    [ObservableProperty]
    private decimal _totalBalance;

    public ApiKeysViewModel(ApiKeyRepository repo, BalanceService balance, DeepSeekClient client, BalanceHistoryRepository history)
    {
        _repo = repo;
        _balance = balance;
        _client = client;
        _history = history;
        _balance.StateChanged += () =>
        {
            IsRefreshing = _balance.IsRefreshing;
            if (!_balance.IsRefreshing)
                Reload(); // 自动刷新/任意来源刷新完成后同步 UI（余额与合计即时更新）
        };
    }

    public void Reload()
    {
        Keys.Clear();
        foreach (var k in _repo.GetAll())
        {
            k.PlainKey = KeyProtection.Unprotect(k.EncryptedKey);
            var latest = _history.GetLatest(k.Id);
            Keys.Add(new ApiKeyItemVm(k, latest?.TotalBalance, latest?.Delta, OnDeleteRequested, OnRefreshRequested));
        }
        TotalBalance = BalanceSummaryService.Sum(Keys.Select(k => k.Balance));
    }

    private void OnDeleteRequested(ApiKeyItemVm item) => DeleteRequested?.Invoke(item);

    /// <summary>单 Key 独立刷新：仅刷新该 Key，完成后刷新列表显示；失败给出可见提示。</summary>
    private async Task OnRefreshRequested(ApiKeyItemVm item)
    {
        try
        {
            await _balance.RefreshKeyAsync(item.Key.Id);
            Reload();
            if (item.LastError is not null)
                Message = $"{item.Key.Alias} 刷新失败：{item.LastError}";
        }
        catch (Exception ex)
        {
            Message = $"{item.Key.Alias} 刷新异常：{ex.Message}";
        }
    }

    /// <summary>添加 Key：先调余额接口验证有效性；无效拒绝保存并报错。</summary>
    [RelayCommand]
    private async Task AddKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAlias) || string.IsNullOrWhiteSpace(NewKey))
        {
            Message = "请填写别名和 API Key";
            return;
        }
        var key = NewKey.Trim();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await _client.GetBalanceAsync(key, cts.Token);
        if (!result.Success)
        {
            Message = $"Key 验证失败，未保存：{result.Error}";
            return;
        }
        _repo.Insert(new ApiKey
        {
            Alias = NewAlias.Trim(),
            EncryptedKey = KeyProtection.Protect(key),
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        });
        NewAlias = "";
        NewKey = "";
        Message = null;
        Reload();
        await _balance.RefreshAllAsync();
        Reload();
    }

    [RelayCommand]
    private void Delete(ApiKeyItemVm? item)
    {
        if (item is not null) DeleteRequested?.Invoke(item);
    }

    /// <summary>视图层确认后的实际删除（级联清余额历史）。</summary>
    public void ConfirmDelete(ApiKeyItemVm item)
    {
        _repo.Delete(item.Key.Id);
        Keys.Remove(item);
        Message = $"已删除 {item.Key.Alias}";
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RefreshAsync()
    {
        try
        {
            await _balance.RefreshAllAsync();
            Reload();
            var failed = Keys.Count(k => k.LastError is not null);
            Message = failed > 0 ? $"{failed} 个 Key 刷新失败（详见列表）" : null;
        }
        catch (Exception ex)
        {
            Message = $"刷新异常：{ex.Message}";
        }
    }
}

/// <summary>Key 列表项：余额与变动金额展示包装 + 删除/独立刷新命令。</summary>
public partial class ApiKeyItemVm : ObservableObject
{
    private readonly Action<ApiKeyItemVm> _onDelete;
    private readonly Func<ApiKeyItemVm, Task> _onRefresh;

    public ApiKey Key { get; }
    public string Alias => Key.Alias;
    public string? LastError => Key.LastError;
    public string CreatedAt => Key.CreatedAt;

    public ApiKeyItemVm(ApiKey key, decimal? balance, decimal? delta, Action<ApiKeyItemVm> onDelete, Func<ApiKeyItemVm, Task> onRefresh)
    {
        Key = key;
        _balance = balance;
        _delta = delta;
        _onDelete = onDelete;
        _onRefresh = onRefresh;
    }

    [ObservableProperty]
    private decimal? _balance;

    [ObservableProperty]
    private decimal? _delta;

    [RelayCommand]
    private void Delete() => _onDelete(this);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Refresh() => await _onRefresh(this);

    public string BalanceText => Balance is { } b ? b.ToString("F2") : "—";
    public string DeltaText => Delta is { } d ? (d >= 0 ? "+" : "") + d.ToString("F2") : "";
    public string CurrencyText => "CNY";
}
