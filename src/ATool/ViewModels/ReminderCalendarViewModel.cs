using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ATool.Data;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>提醒日历：按月展示提醒分布（有提醒的日期标点），月份可切换，默认当月；点击日期联动列表。</summary>
public partial class ReminderCalendarViewModel : ObservableObject
{
    private readonly ReminderRepository _repo;

    public ObservableCollection<CalendarDayVm> Days { get; } = [];

    [ObservableProperty]
    private int _year;

    [ObservableProperty]
    private int _month;

    /// <summary>选中日期变化（null=取消筛选）。</summary>
    public event Action<DateOnly?>? SelectedDateChanged;

    public ReminderCalendarViewModel(ReminderRepository repo)
    {
        _repo = repo;
        var now = DateTime.Now;
        _year = now.Year;
        _month = now.Month;
    }

    public string MonthTitle => $"{Year} 年 {Month} 月";

    /// <summary>重建当月日历（标记有提醒的日期）。</summary>
    public void Load()
    {
        var reminders = _repo.GetAll(null);
        var marked = ReminderCalendarService.GetMonthTriggerDates(reminders, Year, Month);
        Days.Clear();
        foreach (var cell in ReminderCalendarService.BuildGrid(Year, Month))
            Days.Add(new CalendarDayVm(cell, marked.Contains(cell.Date), OnDaySelected));
    }

    private void OnDaySelected(CalendarDayVm day)
    {
        // 再次点击已选中日期 → 取消筛选
        var newSelected = day.IsSelected ? (DateOnly?)null : day.Date;
        foreach (var d in Days) d.IsSelected = d == day && newSelected is not null;
        SelectedDateChanged?.Invoke(newSelected);
    }

    [RelayCommand]
    private void PrevMonth()
    {
        if (Month == 1) { Month = 12; Year--; }
        else Month--;
        Load();
    }

    [RelayCommand]
    private void NextMonth()
    {
        if (Month == 12) { Month = 1; Year++; }
        else Month++;
        Load();
    }
}

/// <summary>日历格子（含空白格）。</summary>
public partial class CalendarDayVm : ObservableObject
{
    private readonly Action<CalendarDayVm> _onSelect;

    public DateOnly Date { get; }
    public bool IsCurrentMonth { get; }
    public int DayNumber => IsCurrentMonth ? Date.Day : 0;
    public string DayNumberText => DayNumber == 0 ? "" : DayNumber.ToString();
    public bool HasDate => IsCurrentMonth;

    /// <summary>今天（日历美化：红色高亮）。</summary>
    public bool IsToday => Date == DateOnly.FromDateTime(DateTime.Now);

    [ObservableProperty]
    private bool _isMarked;

    [ObservableProperty]
    private bool _isSelected;

    public CalendarDayVm(ReminderCalendarService.CalendarCell cell, bool isMarked, Action<CalendarDayVm> onSelect)
    {
        Date = cell.Date;
        IsCurrentMonth = cell.IsCurrentMonth;
        _isMarked = isMarked;
        _onSelect = onSelect;
    }

    [RelayCommand]
    private void Select()
    {
        if (!IsCurrentMonth) return;
        _onSelect(this);
    }
}
