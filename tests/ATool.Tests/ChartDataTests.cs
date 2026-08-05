using ATool.Models;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

public class ChartDataTests
{
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
}
