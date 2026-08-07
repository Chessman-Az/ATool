using ATool.Data;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>时间大师聚合：范围边界（今日/本周/本月/指定日期）+ 汇总口径（总/办公/浏览器/游戏/应用排行/网站明细）。</summary>
public class UsageAggregatorTests
{
    private static UsageLog Log(string proc, string cat, string title, int sec, string? siteUrl = null) => new()
    {
        ProcessName = proc, Category = cat, WindowTitle = title, DurationSec = sec,
        StartTime = "2026-08-06 10:00:00", SiteUrl = siteUrl,
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
        // 应用排行按秒降序；浏览器记录不进应用排行（网站只在网站明细）
        Assert.Equal(new[] { "notepad", "winword", "steam", "unknown" },
            s.ByApp.Select(x => x.Name).ToArray());
        Assert.Equal(190, s.ByApp.Sum(x => x.Seconds));
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

    // ---- 进程名解析失败兜底（提权/受保护进程：ProcessName 为空、Category=other）----

    [Fact]
    public void Summarize_进程名为空_标题兜底应用名且浏览器重判()
    {
        var logs = new[]
        {
            Log("", "other", "B站 - Google Chrome", 120),
            Log("", "other", "无标题 - 记事本", 60),
        };

        var s = UsageAggregator.Summarize(logs);

        // 浏览器：标题后缀兜底重判 → 浏览器时长统计 + 网站明细去后缀
        Assert.Equal(180, s.TotalSeconds);
        Assert.Equal(120, s.BrowserSeconds);
        var site = Assert.Single(s.BySite);
        Assert.Equal("B站", site.Name);
        Assert.Equal(120, site.Seconds);
        // 应用排行：浏览器记录不进排行，只剩非浏览器应用（标题兜底）
        var app = Assert.Single(s.ByApp);
        Assert.Equal("无标题 - 记事本", app.Name);
        Assert.Equal(60, app.Seconds);
    }

    [Fact]
    public void Summarize_Edge多配置零宽标题_网站进明细不进应用排行()
    {
        // 真实环境形态：进程名解析失败 + Edge 标题带配置名与零宽空格
        var logs = new[]
        {
            Log("", "other", "Releases · Chessman-Az/ATool 和另外 1 个页面 - 个人 - Microsoft\u200bEdge", 200),
            Log("", "other", "微信", 80),
        };

        var s = UsageAggregator.Summarize(logs);

        Assert.Equal(200, s.BrowserSeconds); // 标题兜底识别为浏览器
        var site = Assert.Single(s.BySite);
        Assert.Equal("Releases · Chessman-Az/ATool", site.Name); // 去「和另外 N 个页面」与配置后缀
        var app = Assert.Single(s.ByApp);
        Assert.Equal("微信", app.Name); // 网站不进应用排行
        Assert.Equal(80, app.Seconds);
    }

    [Fact]
    public void Summarize_进程名与标题都空_仍显示未知()
    {
        var logs = new[] { Log("", "other", "", 30) };

        var s = UsageAggregator.Summarize(logs);

        Assert.Equal(30, s.TotalSeconds);
        Assert.Equal("未知", Assert.Single(s.ByApp).Name);
        Assert.Empty(s.BySite);
    }

    // ---- 主域名聚合（浏览器 URL 采集后按域名合并）----

    [Fact]
    public void Summarize_有URL按主域名聚合_同域名合并()
    {
        var logs = new[]
        {
            Log("", "browser", "Releases · Chessman-Az/ATool - Microsoft Edge", 100, "https://github.com/Chessman-Az/ATool/releases"),
            Log("", "browser", "Issues · Chessman-Az/ATool - Microsoft Edge", 50, "https://github.com/Chessman-Az/ATool/issues"),
            Log("", "browser", "抖音精选 - Microsoft Edge", 80, "https://www.douyin.com/jingxuan?modal_id=1"),
        };

        var s = UsageAggregator.Summarize(logs);

        // 同域名（github.com）下不同页面合并；www 前缀去除
        Assert.Equal(2, s.BySite.Count);
        Assert.Equal(150, s.BySite.Single(x => x.Name == "github.com").Seconds);
        Assert.Equal(80, s.BySite.Single(x => x.Name == "douyin.com").Seconds);
        Assert.Equal(230, s.BrowserSeconds);
    }

    [Fact]
    public void Summarize_URL无效退回标题聚合()
    {
        var logs = new[]
        {
            Log("", "browser", "B站 - Google Chrome", 60, "not-a-url"),
        };

        var s = UsageAggregator.Summarize(logs);

        Assert.Equal("B站", Assert.Single(s.BySite).Name); // 标题兜底
    }
}
