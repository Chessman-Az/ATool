using ATool.Data;

namespace ATool.Services;

/// <summary>统计范围类型。</summary>
public enum UsageRangeKind { Today, ThisWeek, ThisMonth, CustomDate }

/// <summary>时间大师聚合纯逻辑（无 I/O，可单测）：范围计算 + 记录汇总。</summary>
public static class UsageAggregator
{
    /// <summary>
    /// 计算范围 [Start, End)（左闭右开）：
    /// Today=当日 00:00→now；ThisWeek=周一 00:00→now；ThisMonth=1 号 00:00→now；CustomDate=当日 00:00→次日 00:00。
    /// </summary>
    public static (DateTime Start, DateTime End) RangeOf(UsageRangeKind kind, DateTime now, DateOnly? custom = null)
    {
        switch (kind)
        {
            case UsageRangeKind.ThisWeek:
            {
                // ISO 周序：Monday=0 … Sunday=6
                var monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
                return (monday, now);
            }
            case UsageRangeKind.ThisMonth:
                return (new DateTime(now.Year, now.Month, 1), now);
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
            switch (l.Category)
            {
                case "office": summary.OfficeSeconds += sec; break;
                case "browser":
                    summary.BrowserSeconds += sec;
                    var site = string.IsNullOrWhiteSpace(l.WindowTitle) ? "未知" : l.WindowTitle;
                    sites[site] = sites.GetValueOrDefault(site) + sec;
                    break;
                case "game": summary.GameSeconds += sec; break;
            }
            var proc = string.IsNullOrWhiteSpace(l.ProcessName) ? "未知" : l.ProcessName;
            apps[proc] = apps.GetValueOrDefault(proc) + sec;
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
