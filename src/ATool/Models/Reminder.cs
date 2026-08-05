using System.Text.Json;
using System.Text.Json.Serialization;

namespace ATool.Models;

public enum RepeatType { Single = 0, Daily = 1, Weekly = 2 }

public enum EndType { Never = 0, Date = 1, Times = 2 }

public enum ReminderStatus { Pending = 0, Done = 1 }

/// <summary>自定义每周几的独立时间项。Day: 0=周一 … 6=周日（ISO 周序）。</summary>
public sealed class WeeklyScheduleItem
{
    public int Day { get; set; }
    public string Time { get; set; } = "09:00:00"; // HH:mm:ss

    [JsonIgnore]
    public TimeOnly TimeOnly => TimeOnly.Parse(Time);
}

/// <summary>提醒实体，字段与 SQLite reminders 表一一对应。</summary>
public sealed class Reminder
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public RepeatType RepeatType { get; set; } = RepeatType.Single;

    /// <summary>RepeatType.Weekly 时的 JSON：[{"day":0..6,"time":"HH:mm:ss"},…]</summary>
    public string RepeatSchedule { get; set; } = "[]";
    public string TriggerTime { get; set; } = "09:00:00"; // HH:mm:ss
    public EndType EndType { get; set; } = EndType.Never;

    /// <summary>EndType.Date: 'yyyy-MM-dd'；EndType.Times: 次数文本。</summary>
    public string? EndValue { get; set; }

    /// <summary>已触发次数——编辑重复规则时不重置。</summary>
    public int TriggeredCount { get; set; }
    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;

    /// <summary>延迟截止时间（'yyyy-MM-dd HH:mm:ss'）。非空时该时刻前的触发点被跳过——本次延迟不影响周期。</summary>
    public string? SnoozeUntil { get; set; }
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";

    [JsonIgnore]
    public TimeOnly TriggerTimeOnly => TimeOnly.Parse(TriggerTime);

    [JsonIgnore]
    public DateOnly? EndDate => EndType == EndType.Date && DateOnly.TryParse(EndValue, out var d) ? d : null;

    [JsonIgnore]
    public int? EndTimes => EndType == EndType.Times && int.TryParse(EndValue, out var n) ? n : null;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public List<WeeklyScheduleItem> GetWeeklySchedule() =>
        JsonSerializer.Deserialize<List<WeeklyScheduleItem>>(RepeatSchedule, JsonOptions) ?? new();
}
