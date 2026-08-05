using ATool.Models;

namespace ATool.Services;

/// <summary>
/// 提醒调度核心纯逻辑（无 I/O，可单测）。
/// 触发模型：扫描器每分钟 Tick，用窗口 (lastTick, now] 内枚举触发点；
/// NextTriggerTime 返回严格大于 after 的下一个触发点（避免同一分钟重复触发）。
/// </summary>
public static class ReminderScheduler
{
    /// <summary>单次：当天时刻；当天已过或已完成后返回 null（无明天）。</summary>
    private static DateTime? SingleNext(Reminder r, DateTime after)
    {
        if (r.Status == ReminderStatus.Done) return null;
        var t = DateOnly.FromDateTime(after).ToDateTime(r.TriggerTimeOnly);
        return t > after ? t : null;
    }

    /// <summary>每日：after 之后（含当天）第一个 trigger_time。</summary>
    private static DateTime DailyNext(Reminder r, DateTime after)
    {
        var t = DateOnly.FromDateTime(after).ToDateTime(r.TriggerTimeOnly);
        return t > after ? t : t.AddDays(1);
    }

    /// <summary>每周几：对 schedule 中每个 (day,time) 求 after 之后最近一次，取最小。</summary>
    private static DateTime? WeeklyNext(Reminder r, DateTime after)
    {
        var items = r.GetWeeklySchedule();
        if (items.Count == 0) return null;
        for (var i = 0; i < 8; i++)
        {
            var day = after.Date.AddDays(i);
            DateTime? best = null;
            foreach (var item in items)
            {
                if (DayIndex(day.DayOfWeek) != item.Day) continue;
                var t = day.Add(item.TimeOnly.ToTimeSpan());
                if (t > after && (best is null || t < best)) best = t;
            }
            if (best is not null) return best;
        }
        return null;
    }

    /// <summary>ISO 周序：Monday=0 … Sunday=6。</summary>
    public static int DayIndex(DayOfWeek d) => ((int)d + 6) % 7;

    /// <summary>
    /// 严格大于 after 的下一个触发点；受结束条件约束（触发次数上限/截止日期）后可能为 null。
    /// </summary>
    public static DateTime? NextTriggerTime(Reminder r, DateTime after)
    {
        if (r.EndType == EndType.Times && r.EndTimes is { } n && r.TriggeredCount >= n) return null;

        var next = r.RepeatType switch
        {
            RepeatType.Single => SingleNext(r, after),
            RepeatType.Daily => DailyNext(r, after),
            RepeatType.Weekly => WeeklyNext(r, after),
            _ => null
        };

        if (next is { } n2 && r.EndType == EndType.Date && r.EndDate is { } ed
            && DateOnly.FromDateTime(n2) > ed) return null;
        return next;
    }

    /// <summary>
    /// 窗口 (fromExclusive, toInclusive] 内全部触发点（休眠补发用；正常分钟 tick 至多 1 个）。
    /// 若设置了 snooze_until，该时刻之前的触发点整体跳过（本次延迟不影响周期）。
    /// </summary>
    public static List<DateTime> EnumerateTriggers(Reminder r, DateTime fromExclusive, DateTime toInclusive)
    {
        var result = new List<DateTime>();
        var cursor = fromExclusive;
        if (r.SnoozeUntil is { } su && DateTime.TryParse(su, out var suTime) && suTime > cursor)
            cursor = suTime; // 跳过延迟区间
        while (true)
        {
            var next = NextTriggerTime(r, cursor);
            if (next is null || next > toInclusive) break;
            result.Add(next.Value);
            cursor = next.Value;
            if (result.Count >= 1000) break; // 安全上限
        }
        return result;
    }
}
