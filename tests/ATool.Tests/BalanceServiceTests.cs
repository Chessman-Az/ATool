using System.Net;
using ATool.Data;
using ATool.Models;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>余额刷新并发串行化（添加 Key 后立即刷新不被自动刷新吞掉）的回归锚点。</summary>
public class BalanceServiceTests
{
    private const string BalanceJson =
        """{"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"10.00","granted_balance":"0.00","topped_up_balance":"10.00"}]}""";

    private sealed class SlowHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(150, cancellationToken); // 模拟慢响应，制造并发窗口
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(BalanceJson) };
        }
    }

    private static (ApiKeyRepository Keys, BalanceHistoryRepository History, BalanceService Service) Create()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-bal-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var keys = new ApiKeyRepository(db);
        var history = new BalanceHistoryRepository(db);
        // 注意：不能 using——HttpClient 需存活到刷新完成（Create 返回后释放会导致请求失败）
        var http = new HttpClient(new SlowHandler());
        var service = new BalanceService(new DeepSeekClient(http), keys, history);
        return (keys, history, service);
    }

    [Fact]
    public async Task RefreshAllAsync_并发两次_两次都执行且同值去重()
    {
        var (keys, history, service) = Create();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        keys.Insert(new ApiKey { Alias = "a", EncryptedKey = KeyProtection.Protect("sk-a"), CreatedAt = now });
        keys.Insert(new ApiKey { Alias = "b", EncryptedKey = KeyProtection.Protect("sk-b"), CreatedAt = now });

        await Task.WhenAll(service.RefreshAllAsync(), service.RefreshAllAsync());

        Assert.False(service.IsRefreshing); // 锁已释放
        var from = DateTime.Now.AddDays(-1);
        var to = DateTime.Now.AddDays(1);
        // 两次刷新余额相同（固定 10.00）→ 明细去重，每个 Key 只记录 1 条
        Assert.Single(history.GetByKey(1, from, to));
        Assert.Single(history.GetByKey(2, from, to));
    }

    [Fact]
    public async Task RefreshAllAsync_余额与上一条相同_不重复记录历史()
    {
        var (keys, history, service) = Create();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var id = keys.Insert(new ApiKey { Alias = "a", EncryptedKey = KeyProtection.Protect("sk-a"), CreatedAt = now });

        await service.RefreshAllAsync(); // 10.00
        await service.RefreshAllAsync(); // 仍 10.00 → 不记录

        var from = DateTime.Now.AddDays(-1);
        var to = DateTime.Now.AddDays(1);
        Assert.Single(history.GetByKey(id, from, to));
    }
}
