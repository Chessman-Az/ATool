using ATool.Models;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>余额变动明细行组装（查询余额按钮→明细页）的回归锚点。</summary>
public class HistoryDetailTests
{
    [Fact]
    public void BuildRows_按时间倒序_首条变动为空()
    {
        var records = new List<BalanceRecord>
        {
            new() { ApiKeyId = 1, TotalBalance = 100m, QueriedAt = "2026-08-01 10:00:00" },
            new() { ApiKeyId = 1, TotalBalance = 105m, QueriedAt = "2026-08-02 10:00:00" },
        };
        var rows = BalanceHistoryDetailService.BuildRows(records);
        Assert.Equal(2, rows.Count);
        Assert.Equal("2026-08-02 10:00:00", rows[0].QueriedAt); // 最新在前
        Assert.Equal("+5.00", rows[0].DeltaText);
        Assert.Equal("2026-08-01 10:00:00", rows[1].QueriedAt);
        Assert.Equal("—", rows[1].DeltaText); // 首条无对比
    }

    [Fact]
    public void BuildRows_余额减少_变动为负()
    {
        var records = new List<BalanceRecord>
        {
            new() { TotalBalance = 105m, QueriedAt = "2026-08-01 10:00:00" },
            new() { TotalBalance = 98m, QueriedAt = "2026-08-02 10:00:00" },
        };
        var rows = BalanceHistoryDetailService.BuildRows(records);
        Assert.Equal("-7.00", rows[0].DeltaText);
    }

    [Fact]
    public void BuildRows_空记录_返回空()
    {
        Assert.Empty(BalanceHistoryDetailService.BuildRows([]));
    }
}
