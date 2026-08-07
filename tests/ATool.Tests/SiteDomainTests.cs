using ATool.Data;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>主域名提取：URL → 注册域展示名（去协议/路径/www）；网站聚合按主域名合并。</summary>
public class SiteDomainTests
{
    [Theory]
    [InlineData("https://www.douyin.com/jingxuan?modal_id=123", "douyin.com")]
    [InlineData("https://github.com/Chessman-Az/ATool", "github.com")]
    [InlineData("https://www.deepseek.com/", "deepseek.com")]
    [InlineData("http://bbs.example.com/thread/1", "bbs.example.com")] // 非 www 子域保留
    [InlineData("https://microsoft.com", "microsoft.com")]
    public void ExtractMainDomain_去协议路径与www前缀(string url, string expected)
        => Assert.Equal(expected, SiteDomain.ExtractMainDomain(url));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("不是url")]
    [InlineData("/relative/path")]
    public void ExtractMainDomain_无效输入_返回null(string? url)
        => Assert.Null(SiteDomain.ExtractMainDomain(url));

    [Fact]
    public void Summarize_同主域名不同页面_合并为一个条目()
    {
        var logs = new[]
        {
            Log("", "browser", "Releases · Chessman-Az/ATool", "https://github.com/Chessman-Az/ATool/releases", 100),
            Log("", "browser", "ATool 仓库主页", "https://github.com/Chessman-Az/ATool", 50),
            Log("", "browser", "抖音精选", "https://www.douyin.com/jingxuan", 30),
        };

        var s = UsageAggregator.Summarize(logs);

        Assert.Equal(180, s.BrowserSeconds);
        Assert.Equal(2, s.BySite.Count);
        Assert.Equal(150, s.BySite.Single(x => x.Name == "github.com").Seconds); // 同域名合并
        Assert.Equal(30, s.BySite.Single(x => x.Name == "douyin.com").Seconds);  // www 归一
    }

    [Fact]
    public void Summarize_无URL_退回标题兜底()
    {
        var logs = new[]
        {
            Log("", "browser", "B站 - Google Chrome", null, 120),
        };

        var s = UsageAggregator.Summarize(logs);

        Assert.Equal("B站", Assert.Single(s.BySite).Name);
    }

    private static UsageLog Log(string proc, string cat, string title, string? url, int sec) => new()
    {
        ProcessName = proc,
        Category = cat,
        WindowTitle = title,
        SiteUrl = url,
        DurationSec = sec,
        StartTime = "2026-08-07 10:00:00",
    };
}
