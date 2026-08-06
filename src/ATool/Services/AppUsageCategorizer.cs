using System;
using System.Collections.Generic;
using System.Linq;

namespace ATool.Services;

/// <summary>
/// 前台窗口分类器（纯静态，可单测）：进程名 → browser/office/game/other；
/// 浏览器窗口标题 → 网站名（去 " - Google Chrome" 等后缀）。
/// </summary>
public static class AppUsageCategorizer
{
    public const string Browser = "browser";
    public const string Office = "office";
    public const string Game = "game";
    public const string Other = "other";

    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "360chrome", "opera", "sogouexplorer", "qqbrowser", "iexplore"
    };

    private static readonly HashSet<string> OfficeProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "winword", "excel", "powerpnt", "wps", "et", "wpp", "wpspdf",
        "code", "idea", "devenv", "notepad", "obsidian", "typora",
        "wechat", "weixin", "dingtalk", "qq", "tim"
    };

    private static readonly HashSet<string> GameProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam", "wegame", "wegamelauncher", "league of legends", "dota2", "csgo",
        "genshinimpact", "battle.net", "epicgameslauncher", "yuahelper"
    };

    private static readonly string[] BrowserTitleSuffixes =
    {
        " - Google Chrome", " - Microsoft Edge", " - Mozilla Firefox", " - 360安全浏览器",
        " - Opera", " - QQ浏览器", " - Internet Explorer"
    };

    /// <summary>按进程名分类。空/未知 → other。</summary>
    public static string Categorize(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return Other;
        if (BrowserProcesses.Contains(processName)) return Browser;
        if (OfficeProcesses.Contains(processName)) return Office;
        if (GameProcesses.Contains(processName)) return Game;
        return Other;
    }

    /// <summary>浏览器窗口标题 → 网站名（去掉浏览器后缀）；非浏览器或识别不到返回原标题。</summary>
    public static string ExtractSiteName(string? windowTitle, string? processName)
    {
        if (string.IsNullOrWhiteSpace(windowTitle)) return "";
        if (Categorize(processName) != Browser) return windowTitle;
        foreach (var suffix in BrowserTitleSuffixes)
        {
            if (windowTitle.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return windowTitle[..^suffix.Length].Trim();
        }
        return windowTitle;
    }
}
