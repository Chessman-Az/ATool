using ATool.Models;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>提醒日历：月历网格生成 + 提醒→当月触发日归属（纯函数）。</summary>
public class ReminderCalendarTests
{
    // ---- 月历网格 ----

    [Fact]
    public void BuildGrid_2026年8月_前置空白5格_共42格()
    {
        // 2026-08-01 是周六（周一起始索引 5）
        var cells = ReminderCalendarService.BuildGrid(2026, 8);
        Assert.Equal(42, cells.Count); // 6 行 × 7 列
        Assert.False(cells[0].IsCurrentMonth); // 前置空白
        Assert.False(cells[4].IsCurrentMonth);
        Assert.True(cells[5].IsCurrentMonth);  // 第 6 格 = 8/1
        Assert.Equal(new DateOnly(2026, 8, 1), cells[5].Date);
        Assert.Equal(new DateOnly(2026, 8, 31), cells[5 + 30].Date);
        Assert.False(cells[^1].IsCurrentMonth); // 尾部补白
    }

    [Fact]
    public void BuildGrid_2026年2月_28天()
    {
        var cells = ReminderCalendarService.BuildGrid(2026, 2);
        // 2026-02-01 是周日（索引 6）→ 前置 6 空白
        Assert.False(cells[5].IsCurrentMonth);
        Assert.True(cells[6].IsCurrentMonth);
        Assert.Equal(new DateOnly(2026, 2, 1), cells[6].Date);
        Assert.Equal(new DateOnly(2026, 2, 28), cells[6 + 27].Date);
        Assert.True(cells.Count % 7 == 0);
    }

    // ---- 提醒 → 当月触发日 ----

    [Fact]
    public void GetTriggerDates_每日_覆盖当月每一天()
    {
        var r = new Reminder { RepeatType = RepeatType.Daily, TriggerTime = "09:00:00" };
        var dates = ReminderCalendarService.GetMonthTriggerDates([r], 2026, 8);
        Assert.Equal(31, dates.Count); // 8 月 31 天
        Assert.Contains(new DateOnly(2026, 8, 1), dates);
        Assert.Contains(new DateOnly(2026, 8, 31), dates);
    }

    [Fact]
    public void GetTriggerDates_每周一三_只含匹配星期()
    {
        var r = new Reminder
        {
            RepeatType = RepeatType.Weekly,
            RepeatSchedule = """[{"day":0,"time":"09:00:00"},{"day":2,"time":"10:00:00"}]""", // 周一、周三
        };
        var dates = ReminderCalendarService.GetMonthTriggerDates([r], 2026, 8);
        // 2026-08 的周一：3,10,17,24,31；周三：5,12,19,26
        Assert.Equal(9, dates.Count);
        Assert.Contains(new DateOnly(2026, 8, 3), dates);
        Assert.Contains(new DateOnly(2026, 8, 5), dates);
        Assert.Contains(new DateOnly(2026, 8, 31), dates);
        Assert.DoesNotContain(new DateOnly(2026, 8, 4), dates); // 周二无
    }

    [Fact]
    public void GetTriggerDates_单次_按创建日标记()
    {
        var r = new Reminder
        {
            RepeatType = RepeatType.Single,
            TriggerTime = "15:30:00",
            CreatedAt = "2026-08-15 10:00:00",
        };
        var dates = ReminderCalendarService.GetMonthTriggerDates([r], 2026, 8);
        Assert.Equal(new DateOnly(2026, 8, 15), Assert.Single(dates));
        Assert.Empty(ReminderCalendarService.GetMonthTriggerDates([r], 2026, 9)); // 其他月无
    }

    [Fact]
    public void GetMonthTriggerDates_多条提醒_日期合并()
    {
        var daily = new Reminder { RepeatType = RepeatType.Daily, TriggerTime = "09:00:00" };
        var single = new Reminder
        {
            RepeatType = RepeatType.Single,
            TriggerTime = "10:00:00",
            CreatedAt = "2026-08-03 12:00:00",
        };
        var dates = ReminderCalendarService.GetMonthTriggerDates([daily, single], 2026, 8);
        Assert.Equal(31, dates.Count); // 每日已覆盖全部，单次不增
        Assert.Contains(new DateOnly(2026, 8, 3), dates);
    }
}
