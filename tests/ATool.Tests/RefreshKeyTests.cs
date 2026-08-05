using System.Net;
using ATool.Data;
using ATool.Models;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>单 Key 独立刷新（列表项「刷新」按钮）的回归锚点。</summary>
public class RefreshKeyTests
{
    private const string BalanceJson =
        """{"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"10.00","granted_balance":"0.00","topped_up_balance":"10.00"}]}""";

    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(BalanceJson) });
    }

    [Fact]
    public async Task RefreshKeyAsync_只刷新目标Key_不影响其他()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-rk-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var keys = new ApiKeyRepository(db);
        var history = new BalanceHistoryRepository(db);
        var http = new HttpClient(new OkHandler());
        var svc = new BalanceService(new DeepSeekClient(http), keys, history);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var a = keys.Insert(new ApiKey { Alias = "a", EncryptedKey = KeyProtection.Protect("sk-a"), CreatedAt = now });
        var b = keys.Insert(new ApiKey { Alias = "b", EncryptedKey = KeyProtection.Protect("sk-b"), CreatedAt = now });

        await svc.RefreshKeyAsync(a);

        var from = DateTime.Now.AddDays(-1);
        var to = DateTime.Now.AddDays(1);
        Assert.Single(history.GetByKey(a, from, to)); // 目标 Key 已刷新
        Assert.Empty(history.GetByKey(b, from, to));  // 其他 Key 不受影响
    }
}
