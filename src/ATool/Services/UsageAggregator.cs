using ATool.Data;

namespace ATool.Services;

/// <summary>统计范围类型。</summary>
public enum UsageRangeKind { Today, ThisWeek, ThisMonth, CustomDate }

/// <summary>时间大师聚合纯逻辑（无 I/O，可单测）：范围计算 + 记录汇总。</summary>
public static class UsageAggregator
{
    /// <summary>
    /// 计算范围 [Start, End)（左闭右开）：
    /// Today=当日 00:00→now；ThisWeek=自然周（周一 00:00→下周一 00:00）；ThisMonth=自然月（1 号 00:00→下月 1 号 00:00）；CustomDate=当日整天。
    /// </summary>
    public static (DateTime Start, DateTime End) RangeOf(UsageRangeKind kind, DateTime now, DateOnly? custom = null)
    {
        switch (kind)
        {
            case UsageRangeKind.ThisWeek:
            {
                // ISO 周序：Monday=0 … Sunday=6；自然周结束 = 下周一 00:00（含周日整天）
                var monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
                return (monday, monday.AddDays(7));
            }
            case UsageRangeKind.ThisMonth:
            {
                var first = new DateTime(now.Year, now.Month, 1);
                return (first, first.AddMonths(1));
            }
            case UsageRangeKind.CustomDate when custom is { } d:
                return (d.ToDateTime(TimeOnly.MinValue), d.ToDateTime(TimeOnly.MinValue).AddDays(1));
            default:
                return (now.Date, now);
        }
    }

    /// <summary>汇总记录：总时长 + 分类时长 + 应用排行 + 浏览器网站明细（均按秒降序）。</summary>
    public static UsageSummary Summarize(IEnumerable<UsageLog> logs)
    {
        var summary = new UsageSummary();
        var apps = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var sites = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var l in logs)
        {
            var sec = (long)l.DurationSec;
            if (sec <= 0) continue;
            summary.TotalSeconds += sec;

            // 分类：库中类别优先；标题带浏览器后缀时修正（进程名解析失败写库的历史记录兜底）
            var category = l.Category;
            if (category != AppUsageCategorizer.Browser && AppUsageCategorizer.TitleLooksLikeBrowser(l.WindowTitle))
                category = AppUsageCategorizer.Browser;
            switch (category)
            {
                case "office": summary.OfficeSeconds += sec; break;
                case "browser":
                    summary.BrowserSeconds += sec;
                    // 网站聚合口径：有 URL 按主域名（同域名下所有页面合并）；无 URL（历史数据/采集失败）退回标题
                    var site = SiteDomain.ExtractMainDomain(l.SiteUrl)
                               ?? AppUsageCategorizer.ExtractSiteName(l.WindowTitle, l.ProcessName);
                    if (string.IsNullOrWhiteSpace(site)) site = "未知";
                    sites[site] = sites.GetValueOrDefault(site) + sec;
                    break;
                case "game": summary.GameSeconds += sec; break;
            }

            // 应用名：进程名优先；空时用窗口标题兜底（浏览器 → 浏览器名，其他 → 原标题），避免全部聚合到"未知"。
            // 浏览器记录不进应用排行（网站已在网站明细统计，应用排行只列软件）。
            if (category != AppUsageCategorizer.Browser)
            {
                var proc = string.IsNullOrWhiteSpace(l.ProcessName)
                    ? AppUsageCategorizer.ExtractAppName(l.WindowTitle)
                    : l.ProcessName;
                if (string.IsNullOrWhiteSpace(proc)) proc = "未知";
                apps[proc] = apps.GetValueOrDefault(proc) + sec;
            }
        }

        summary.ByApp = apps.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, kv.Value)).ToList();
        summary.BySite = sites.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, kv.Value)).ToList();
        return summary;
    }
}

/// <summary>聚合结果：总/分类秒数 + 应用/网站 (名称, 秒数) 排行。</summary>
public sealed class UsageSummary
{
    public long TotalSeconds { get; set; }
    public long OfficeSeconds { get; set; }
    public long BrowserSeconds { get; set; }
    public long GameSeconds { get; set; }
    public List<(string Name, long Seconds)> ByApp { get; set; } = [];
    public List<(string Name, long Seconds)> BySite { get; set; } = [];
}
