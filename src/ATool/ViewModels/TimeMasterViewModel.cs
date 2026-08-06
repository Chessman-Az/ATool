using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using ATool.Data;
using ATool.Models;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>
/// 时间大师页 VM：软件/游戏/网站使用时长统计。
/// 范围单选（今日/本周/本月/指定日期）→ 总览卡（活动总时长 + 办公/浏览器/游戏）+ 应用明细排行 + 浏览器网站明细 + 近7天柱状图。
/// 数据源：usage_log 表（后台 UsageTrackerService 每 5s 采样写入），经 UsageLogRepository.QueryRange 读取。
/// </summary>
public partial class TimeMasterViewModel : ObservableObject
{
    private readonly UsageLogRepository _repo;
    private readonly DispatcherTimer _autoRefresh;

    // ---- 范围单选（RadioButton 双向绑定，照 BalanceChartViewModel bool 单选）----
    [ObservableProperty] private bool _isToday = true;
    [ObservableProperty] private bool _isThisWeek;
    [ObservableProperty] private bool _isThisMonth;
    [ObservableProperty] private bool _isCustom;

    /// <summary>指定日期（Avalonia DatePicker SelectedDate 绑定）。</summary>
    [ObservableProperty] private DateTimeOffset? _customDate = DateTimeOffset.Now;

    // ---- 总览卡 ----
    /// <summary>活动总时长（大数字）。</summary>
    [ObservableProperty] private string _totalText = "0 分钟";
    [ObservableProperty] private string _officeText = "0 分钟";
    [ObservableProperty] private string _browserText = "0 分钟";
    [ObservableProperty] private string _gameText = "0 分钟";

    /// <summary>当前范围标签（今日/本周/本月/指定日期）。</summary>
    [ObservableProperty] private string _periodLabel = "今日";

    // ---- 明细卡 ----
    /// <summary>应用时长排行（按秒降序；名称/时长/占比进度条）。</summary>
    public ObservableCollection<AppUsageItem> ByAppItems { get; } = [];

    /// <summary>浏览器网站时长明细（按窗口标题聚合，按秒降序）。</summary>
    public ObservableCollection<AppUsageItem> BySiteItems { get; } = [];

    [ObservableProperty] private bool _hasAppData;
    [ObservableProperty] private bool _hasSiteData;

    // ---- 近 7 天柱状图 ----
    /// <summary>柱状图序列（赋值时同步通知 HasData）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasData))]
    private ISeries[] _dailyChart = [];

    /// <summary>是否有数据可画（图表可见 / 空提示可见的开关）。</summary>
    public bool HasData => DailyChart.Length > 0;

    public Axis[] XAxes { get; private set; } = [];
    public Axis[] YAxes { get; private set; } = [];

    public TimeMasterViewModel(UsageLogRepository repo)
    {
        _repo = repo;
        BuildAxes();
        Refresh();
        // 每 60s 自动刷新（后台采样写入持续发生，页面常驻时数据实时跟进）
        _autoRefresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _autoRefresh.Tick += (_, _) => Refresh();
        _autoRefresh.Start();
    }

    // ---- 范围切换 ----
    partial void OnIsTodayChanged(bool value) { if (value) SetRange("今日"); }
    partial void OnIsThisWeekChanged(bool value) { if (value) SetRange("本周"); }
    partial void OnIsThisMonthChanged(bool value) { if (value) SetRange("本月"); }
    partial void OnIsCustomChanged(bool value) { if (value) SetRange("指定日期"); }
    partial void OnCustomDateChanged(DateTimeOffset? value) { if (IsCustom) SetRange("指定日期"); }

    private void SetRange(string label)
    {
        PeriodLabel = label;
        Refresh();
    }

    /// <summary>进入页面时由 MainWindowViewModel 调用（NavIndex==3）；范围切换 / 60s 定时器也走这里。</summary>
    public void Refresh()
    {
        var (start, end) = CurrentRange();
        var logs = _repo.QueryRange(start, end);
        ApplySummary(logs);
        RebuildDailyChart();
    }

    private (DateTime Start, DateTime End) CurrentRange()
    {
        var kind = IsThisWeek ? UsageRangeKind.ThisWeek
            : IsThisMonth ? UsageRangeKind.ThisMonth
            : IsCustom ? UsageRangeKind.CustomDate
            : UsageRangeKind.Today;
        var custom = CustomDate is { } d ? DateOnly.FromDateTime(d.Date) : (DateOnly?)null;
        return UsageAggregator.RangeOf(kind, DateTime.Now, custom);
    }

    /// <summary>汇总当前范围：总/办公/浏览器/游戏时长 + 应用排行 + 浏览器网站明细（口径统一走 UsageAggregator）。</summary>
    private void ApplySummary(IEnumerable<UsageLog> logs)
    {
        var s = UsageAggregator.Summarize(logs);

        TotalText = FormatDuration(s.TotalSeconds);
        OfficeText = FormatDuration(s.OfficeSeconds);
        BrowserText = FormatDuration(s.BrowserSeconds);
        GameText = FormatDuration(s.GameSeconds);

        ByAppItems.Clear();
        foreach (var (name, sec) in s.ByApp)
            ByAppItems.Add(new AppUsageItem(name, sec, s.TotalSeconds));
        HasAppData = ByAppItems.Count > 0;

        BySiteItems.Clear();
        foreach (var (name, sec) in s.BySite)
            BySiteItems.Add(new AppUsageItem(name, sec, s.BrowserSeconds));
        HasSiteData = BySiteItems.Count > 0;
    }

    /// <summary>近 7 天每日使用时长柱状图（含今天，无数据日补 0；全部为 0 时置空态）。</summary>
    private void RebuildDailyChart()
    {
        var today = DateTime.Today;
        var start = today.AddDays(-6);
        var logs = _repo.QueryRange(start, today.AddDays(1).AddSeconds(-1));

        var perDay = new Dictionary<DateTime, long>();
        foreach (var l in logs)
        {
            if (ParseTime(l.StartTime) is { } t)
                perDay[t.Date] = perDay.GetValueOrDefault(t.Date) + (long)l.DurationSec;
        }

        var points = new List<DateTimePoint>();
        for (var d = start; d <= today; d = d.AddDays(1))
            points.Add(new DateTimePoint(d, perDay.GetValueOrDefault(d)));

        if (points.All(p => (p.Value ?? 0) <= 0))
        {
            DailyChart = [];
            return;
        }

        var barColor = SKColor.Parse("#38BDF8");
        DailyChart = new ISeries[]
        {
            new ColumnSeries<DateTimePoint>
            {
                Values = points,
                Name = "使用时长",
                Fill = new SolidColorPaint(barColor),
                MaxBarWidth = 18,
            }
        };
    }

    private void BuildAxes()
    {
        // 坐标轴美化：文字浅灰、分隔线深蓝，X 轴显示日期（MM-dd），Y 轴显示时长
        var labelPaint = new SolidColorPaint(SKColor.Parse("#7E93AD"));
        var sepPaint = new SolidColorPaint(SKColor.Parse("#1E3A5C")) { StrokeThickness = 1 };
        XAxes = new[]
        {
            new Axis
            {
                Labeler = v => new DateTime((long)v).ToString("MM-dd"),
                TextSize = 11,
                LabelsPaint = labelPaint,
                SeparatorsPaint = sepPaint,
            }
        };
        YAxes = new[]
        {
            new Axis
            {
                Labeler = v =>
                {
                    var s = (long)v;
                    return s >= 3600 ? $"{s / 3600}h" : s >= 60 ? $"{s / 60}m" : $"{s}s";
                },
                TextSize = 11,
                MinLimit = 0,
                LabelsPaint = labelPaint,
                SeparatorsPaint = sepPaint,
            }
        };
    }

    /// <summary>秒数 → 人类可读时长（秒 / 分钟 / 小时分）。</summary>
    internal static string FormatDuration(long sec)
    {
        if (sec < 60) return $"{sec} 秒";
        var ts = TimeSpan.FromSeconds(sec);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours} 小时 {ts.Minutes} 分"
            : $"{ts.Minutes} 分钟";
    }

    /// <summary>
    /// 解析段开始时间。契约：StartTime 为 'yyyy-MM-dd HH:mm:ss' 字符串（照 QueriedAt 模式），DateTime 亦兼容。
    /// </summary>
    private static DateTime? ParseTime(object? value) => value switch
    {
        DateTime dt => dt,
        string s when DateTime.TryParse(s, out var parsed) => parsed,
        _ => null,
    };
}

/// <summary>应用/网站时长条目：名称 + 秒数 + 时长文本 + 占比（0~1，进度条用）。</summary>
public sealed class AppUsageItem
{
    public string Name { get; }
    public long Seconds { get; }
    public string DurationText { get; }
    public double Ratio { get; }

    public AppUsageItem(string name, long seconds, long totalSeconds)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "未知" : name;
        Seconds = seconds;
        DurationText = TimeMasterViewModel.FormatDuration(seconds);
        Ratio = totalSeconds > 0 ? Math.Round((double)seconds / totalSeconds, 4) : 0;
    }
}
