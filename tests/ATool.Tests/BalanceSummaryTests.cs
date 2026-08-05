using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>全部余额汇总（开干下方合计行）的回归锚点。</summary>
public class BalanceSummaryTests
{
    [Fact]
    public void Sum_正常合计()
    {
        Assert.Equal(210m, BalanceSummaryService.Sum([100m, 110m, null]));
    }

    [Fact]
    public void Sum_全部为空_返回0()
    {
        Assert.Equal(0m, BalanceSummaryService.Sum([null, null]));
    }

    [Fact]
    public void Sum_空列表_返回0()
    {
        Assert.Equal(0m, BalanceSummaryService.Sum([]));
    }
}
