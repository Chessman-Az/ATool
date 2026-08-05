using ATool.Services;
using Xunit;

namespace ATool.Tests;

public class PeakHourTests
{
    [Theory]
    [InlineData("2026-08-05 09:00:00", true)]   // 9:00 整进入高峰
    [InlineData("2026-08-05 08:59:59", false)]  // 8:59 仍是低谷
    [InlineData("2026-08-05 11:59:59", true)]   // 11:59 高峰末
    [InlineData("2026-08-05 12:00:00", false)]  // 12:00 整退出高峰
    [InlineData("2026-08-05 13:59:59", false)]
    [InlineData("2026-08-05 14:00:00", true)]   // 14:00 整进入高峰
    [InlineData("2026-08-05 17:59:59", true)]
    [InlineData("2026-08-05 18:00:00", false)]  // 18:00 整退出高峰
    [InlineData("2026-08-05 00:00:00", false)]
    public void IsPeakHour_边界时刻归属(string dt, bool expected)
    {
        Assert.Equal(expected, PeakHourService.IsPeakHour(DateTime.Parse(dt)));
    }

    [Fact]
    public void CurrentStatus_高峰返回摸鱼红色语义()
    {
        var (text, isPeak) = PeakHourService.CurrentStatus(new DateTime(2026, 8, 5, 10, 0, 0));
        Assert.True(isPeak);
        Assert.Contains("摸鱼", text);

        var (text2, isPeak2) = PeakHourService.CurrentStatus(new DateTime(2026, 8, 5, 20, 0, 0));
        Assert.False(isPeak2);
        Assert.Contains("开干", text2);
    }

    [Fact]
    public void GetPeriods_覆盖全天且高峰两段()
    {
        var periods = PeakHourService.GetPeriods();
        Assert.Equal(5, periods.Count);
        Assert.Equal(2, periods.Count(p => p.IsPeak));
        Assert.All(periods.Where(p => p.IsPeak), p => Assert.Equal(2m, p.Multiplier));
        Assert.All(periods.Where(p => !p.IsPeak), p => Assert.Equal(1m, p.Multiplier));
    }
}
