using ATool.Data;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>设置服务：快捷启动工具列表（中控台）。</summary>
public class QuickToolsSettingsTests
{
    private static (SettingsService settings, Db db) NewSettings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-quicktools-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        return (new SettingsService(new SettingsRepository(db), db), db);
    }

    [Fact]
    public void GetQuickTools_未配置_默认全部工具()
    {
        var (settings, _) = NewSettings();

        var tools = settings.GetQuickTools();

        Assert.Equal(ToolCatalog.All.Length, tools.Count); // 全部机哥工具
        Assert.Contains("Everything.exe", tools);
    }

    [Fact]
    public void SetQuickTools_保存后按选择返回()
    {
        var (settings, _) = NewSettings();

        settings.SetQuickTools(new[] { "Everything.exe", "geek.exe" });

        var tools = settings.GetQuickTools();
        Assert.Equal(new[] { "Everything.exe", "geek.exe" }, tools);
    }

    [Fact]
    public void GetQuickTools_无效条目被过滤()
    {
        var (settings, _) = NewSettings();

        settings.SetQuickTools(new[] { "Everything.exe", "not-a-real-tool.exe" });

        Assert.Equal(new[] { "Everything.exe" }, settings.GetQuickTools());
    }
}
