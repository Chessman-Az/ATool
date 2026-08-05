using ATool.Models;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

public class SchedulerTests
{
    private static Reminder Single(string time, ReminderStatus status = ReminderStatus.Pending, int count = 0) => new()
    {
        RepeatType = RepeatType.Single, TriggerTime = time, Status = status, TriggeredCount = count
    };

    private static Reminder Daily(string time, EndType endType = EndType.Never, string? endValue = null, int count = 0) => new()
    {
        RepeatType = RepeatType.Daily, TriggerTime = time, EndType = endType, EndValue = endValue, TriggeredCount = count
    };

    private static Reminder Weekly(string scheduleJson, int count = 0) => new()
    {
        RepeatType = RepeatType.Weekly, RepeatSchedule = scheduleJson, TriggeredCount = count
    };

    // ---- 单次 ----

    [Fact]
    public void Single_TimeInFuture_ReturnsTodayAtTriggerTime()
    {
        var r = Single("10:00:00");
        var after = new DateTime(2026, 8, 5, 9, 0, 0);
        Assert.Equal(new DateTime(2026, 8, 5, 10, 0, 0), ReminderScheduler.NextTriggerTime(r, after));
    }

    [Fact]
    public void Single_TimePassedToday_ReturnsNull()
    {
        var r = Single("10:00:00");
        var after = new DateTime(2026, 8, 5, 11, 0, 0);
        Assert.Null(ReminderScheduler.NextTriggerTime(r, after));
    }

    [Fact]
    public void Single_ExactBoundary_StrictGreater_ReturnsNull()
    {
        var r = Single("10:00:00");
        var after = new DateTime(2026, 8, 5, 10, 0, 0);
        Assert.Null(ReminderScheduler.NextTriggerTime(r, after)); // after 恰好等于触发点 → 已过
    }

    [Fact]
    public void Single_AlreadyDone_ReturnsNull()
    {
        var r = Single("10:00:00", ReminderStatus.Done);
        Assert.Null(ReminderScheduler.NextTriggerTime(r, new DateTime(2026, 8, 5, 0, 0, 0)));
    }

    // ---- 每日 ----

    [Fact]
    public void Daily_BeforeTrigger_ReturnsToday()
    {
        var r = Daily("09:30:00");
        var after = new DateTime(2026, 8, 5, 8, 0, 0);
        Assert.Equal(new DateTime(2026, 8, 5, 9, 30, 0), ReminderScheduler.NextTriggerTime(r, after));
    }

    [Fact]
    public void Daily_AfterTrigger_ReturnsTomorrow()
    {
        var r = Daily("09:30:00");
        var after = new DateTime(2026, 8, 5, 9, 30, 30);
        Assert.Equal(new DateTime(2026, 8, 6, 9, 30, 0), ReminderScheduler.NextTriggerTime(r, after));
    }

    [Fact]
    public void Daily_ExactTriggerTime_ReturnsTomorrow()
    {
        var r = Daily("09:30:00");
        var after = new DateTime(2026, 8, 5, 9, 30, 0);
        Assert.Equal(new DateTime(2026, 8, 6, 9, 30, 0), ReminderScheduler.NextTriggerTime(r, after));
    }

    [Fact]
    public void Daily_EndDateReached_ReturnsNull()
    {
        var r = Daily("09:30:00", EndType.Date, "2026-08-05");
        var after = new DateTime(2026, 8, 5, 8, 0, 0);
        // 下次触发 2026-08-05 不晚于截止日 → 仍返回
        Assert.Equal(new DateTime(2026, 8, 5, 9, 30, 0), ReminderScheduler.NextTriggerTime(r, after));
    }

    [Fact]
    public void Daily_EndDatePassed_ReturnsNull()
    {
        var r = Daily("09:30:00", EndType.Date, "2026-08-04");
        var after = new DateTime(2026, 8, 5, 8, 0, 0);
        Assert.Null(ReminderScheduler.NextTriggerTime(r, after));
    }

    [Fact]
    public void Daily_TimesLimitReached_ReturnsNull()
    {
        var r = Daily("09:30:00", EndType.Times, "3", count: 3);
        Assert.Null(ReminderScheduler.NextTriggerTime(r, new DateTime(2026, 8, 5, 8, 0, 0)));
    }

    [Fact]
    public void Daily_TimesLimitNotReached_StillTriggers()
    {
        var r = Daily("09:30:00", EndType.Times, "3", count: 2);
        Assert.NotNull(ReminderScheduler.NextTriggerTime(r, new DateTime(2026, 8, 5, 8, 0, 0)));
    }

    // ---- 每周几（ISO: 0=周一 … 6=周日）----

    [Fact]
    public void Weekly_MultipleDays_TakesNearest()
    {
        // 周一 09:00 与 周三 10:00；after=周一 08:00 → 周一 09:00
        var r = Weekly("""[{"day":0,"time":"09:00:00"},{"day":2,"time":"10:00:00"}]""");
        var after = new DateTime(2026, 8, 3, 8, 0, 0); // 2026-08-03 是周一
        Assert.Equal(new DateTime(2026, 8, 3, 9, 0, 0), ReminderScheduler.NextTriggerTime(r, after));
    }

    [Fact]
    public void Weekly_AfterMondayTrigger_NextIsWednesday()
    {
        var r = Weekly("""[{"day":0,"time":"09:00:00"},{"day":2,"time":"10:00:00"}]""");
        var after = new DateTime(2026, 8, 3, 9, 30, 0); // 周一 09:00 已过
        Assert.Equal(new DateTime(2026, 8, 5, 10, 0, 0), ReminderScheduler.NextTriggerTime(r, after));
    }

    [Fact]
    public void Weekly_AfterAllThisWeek_NextIsNextWeek()
    {
        var r = Weekly("""[{"day":0,"time":"09:00:00"}]""");
        var after = new DateTime(2026, 8, 9, 10, 0, 0); // 周日 → 下周一
        Assert.Equal(new DateTime(2026, 8, 10, 9, 0, 0), ReminderScheduler.NextTriggerTime(r, after));
    }

    [Fact]
    public void Weekly_SameDayMultipleTimes_ReturnsFirstAfter()
    {
        var r = Weekly("""[{"day":0,"time":"09:00:00"},{"day":0,"time":"18:00:00"}]""");
        var after = new DateTime(2026, 8, 3, 12, 0, 0); // 周一中午 → 周一 18:00
        Assert.Equal(new DateTime(2026, 8, 3, 18, 0, 0), ReminderScheduler.NextTriggerTime(r, after));
    }

    [Fact]
    public void Weekly_EmptySchedule_ReturnsNull()
    {
        var r = Weekly("[]");
        Assert.Null(ReminderScheduler.NextTriggerTime(r, new DateTime(2026, 8, 3, 8, 0, 0)));
    }

    // ---- 补发窗口枚举 ----

    [Fact]
    public void Enumerate_DailyThreeMissed_ReturnsThreePoints()
    {
        var r = Daily("09:00:00");
        var from = new DateTime(2026, 8, 1, 8, 0, 0); // 周六 08:00（上次 tick）
        var to = new DateTime(2026, 8, 4, 8, 0, 0);   // 周二 08:00（唤醒）
        var points = ReminderScheduler.EnumerateTriggers(r, from, to);
        // 窗口 (8/1 08:00, 8/4 08:00] 内错过：8/1、8/2、8/3 三次；8/4 09:00 未到不算
        Assert.Equal(3, points.Count);
        Assert.Equal(new DateTime(2026, 8, 1, 9, 0, 0), points[0]);
        Assert.Equal(new DateTime(2026, 8, 2, 9, 0, 0), points[1]);
        Assert.Equal(new DateTime(2026, 8, 3, 9, 0, 0), points[2]);
    }

    [Fact]
    public void Enumerate_SingleMissed_ReturnsOnePoint()
    {
        var r = Single("09:00:00");
        var from = new DateTime(2026, 8, 5, 8, 0, 0);
        var to = new DateTime(2026, 8, 5, 10, 0, 0);
        var points = ReminderScheduler.EnumerateTriggers(r, from, to);
        Assert.Single(points);
        Assert.Equal(new DateTime(2026, 8, 5, 9, 0, 0), points[0]);
    }

    [Fact]
    public void Enumerate_SingleTriggered_ThenStops()
    {
        // 单次触发后 Status=Done → 枚举不再返回
        var r = Single("09:00:00", ReminderStatus.Done);
        var points = ReminderScheduler.EnumerateTriggers(r, new DateTime(2026, 8, 5, 8, 0, 0), new DateTime(2026, 8, 5, 12, 0, 0));
        Assert.Empty(points);
    }
}
