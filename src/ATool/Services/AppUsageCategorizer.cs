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

    /// <summary>Edge 多配置变体后缀："xxx - <配置名> - Microsoft Edge"（配置名如"个人"，不固定）。</summary>
    private static readonly System.Text.RegularExpressions.Regex EdgeProfileSuffixRegex = new(@" - [^-]+ - Microsoft Edge$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>网站名里的多标签页尾缀（" 和另外 3 个页面"）。</summary>
    private static readonly System.Text.RegularExpressions.Regex ExtraPagesSuffix = new(@"\s*和另外\s*\d+\s*个页面$");

    /// <summary>把标题中的零宽/不可见字符替换为普通空格（Edge 标题常含 ZWSP，直接删除会让 "Microsoft Edge" 变 "MicrosoftEdge"）。</summary>
    public static string NormalizeTitle(string? title)
        => title is null ? "" : new string(title.Select(c => c is '\u200b' or '\u200c' or '\u200d' or '\ufeff' or '\u00a0' ? ' ' : c).ToArray());

    /// <summary>
    /// 规范化标题后匹配浏览器后缀；成功时输出浏览器名与网站名部分。
    /// 支持：Edge 多配置变体（"xxx - 个人 - Microsoft Edge"）+ 标准后缀（"B站 - Google Chrome" 等）。
    /// </summary>
    private static bool TryMatchBrowserSuffix(string normalized, out string browserName, out string sitePart)
    {
        // Edge 多配置变体优先匹配，配置名并入后缀，避免残留进网站名
        var m = EdgeProfileSuffixRegex.Match(normalized);
        if (m.Success)
        {
            browserName = "Microsoft Edge";
            sitePart = normalized[..m.Index].Trim();
            return true;
        }
        foreach (var suffix in BrowserTitleSuffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                browserName = suffix.TrimStart('-', ' ');
                sitePart = normalized[..^suffix.Length].Trim();
                return true;
            }
        }
        browserName = "";
        sitePart = "";
        return false;
    }

    /// <summary>按进程名分类。空/未知 → other。</summary>
    public static string Categorize(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return Other;
        if (BrowserProcesses.Contains(processName)) return Browser;
        if (OfficeProcesses.Contains(processName)) return Office;
        if (GameProcesses.Contains(processName)) return Game;
        return Other;
    }

    /// <summary>窗口标题是否带浏览器后缀（进程名解析失败时的浏览器识别兜底，容忍零宽字符与 Edge 多配置）。</summary>
    public static bool TitleLooksLikeBrowser(string? windowTitle)
        => TryMatchBrowserSuffix(NormalizeTitle(windowTitle), out _, out _);

    /// <summary>
    /// 进程名解析失败时的应用名兜底：浏览器标题 → 浏览器名（"B站 - Google Chrome" → "Google Chrome"）；
    /// 其他标题 → 原标题（去首尾空白与零宽字符）。空标题 → 空串（由调用方决定"未知"）。
    /// </summary>
    public static string ExtractAppName(string? windowTitle)
    {
        var normalized = NormalizeTitle(windowTitle);
        if (normalized.Length == 0) return "";
        return TryMatchBrowserSuffix(normalized, out var browserName, out _)
            ? browserName
            : normalized.Trim();
    }

    /// <summary>浏览器窗口标题 → 网站名（去浏览器后缀与「和另外 N 个页面」）；进程名分类为浏览器或标题自身带后缀均识别。</summary>
    public static string ExtractSiteName(string? windowTitle, string? processName)
    {
        var normalized = NormalizeTitle(windowTitle);
        if (normalized.Length == 0) return "";
        if (Categorize(processName) != Browser && !TitleLooksLikeBrowser(windowTitle)) return normalized;
        if (TryMatchBrowserSuffix(normalized, out _, out var site))
            return ExtraPagesSuffix.Replace(site, "").Trim();
        return normalized;
    }
}
