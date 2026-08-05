using System.Net;
using ATool.Data;
using ATool.Models;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>刷新门（SemaphoreSlim）在事件订阅者抛异常时仍释放的回归锚点。</summary>
public class BalanceServiceGateTests
{
    private const string BalanceJson =
        """{"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"10.00","granted_balance":"0.00","topped_up_balance":"10.00"}]}""";

    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(BalanceJson) });
    }

    [Fact]
    public async Task RefreshAllAsync_事件订阅者抛异常_gate仍释放且后续刷新可用()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-gate-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var keys = new ApiKeyRepository(db);
        var history = new BalanceHistoryRepository(db);
        var http = new HttpClient(new OkHandler());
        var svc = new BalanceService(new DeepSeekClient(http), keys, history);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        keys.Insert(new ApiKey { Alias = "a", EncryptedKey = KeyProtection.Protect("sk-a"), CreatedAt = now });

        // 订阅者抛异常：模拟 Reload 失败等场景
        svc.StateChanged += () => throw new InvalidOperationException("订阅者异常");

        await svc.RefreshAllAsync(); // 不应抛（内部防御），gate 必须释放

        Assert.False(svc.IsRefreshing); // 刷新标志已复位
        await svc.RefreshAllAsync();    // 第二次能进入（gate 未被死锁）
        Assert.False(svc.IsRefreshing);
    }
}
