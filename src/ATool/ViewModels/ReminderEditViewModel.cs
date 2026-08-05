using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ATool.Data;
using ATool.Models;

namespace ATool.ViewModels;

/// <summary>编辑面板 VM：新建/编辑提醒。保存不重置 TriggeredCount（需求：修改重复规则后已触发次数继续累加）。</summary>
public partial class ReminderEditViewModel : ObservableObject
{
    private readonly ReminderRepository _repo;
    private Reminder? _editing;

    public ObservableCollection<WeeklyItemVm> WeeklyItems { get; } = [];

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private string _triggerTime = "09:00:00";

    [ObservableProperty]
    private RepeatType _repeatType = RepeatType.Single;

    [ObservableProperty]
    private EndType _endType = EndType.Never;

    [ObservableProperty]
    private string _endValue = "";

    [ObservableProperty]
    private string? _error;

    /// <summary>面板标题（新建/编辑）。</summary>
    [ObservableProperty]
    private string _editorTitle = "新建提醒";

    /// <summary>保存成功（owner 关闭面板并刷新列表）。</summary>
    public event Action? Saved;

    /// <summary>取消编辑（owner 关闭面板）。</summary>
    public event Action? Cancelled;

    public ReminderEditViewModel(ReminderRepository repo)
    {
        _repo = repo;
        for (var i = 0; i < 7; i++)
            WeeklyItems.Add(new WeeklyItemVm(i));
    }

    public bool IsEditing => _editing is not null;

    // ---- 重复规则单选（RadioButton 双向绑定）----
    [ObservableProperty] private bool _isSingle = true;
    [ObservableProperty] private bool _isDaily;
    [ObservableProperty] private bool _isWeekly;

    partial void OnIsSingleChanged(bool value) { if (value) RepeatType = RepeatType.Single; }
    partial void OnIsDailyChanged(bool value) { if (value) RepeatType = RepeatType.Daily; }
    partial void OnIsWeeklyChanged(bool value) { if (value) RepeatType = RepeatType.Weekly; }

    // ---- 结束条件单选 ----
    [ObservableProperty] private bool _isNever = true;
    [ObservableProperty] private bool _isDate;
    [ObservableProperty] private bool _isTimes;

    partial void OnIsNeverChanged(bool value) { if (value) EndType = EndType.Never; }
    partial void OnIsDateChanged(bool value) { if (value) EndType = EndType.Date; }
    partial void OnIsTimesChanged(bool value) { if (value) EndType = EndType.Times; }

    public void BeginNew()
    {
        _editing = null;
        EditorTitle = "新建提醒";
        Title = "";
        Description = "";
        TriggerTime = "09:00:00";
        RepeatType = RepeatType.Single;
        IsSingle = true; IsDaily = false; IsWeekly = false;
        EndType = EndType.Never;
        IsNever = true; IsDate = false; IsTimes = false;
        EndValue = "";
        Error = null;
        foreach (var w in WeeklyItems) w.IsSelected = false;
    }

    public void BeginEdit(Reminder r)
    {
        _editing = r;
        EditorTitle = "编辑提醒";
        Title = r.Title;
        Description = r.Description;
        TriggerTime = r.TriggerTime;
        RepeatType = r.RepeatType;
        IsSingle = r.RepeatType == RepeatType.Single;
        IsDaily = r.RepeatType == RepeatType.Daily;
        IsWeekly = r.RepeatType == RepeatType.Weekly;
        EndType = r.EndType;
        IsNever = r.EndType == EndType.Never;
        IsDate = r.EndType == EndType.Date;
        IsTimes = r.EndType == EndType.Times;
        EndValue = r.EndValue ?? "";
        Error = null;
        var schedule = r.GetWeeklySchedule();
        foreach (var w in WeeklyItems)
        {
            var hit = schedule.FirstOrDefault(s => s.Day == w.Day);
            w.IsSelected = hit is not null;
            w.Time = hit?.Time ?? "09:00:00";
        }
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Title)) { Error = "标题不能为空"; return false; }
        if (!TimeOnly.TryParse(TriggerTime, out _)) { Error = "触发时间格式应为 HH:mm:ss"; return false; }
        if (RepeatType == RepeatType.Weekly)
        {
            var selected = WeeklyItems.Where(w => w.IsSelected).ToList();
            if (selected.Count == 0) { Error = "请至少选择一天"; return false; }
            foreach (var w in selected)
                if (!TimeOnly.TryParse(w.Time, out _)) { Error = $"星期{w.Label}的时间格式应为 HH:mm:ss"; return false; }
        }
        if (EndType == EndType.Date && !DateOnly.TryParse(EndValue, out _)) { Error = "截止日期格式应为 yyyy-MM-dd"; return false; }
        if (EndType == EndType.Times && (!int.TryParse(EndValue, out var n) || n < 1)) { Error = "触发次数应为正整数"; return false; }
        return true;
    }

    public void Save()
    {
        if (!Validate()) return;
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var scheduleJson = RepeatType == RepeatType.Weekly
            ? JsonSerializer.Serialize(WeeklyItems.Where(w => w.IsSelected)
                .Select(w => new WeeklyScheduleItem { Day = w.Day, Time = w.Time }).ToList())
            : "[]";

        if (_editing is null)
        {
            _repo.Insert(new Reminder
            {
                Title = Title.Trim(),
                Description = Description,
                RepeatType = RepeatType,
                RepeatSchedule = scheduleJson,
                TriggerTime = TriggerTime,
                EndType = EndType,
                EndValue = EndType == EndType.Never ? null : EndValue,
                Status = ReminderStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            _editing.Title = Title.Trim();
            _editing.Description = Description;
            _editing.RepeatType = RepeatType;
            _editing.RepeatSchedule = scheduleJson;
            _editing.TriggerTime = TriggerTime;
            _editing.EndType = EndType;
            _editing.EndValue = EndType == EndType.Never ? null : EndValue;
            _editing.UpdatedAt = now;
            // 不重置 TriggeredCount（需求）
            _repo.Update(_editing);
        }
        Saved?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}

/// <summary>每周几选择项（含独立时间）。Day 0..6 = 周一..周日。</summary>
public partial class WeeklyItemVm : ObservableObject
{
    public int Day { get; }
    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _time = "09:00:00";

    public WeeklyItemVm(int day)
    {
        Day = day;
        Label = day switch
        {
            0 => "周一", 1 => "周二", 2 => "周三", 3 => "周四",
            4 => "周五", 5 => "周六", _ => "周日"
        };
    }
}
