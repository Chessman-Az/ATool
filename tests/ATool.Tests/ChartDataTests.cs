using ATool.Data;
using ATool.Models;
using ATool.Services;
using ATool.ViewModels;
using Xunit;

namespace ATool.Tests;

public class ChartDataTests
{
    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }

    private static BalanceRecord Rec(string time, decimal balance) => new()
    {
        QueriedAt = time,
        TotalBalance = balance,
    };

    [Fact]
    public void BuildPoints_乱序输入_按时间升序输出()
    {
        var records = new[]
        {
            Rec("2026-08-03 10:00:00", 90m),
            Rec("2026-08-01 10:00:00", 110m),
            Rec("2026-08-02 10:00:00", 100m),
        };
        var points = ChartDataConverter.BuildPoints(records);
        Assert.Equal(3, points.Count);
        Assert.Equal(new DateTime(2026, 8, 1, 10, 0, 0), points[0].DateTime);
        Assert.Equal(110d, points[0].Value);
        Assert.Equal(new DateTime(2026, 8, 2, 10, 0, 0), points[1].DateTime);
        Assert.Equal(new DateTime(2026, 8, 3, 10, 0, 0), points[2].DateTime);
    }

    [Fact]
    public void BuildPoints_空数据_返回空()
    {
        Assert.Empty(ChartDataConverter.BuildPoints([]));
    }

    [Fact]
    public void BuildPoints_非法时间与负余额_被过滤()
    {
        var records = new[]
        {
            Rec("不是时间", 100m),
            Rec("2026-08-01 10:00:00", -5m),
            Rec("2026-08-01 11:00:00", 100m),
        };
        var points = ChartDataConverter.BuildPoints(records);
        Assert.Single(points);
        Assert.Equal(new DateTime(2026, 8, 1, 11, 0, 0), points[0].DateTime);
    }

    [Fact]
    public void Refresh_选中Key且有历史_Series有数据且HasData为真()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-chart-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var keys = new ApiKeyRepository(db);
        var history = new BalanceHistoryRepository(db);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var a = keys.Insert(new ApiKey { Alias = "a", EncryptedKey = KeyProtection.Protect("sk-a"), CreatedAt = now });
        history.Insert(new BalanceRecord { ApiKeyId = a, TotalBalance = 100.25m, QueriedAt = now });
        history.Insert(new BalanceRecord { ApiKeyId = a, TotalBalance = 99.50m, QueriedAt = now });

        var http = new HttpClient(new OkHandler());
        var client = new DeepSeekClient(http);
        var keysVm = new ApiKeysViewModel(keys, new BalanceService(client, keys, history), client, history);
        keysVm.Reload(); // 默认选中第一个 Key
        var chart = new BalanceChartViewModel(history, keysVm);

        chart.Refresh();

        Assert.True(chart.HasData);
        Assert.Single(chart.Series);
        Assert.Equal("", chart.EmptyHint);
    }

    [Fact]
    public void Refresh_无选中Key_HasData为假且给出提示()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-chart0-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var keys = new ApiKeyRepository(db);
        var history = new BalanceHistoryRepository(db);

        var http = new HttpClient(new OkHandler());
        var client = new DeepSeekClient(http);
        var keysVm = new ApiKeysViewModel(keys, new BalanceService(client, keys, history), client, history);
        var chart = new BalanceChartViewModel(history, keysVm);

        chart.Refresh();

        Assert.False(chart.HasData);
        Assert.Empty(chart.Series);
    }
}
