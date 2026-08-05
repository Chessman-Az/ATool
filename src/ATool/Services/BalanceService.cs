using Serilog;
using ATool.Data;
using ATool.Models;

namespace ATool.Services;

/// <summary>
/// 余额刷新编排：并发刷新所有 Key、单 Key 失败隔离、历史写入与变动金额、自动刷新定时。
/// </summary>
public sealed class BalanceService
{
    private readonly DeepSeekClient _client;
    private readonly ApiKeyRepository _keys;
    private readonly BalanceHistoryRepository _history;
    private readonly System.Timers.Timer _autoTimer;
    private bool _refreshing;

    /// <summary>刷新开始/完成时通知 UI（手动刷新按钮状态等）。</summary>
    public event Action? StateChanged;

    public BalanceService(DeepSeekClient client, ApiKeyRepository keys, BalanceHistoryRepository history)
    {
        _client = client;
        _keys = keys;
        _history = history;
        _autoTimer = new System.Timers.Timer(30 * 60_000) { AutoReset = true };
        _autoTimer.Elapsed += (_, _) => _ = RefreshAllAsync();
    }

    public bool IsRefreshing => _refreshing;

    /// <summary>并发刷新全部 Key；单个 Key 失败/超时只影响该 Key 的 LastError。</summary>
    public async Task RefreshAllAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        StateChanged?.Invoke();
        try
        {
            var keys = _keys.GetAll();
            if (keys.Count == 0) return;
            Log.Information("开始刷新 {Count} 个 Key 余额", keys.Count);
            await Task.WhenAll(keys.Select(RefreshOneAsync));
            Log.Information("余额刷新完成");
        }
        finally
        {
            _refreshing = false;
            StateChanged?.Invoke();
        }
    }

    private async Task RefreshOneAsync(ApiKey key)
    {
        key.PlainKey ??= KeyProtection.Unprotect(key.EncryptedKey);
        if (key.PlainKey is null)
        {
            key.LastError = "无法解密 Key，请删除后重新添加";
            _keys.UpdateLastError(key.Id, key.LastError);
            return;
        }

        // 30 秒超时双保险：HttpClient.Timeout 兜底 + CTS 主动取消（不重试）
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await _client.GetBalanceAsync(key.PlainKey, cts.Token);

        if (result.Success)
        {
            var prev = _history.GetLatest(key.Id);
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _history.Insert(new BalanceRecord
            {
                ApiKeyId = key.Id,
                TotalBalance = result.TotalBalance,
                GrantedBalance = result.Granted,
                ToppedUpBalance = result.ToppedUp,
                Currency = result.Currency,
                QueriedAt = now,
                Delta = prev is null ? null : result.TotalBalance - prev.TotalBalance,
            });
            key.LastError = null;
            Log.Information("余额刷新成功: Key#{Id} {Alias} = {Balance} {Currency}", key.Id, key.Alias, result.TotalBalance, result.Currency);
        }
        else
        {
            key.LastError = result.Error;
            Log.Warning("余额刷新失败: Key#{Id} {Alias} -> {Error}", key.Id, key.Alias, result.Error);
        }
        _keys.UpdateLastError(key.Id, key.LastError);
    }

    /// <summary>设置自动刷新间隔（分钟，最低 5）。</summary>
    public void SetAutoRefreshMinutes(int minutes)
    {
        if (minutes < 5) throw new ArgumentException("自动刷新间隔最低 5 分钟");
        _autoTimer.Interval = minutes * 60_000;
        if (!_autoTimer.Enabled) _autoTimer.Start();
    }

    public void StartAutoRefresh() => _autoTimer.Start();
    public void StopAutoRefresh() => _autoTimer.Stop();
}
