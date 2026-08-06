using ATool.Data;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>时间大师聚合：范围边界（今日/本周/本月/指定日期）+ 汇总口径（总/办公/浏览器/游戏/应用排行/网站明细）。</summary>
public class UsageAggregatorTests
{
    private static UsageLog Log(string proc, string cat, string title, int sec) => new()
    {
        ProcessName = proc, Category = cat, WindowTitle = title, DurationSec = sec,
        StartTime = "2026-08-06 10:00:00",
    };

    // ---- 范围边界 ----

    [Fact]
    public void RangeOf_今日_从零点到当前()
    {
        var now = new DateTime(2026, 8, 6, 15, 30, 0);
        var (s, e) = UsageAggregator.RangeOf(UsageRangeKind.Today, now);
        Assert.Equal(new DateTime(2026, 8, 6, 0, 0, 0), s);
        Assert.Equal(now, e);
    }

    [Fact]
    public void RangeOf_本周_自然周到下周一零点()
    {
        // 2026-08-06 是周四；周一 = 08-03；自然周结束 = 下周一 00:00（左闭右开含周日）
        var now = new DateTime(2026, 8, 6, 15, 30, 0);
        var (s, e) = UsageAggregator.RangeOf(UsageRangeKind.ThisWeek, now);
        Assert.Equal(new DateTime(2026, 8, 3, 0, 0, 0), s);
        Assert.Equal(new DateTime(2026, 8, 10, 0, 0, 0), e);
    }

    [Fact]
    public void RangeOf_周日仍算本周()
    {
        var now = new DateTime(2026, 8, 9, 10, 0, 0); // 周日
        var (s, _) = UsageAggregator.RangeOf(UsageRangeKind.ThisWeek, now);
        Assert.Equal(new DateTime(2026, 8, 3, 0, 0, 0), s);
    }

    [Fact]
    public void RangeOf_本月_自然月到下月一号零点()
    {
        var now = new DateTime(2026, 8, 6, 15, 30, 0);
        var (s, e) = UsageAggregator.RangeOf(UsageRangeKind.ThisMonth, now);
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0), s);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0), e);
    }

    [Fact]
    public void RangeOf_指定日期_整天左闭右开()
    {
        var (s, e) = UsageAggregator.RangeOf(UsageRangeKind.CustomDate, DateTime.Now, new DateOnly(2026, 8, 5));
        Assert.Equal(new DateTime(2026, 8, 5, 0, 0, 0), s);
        Assert.Equal(new DateTime(2026, 8, 6, 0, 0, 0), e); // 左闭右开：次日零点，不丢 23:59:59 记录
    }

    // ---- 汇总口径 ----

    [Fact]
    public void Summarize_按类别汇总并降序排行()
    {
        var logs = new[]
        {
            Log("chrome", "browser", "B站 - Google Chrome", 120),
            Log("winword", "office", "文档1 - Word", 60),
            Log("steam", "game", "Steam", 30),
            Log("notepad", "office", "无标题 - 记事本", 90),
            Log("unknown", "other", "SomeApp", 10),
        };

        var s = UsageAggregator.Summarize(logs);

        Assert.Equal(310, s.TotalSeconds);
        Assert.Equal(150, s.OfficeSeconds);
        Assert.Equal(120, s.BrowserSeconds);
        Assert.Equal(30, s.GameSeconds);
        // 应用排行按秒降序（120/90/60/30/10）
        Assert.Equal(new[] { "chrome", "notepad", "winword", "steam", "unknown" },
            s.ByApp.Select(x => x.Name).ToArray());
        Assert.Equal(310, s.ByApp.Sum(x => x.Seconds));
    }

    [Fact]
    public void Summarize_浏览器按标题聚合()
    {
        var logs = new[]
        {
            Log("chrome", "browser", "B站 - Google Chrome", 100),
            Log("chrome", "browser", "B站 - Google Chrome", 50),
            Log("msedge", "browser", "GitHub - Microsoft Edge", 30),
        };

        var s = UsageAggregator.Summarize(logs);

        var site = Assert.Single(s.BySite.Where(x => x.Name.Contains("B站")));
        Assert.Equal(150, site.Seconds);
        Assert.Equal(30, s.BySite.Single(x => x.Name.Contains("GitHub")).Seconds);
    }

    [Fact]
    public void Summarize_空记录_全零()
    {
        var s = UsageAggregator.Summarize([]);
        Assert.Equal(0, s.TotalSeconds);
        Assert.Empty(s.ByApp);
        Assert.Empty(s.BySite);
    }
}
