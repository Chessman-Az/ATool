using ATool.Models;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>
/// 中控台「今日提醒」判定（ReminderScheduler.TriggersToday）：
/// 下一次触发点严格晚于 now 且落在 now 当天；时间全部注入，测试与运行时刻无关。
/// </summary>
public class TodayReminderTests
{
    private static readonly DateTime Now = new(2026, 8, 5, 12, 0, 0); // 周三 12:00

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
    public void TriggersToday_单次今天未过_为真()
    {
        Assert.True(ReminderScheduler.TriggersToday(Single("15:00:00"), Now));
    }

    [Fact]
    public void TriggersToday_单次今天已过_为假()
    {
        Assert.False(ReminderScheduler.TriggersToday(Single("09:00:00"), Now));
    }

    [Fact]
    public void TriggersToday_单次已完成_为假()
    {
        Assert.False(ReminderScheduler.TriggersToday(Single("15:00:00", ReminderStatus.Done), Now));
    }

    [Fact]
    public void TriggersToday_每日今天未过_为真()
    {
        Assert.True(ReminderScheduler.TriggersToday(Daily("15:00:00"), Now));
    }

    [Fact]
    public void TriggersToday_每日今天已过_为假()
    {
        // 今天 09:00 已过 → 下次是明天 → 不在今天
        Assert.False(ReminderScheduler.TriggersToday(Daily("09:00:00"), Now));
    }

    [Fact]
    public void TriggersToday_每周今天含该天未过_为真()
    {
        // 2026-08-05 是周三（day=2）；15:00 未过
        Assert.True(ReminderScheduler.TriggersToday(Weekly("""[{"day":2,"time":"15:00:00"}]"""), Now));
    }

    [Fact]
    public void TriggersToday_每周今天不含该天_为假()
    {
        // 只有周一（day=0）→ 下次是下周一
        Assert.False(ReminderScheduler.TriggersToday(Weekly("""[{"day":0,"time":"15:00:00"}]"""), Now));
    }

    [Fact]
    public void TriggersToday_每周今天含该天但已过_为假()
    {
        Assert.False(ReminderScheduler.TriggersToday(Weekly("""[{"day":2,"time":"09:00:00"}]"""), Now));
    }
}
