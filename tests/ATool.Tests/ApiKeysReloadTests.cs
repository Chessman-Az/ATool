using System.Net;
using ATool.Data;
using ATool.Models;
using ATool.Services;
using ATool.ViewModels;
using Xunit;

namespace ATool.Tests;

/// <summary>余额列表 Reload → 合计与余额显示的回归锚点（0.00 问题的逻辑层验证）。</summary>
public class ApiKeysReloadTests
{
    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    [Fact]
    public void Reload_从历史读取余额并计算合计()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-rel-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var keys = new ApiKeyRepository(db);
        var history = new BalanceHistoryRepository(db);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var a = keys.Insert(new ApiKey { Alias = "a", EncryptedKey = KeyProtection.Protect("sk-a"), CreatedAt = now });
        var b = keys.Insert(new ApiKey { Alias = "b", EncryptedKey = KeyProtection.Protect("sk-b"), CreatedAt = now });
        history.Insert(new BalanceRecord { ApiKeyId = a, TotalBalance = 100.25m, QueriedAt = now });
        history.Insert(new BalanceRecord { ApiKeyId = b, TotalBalance = 50.75m, QueriedAt = now });

        var http = new HttpClient(new OkHandler());
        var client = new DeepSeekClient(http);
        var vm = new ApiKeysViewModel(keys, new BalanceService(client, keys, history), client, history);

        vm.Reload();

        Assert.Equal(2, vm.Keys.Count);
        Assert.Equal(100.25m, vm.Keys.First(k => k.Key.Id == a).Balance);
        Assert.Equal(151.00m, vm.TotalBalance); // 合计 = 100.25 + 50.75
    }

    [Fact]
    public void Reload_无历史记录_合计为0()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-rel0-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var keys = new ApiKeyRepository(db);
        var history = new BalanceHistoryRepository(db);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        keys.Insert(new ApiKey { Alias = "a", EncryptedKey = KeyProtection.Protect("sk-a"), CreatedAt = now });

        var http = new HttpClient(new OkHandler());
        var client = new DeepSeekClient(http);
        var vm = new ApiKeysViewModel(keys, new BalanceService(client, keys, history), client, history);

        vm.Reload();

        Assert.Equal(0m, vm.TotalBalance);
        Assert.Equal("—", vm.Keys[0].BalanceText);
    }
}
