using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>应用分类器：进程名 → 类别（browser/office/game/other）+ 浏览器网站名提取。</summary>
public class AppUsageCategorizerTests
{
    [Theory]
    [InlineData("chrome", AppUsageCategorizer.Browser)]
    [InlineData("msedge", AppUsageCategorizer.Browser)]
    [InlineData("firefox", AppUsageCategorizer.Browser)]
    [InlineData("360chrome", AppUsageCategorizer.Browser)]
    [InlineData("opera", AppUsageCategorizer.Browser)]
    [InlineData("sogouexplorer", AppUsageCategorizer.Browser)]
    [InlineData("qqbrowser", AppUsageCategorizer.Browser)]
    public void Categorize_浏览器进程_返回browser(string proc, string expected)
        => Assert.Equal(expected, AppUsageCategorizer.Categorize(proc));

    [Theory]
    [InlineData("WINWORD", AppUsageCategorizer.Office)]
    [InlineData("EXCEL", AppUsageCategorizer.Office)]
    [InlineData("POWERPNT", AppUsageCategorizer.Office)]
    [InlineData("WPS", AppUsageCategorizer.Office)]
    [InlineData("et", AppUsageCategorizer.Office)]
    [InlineData("wpp", AppUsageCategorizer.Office)]
    [InlineData("wpspdf", AppUsageCategorizer.Office)]
    [InlineData("Code", AppUsageCategorizer.Office)]
    [InlineData("idea", AppUsageCategorizer.Office)]
    [InlineData("devenv", AppUsageCategorizer.Office)]
    [InlineData("notepad", AppUsageCategorizer.Office)]
    [InlineData("WeChat", AppUsageCategorizer.Office)]
    [InlineData("DingTalk", AppUsageCategorizer.Office)]
    [InlineData("QQ", AppUsageCategorizer.Office)]
    public void Categorize_办公进程_返回office(string proc, string expected)
        => Assert.Equal(expected, AppUsageCategorizer.Categorize(proc));

    [Theory]
    [InlineData("steam", AppUsageCategorizer.Game)]
    [InlineData("wegame", AppUsageCategorizer.Game)]
    [InlineData("dota2", AppUsageCategorizer.Game)]
    [InlineData("csgo", AppUsageCategorizer.Game)]
    [InlineData("genshinimpact", AppUsageCategorizer.Game)]
    public void Categorize_游戏进程_返回game(string proc, string expected)
        => Assert.Equal(expected, AppUsageCategorizer.Categorize(proc));

    [Fact]
    public void Categorize_未知进程_返回other()
        => Assert.Equal(AppUsageCategorizer.Other, AppUsageCategorizer.Categorize("totally_unknown_app"));

    [Fact]
    public void Categorize_空或null_返回other()
    {
        Assert.Equal(AppUsageCategorizer.Other, AppUsageCategorizer.Categorize(""));
        Assert.Equal(AppUsageCategorizer.Other, AppUsageCategorizer.Categorize(null));
    }

    [Fact]
    public void Categorize_大小写不敏感()
        => Assert.Equal(AppUsageCategorizer.Browser, AppUsageCategorizer.Categorize("Chrome"));

    // ---- 网站名提取 ----

    [Theory]
    [InlineData("百度 - Google Chrome", "百度")]
    [InlineData("GitHub - Microsoft Edge", "GitHub")]
    [InlineData("B站 - Mozilla Firefox", "B站")]
    [InlineData("知乎 - 360安全浏览器", "知乎")]
    [InlineData("无后缀页面标题", "无后缀页面标题")]
    public void ExtractSiteName_去掉浏览器后缀(string title, string expected)
        => Assert.Equal(expected, AppUsageCategorizer.ExtractSiteName(title, "chrome"));

    [Fact]
    public void ExtractSiteName_空标题_返回空()
        => Assert.Equal("", AppUsageCategorizer.ExtractSiteName("", "chrome"));

    [Fact]
    public void ExtractSiteName_非浏览器进程_返回原标题()
        => Assert.Equal("某文档.docx", AppUsageCategorizer.ExtractSiteName("某文档.docx", "winword"));

    // ---- 标题兜底（进程名解析失败：提权/受保护进程）----

    [Theory]
    [InlineData("百度 - Google Chrome")]
    [InlineData("GitHub - Microsoft Edge")]
    [InlineData("B站 - Mozilla Firefox")]
    [InlineData("知乎 - 360安全浏览器")]
    [InlineData("无标题 - Opera")]
    public void TitleLooksLikeBrowser_浏览器后缀标题_返回true(string title)
        => Assert.True(AppUsageCategorizer.TitleLooksLikeBrowser(title));

    [Theory]
    [InlineData("无后缀页面标题")]
    [InlineData("文档1 - Word")]
    [InlineData("")]
    [InlineData(null)]
    public void TitleLooksLikeBrowser_普通标题_返回false(string? title)
        => Assert.False(AppUsageCategorizer.TitleLooksLikeBrowser(title));

    [Theory]
    [InlineData("百度 - Google Chrome", "Google Chrome")]
    [InlineData("GitHub - Microsoft Edge", "Microsoft Edge")]
    [InlineData("B站 - Mozilla Firefox", "Mozilla Firefox")]
    [InlineData("知乎 - 360安全浏览器", "360安全浏览器")]
    public void ExtractAppName_浏览器标题_返回浏览器名(string title, string expected)
        => Assert.Equal(expected, AppUsageCategorizer.ExtractAppName(title));

    [Theory]
    [InlineData("文档1 - Word", "文档1 - Word")]
    [InlineData("  无标题 - 记事本  ", "无标题 - 记事本")]
    public void ExtractAppName_普通标题_返回原标题(string title, string expected)
        => Assert.Equal(expected, AppUsageCategorizer.ExtractAppName(title));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ExtractAppName_空标题_返回空(string? title)
        => Assert.Equal("", AppUsageCategorizer.ExtractAppName(title));

    [Fact]
    public void ExtractSiteName_进程名为空_标题兜底去后缀()
        => Assert.Equal("百度", AppUsageCategorizer.ExtractSiteName("百度 - Google Chrome", ""));

    [Fact]
    public void ExtractSiteName_进程名为空且非浏览器标题_返回原标题()
        => Assert.Equal("某文档.docx", AppUsageCategorizer.ExtractSiteName("某文档.docx", ""));
}
