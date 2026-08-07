using ATool.Models;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>充值识别与汇总（纯逻辑）：余额增加（delta&gt;0）识别为充值；汇总充值/实际/差值。</summary>
public class RechargeServiceTests
{
    private static BalanceRecord Rec(long id, long keyId, decimal balance, string at, string alias = "k1") => new()
    {
        Id = id, ApiKeyId = keyId, TotalBalance = balance, QueriedAt = at, Alias = alias,
    };

    [Fact]
    public void DetectRecharges_增加记录被识别_减少不计()
    {
        // 10 → 11（+1 充值） → 10.5（-0.5 不计）
        var records = new[]
        {
            Rec(1, 1, 10m, "2026-08-01 10:00:00"),
            Rec(2, 1, 11m, "2026-08-01 11:00:00"),
            Rec(3, 1, 10.5m, "2026-08-01 12:00:00"),
        };

        var recharges = RechargeService.DetectRecharges(records);

        var r = Assert.Single(recharges);
        Assert.Equal(2, r.HistoryId);
        Assert.Equal(1m, r.Delta);
    }

    [Fact]
    public void DetectRecharges_首条无前一条_不计()
    {
        var records = new[] { Rec(1, 1, 10m, "2026-08-01 10:00:00") };
        Assert.Empty(RechargeService.DetectRecharges(records));
    }

    [Fact]
    public void DetectRecharges_按Key分组计算()
    {
        // key1: 10→11（+1）；key2: 20→19（-1 不计）→ 只有 key1 的充值
        var records = new[]
        {
            Rec(1, 1, 10m, "2026-08-01 10:00:00", "k1"),
            Rec(2, 1, 11m, "2026-08-01 11:00:00", "k1"),
            Rec(3, 2, 20m, "2026-08-01 10:00:00", "k2"),
            Rec(4, 2, 19m, "2026-08-01 11:00:00", "k2"),
        };

        var recharges = RechargeService.DetectRecharges(records);

        var r = Assert.Single(recharges);
        Assert.Equal(2, r.HistoryId); // key1 的第二条记录（10→11）
        Assert.Equal("k1", r.Alias);
    }

    [Fact]
    public void Summarize_充值实际佣金与差值()
    {
        var items = new[]
        {
            (Delta: 1m, Actual: 0.5m, Commission: 0.1m),
            (Delta: 2m, Actual: 2m, Commission: 0m),
        };

        var s = RechargeService.Summarize(items);

        Assert.Equal(3m, s.TotalDelta);       // 充值金额合计
        Assert.Equal(2.5m, s.TotalActual);    // 实际充值合计
        Assert.Equal(0.1m, s.TotalCommission); // 佣金合计
        Assert.Equal(0.4m, s.Diff);           // 差值 = 充值 - 实际 - 佣金
    }

    [Fact]
    public void Summarize_空列表_全零()
    {
        var s = RechargeService.Summarize([]);
        Assert.Equal(0m, s.TotalDelta);
        Assert.Equal(0m, s.TotalActual);
        Assert.Equal(0m, s.TotalCommission);
        Assert.Equal(0m, s.Diff);
    }
}
