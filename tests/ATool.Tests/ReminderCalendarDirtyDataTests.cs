using ATool.Models;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>日历对脏数据的防御性（切月闪退根因回归锚点）。</summary>
public class ReminderCalendarDirtyDataTests
{
    [Fact]
    public void GetMonthTriggerDates_损坏的每周JSON_不崩溃返回空()
    {
        var r = new Reminder { RepeatType = RepeatType.Weekly, RepeatSchedule = "not-json" };
        var dates = ReminderCalendarService.GetMonthTriggerDates(new[] { r }, 2026, 8);
        Assert.Empty(dates);
    }

    [Fact]
    public void GetMonthTriggerDates_损坏JSON与正常提醒混合_不崩溃()
    {
        var bad = new Reminder { RepeatType = RepeatType.Weekly, RepeatSchedule = "{{{" };
        var good = new Reminder { RepeatType = RepeatType.Daily, TriggerTime = "09:00:00" };
        var dates = ReminderCalendarService.GetMonthTriggerDates(new[] { bad, good }, 2026, 8);
        Assert.Equal(31, dates.Count); // 每日提醒正常标记，坏数据被跳过
    }

    [Fact]
    public void GetMonthTriggerDates_创建时间格式异常_不崩溃()
    {
        var r = new Reminder { RepeatType = RepeatType.Single, CreatedAt = "not-a-date" };
        var dates = ReminderCalendarService.GetMonthTriggerDates(new[] { r }, 2026, 8);
        Assert.Empty(dates);
    }
}
