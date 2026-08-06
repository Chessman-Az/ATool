using ATool.Models;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>
/// 中控台「今日提醒」判定（ReminderScheduler.HasTriggerOnDate）：
/// 提醒是否有触发点落在指定日期（含已过但未完成的）；日期全部注入，测试与运行时刻无关。
/// </summary>
public class TodayReminderTests
{
    private static readonly DateOnly Today = new(2026, 8, 5); // 周三

    private static Reminder Single(string time, ReminderStatus status = ReminderStatus.Pending) => new()
    {
        RepeatType = RepeatType.Single, TriggerTime = time, Status = status
    };

    private static Reminder Daily(string time) => new()
    {
        RepeatType = RepeatType.Daily, TriggerTime = time
    };

    private static Reminder Weekly(string scheduleJson) => new()
    {
        RepeatType = RepeatType.Weekly, RepeatSchedule = scheduleJson
    };

    [Fact]
    public void HasTriggerOnDate_单次今天_为真()
    {
        // 今天 15:00 未过 → 真
        Assert.True(ReminderScheduler.HasTriggerOnDate(Single("15:00:00"), Today));
    }

    [Fact]
    public void HasTriggerOnDate_单次今天已过_为真()
    {
        // 今天 09:00 已过但仍是今天的提醒 → 真（中控台应显示）
        Assert.True(ReminderScheduler.HasTriggerOnDate(Single("09:00:00"), Today));
    }

    [Fact]
    public void HasTriggerOnDate_单次未完成每天有触发点_为真()
    {
        // Single 未完成时每天都会在 TriggerTime 触发（休眠补发漂移语义）→ 今天应显示
        Assert.True(ReminderScheduler.HasTriggerOnDate(Single("09:00:00"), Today));
        Assert.True(ReminderScheduler.HasTriggerOnDate(Single("09:00:00"), Today.AddDays(1)));
    }

    [Fact]
    public void HasTriggerOnDate_单次已完成_为假()
    {
        Assert.False(ReminderScheduler.HasTriggerOnDate(Single("15:00:00", ReminderStatus.Done), Today));
    }

    [Fact]
    public void HasTriggerOnDate_每日_为真()
    {
        Assert.True(ReminderScheduler.HasTriggerOnDate(Daily("09:30:00"), Today));
        Assert.True(ReminderScheduler.HasTriggerOnDate(Daily("23:59:00"), Today));
    }

    [Fact]
    public void HasTriggerOnDate_每周今天含该天_为真()
    {
        // 2026-08-05 是周三（day=2）
        Assert.True(ReminderScheduler.HasTriggerOnDate(Weekly("""[{"day":2,"time":"15:00:00"}]"""), Today));
    }

    [Fact]
    public void HasTriggerOnDate_每周今天不含该天_为假()
    {
        // 只有周一（day=0）→ 今天无触发点
        Assert.False(ReminderScheduler.HasTriggerOnDate(Weekly("""[{"day":0,"time":"15:00:00"}]"""), Today));
    }
}
