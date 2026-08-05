using ATool.Models;

namespace ATool.Services;

/// <summary>提醒日历核心逻辑（纯函数，可单测）：月历网格 + 提醒→当月触发日归属。</summary>
public static class ReminderCalendarService
{
    public sealed record CalendarCell(DateOnly Date, bool IsCurrentMonth);

    /// <summary>
    /// 生成月历网格（周一起始，6 行 × 7 列 = 42 格）。
    /// 前置/尾部空白格的 IsCurrentMonth=false、Date=default。
    /// </summary>
    public static List<CalendarCell> BuildGrid(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var lead = DayIndex(first.DayOfWeek); // 周一起始：周一=0 … 周日=6

        var cells = new List<CalendarCell>(42);
        for (var i = 0; i < lead; i++)
            cells.Add(new CalendarCell(default, false));
        for (var d = 1; d <= daysInMonth; d++)
            cells.Add(new CalendarCell(new DateOnly(year, month, d), true));
        while (cells.Count % 7 != 0)
            cells.Add(new CalendarCell(default, false));
        return cells;
    }

    /// <summary>
    /// 提醒在指定月份的所有触发日：
    /// 单次 → 创建日；每日 → 当月每天；每周几 → 当月匹配的星期。
    /// 不考虑状态与结束条件（日历为直观展示）。
    /// </summary>
    public static HashSet<DateOnly> GetMonthTriggerDates(IEnumerable<Reminder> reminders, int year, int month)
    {
        var result = new HashSet<DateOnly>();
        var from = new DateOnly(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var to = new DateOnly(year, month, daysInMonth);

        foreach (var r in reminders)
        {
            switch (r.RepeatType)
            {
                case RepeatType.Single:
                    if (DateOnly.TryParse(r.CreatedAt.AsSpan(0, 10), out var created) && created.Year == year && created.Month == month)
                        result.Add(created);
                    break;

                case RepeatType.Daily:
                    for (var d = from; d <= to; d = d.AddDays(1))
                        result.Add(d);
                    break;

                case RepeatType.Weekly:
                    List<WeeklyScheduleItem>? items = null;
                    try { items = r.GetWeeklySchedule(); }
                    catch (Exception) { items = null; } // 防御：损坏 JSON 视为无安排，不崩溃
                    if (items is null) break;
                    foreach (var item in items)
                        for (var d = from; d <= to; d = d.AddDays(1))
                            if (DayIndex(d.DayOfWeek) == item.Day)
                                result.Add(d);
                    break;
            }
        }
        return result;
    }

    /// <summary>ISO 周序：周一=0 … 周日=6（与 ReminderScheduler.DayIndex 一致）。</summary>
    private static int DayIndex(DayOfWeek d) => ((int)d + 6) % 7;
}
