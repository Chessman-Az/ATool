using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ATool.Data;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>
/// 主窗口 VM：页面导航（提醒/Key/设置）、托盘命令（显隐/刷新/退出）、峰谷主按钮状态。
/// 窗口操作（Show/Hide/关闭确认）经事件交给 MainWindow 处理。
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly BalanceService _balance;
    private readonly UsageLogRepository _usageLogRepo;
    private readonly SettingsService _settings;

    public ReminderListViewModel Reminders { get; }
    public ApiKeysViewModel ApiKeys { get; }
    public SettingsViewModel Settings { get; }
    public BalanceChartViewModel Chart { get; }
    public PeakHourViewModel PeakHour { get; } = new();
    public ReminderCalendarViewModel Calendar { get; }
    public BalanceDetailViewModel BalanceDetail { get; }
    public TimeMasterViewModel TimeMaster { get; }
    public JiGeViewModel JiGe { get; }

    // ---- 中控台：今日使用时长 ----
    [ObservableProperty] private string _todayTotalText = "0 分钟";
    [ObservableProperty] private string _todayOfficeText = "0 分钟";
    [ObservableProperty] private string _todayBrowserText = "0 分钟";
    [ObservableProperty] private string _todayGameText = "0 分钟";

    /// <summary>中控台快捷启动工具（机哥工具的子集，点击即启动）。</summary>
    public System.Collections.ObjectModel.ObservableCollection<ToolItemVm> QuickTools { get; } = [];

    public event Action? ShowWindowRequested;
    public event Action? QuitRequested;

    /// <summary>左侧导航选中项：0=中控台 1=提醒事项 2=DeepSeek 余额 3=时间大师 4=机哥 5=系统设置 6=A工具。</summary>
    [ObservableProperty]
    private int _navIndex;

    private DateTime _lastBalanceRefresh = DateTime.MinValue;

    /// <summary>切到余额页时自动刷新一次（节流 60s），保证「全部余额」实时；切到时间大师页刷新统计。</summary>
    partial void OnNavIndexChanged(int value)
    {
        if (value == 0)
            RefreshTodayUsage(); // 进入中控台刷新今日时长
        if (value == 2 && DateTime.Now - _lastBalanceRefresh > TimeSpan.FromSeconds(60))
        {
            _lastBalanceRefresh = DateTime.Now;
            _ = RefreshBalance();
        }
        if (value == 3)
            TimeMaster.Refresh();
    }

    [ObservableProperty]
    private bool _isPeakHour;

    [ObservableProperty]
    private string _peakStatusText = "";

    public MainWindowViewModel(BalanceService balance, ReminderListViewModel reminders, ApiKeysViewModel apiKeys, SettingsViewModel settings, BalanceChartViewModel chart, ReminderCalendarViewModel calendar, BalanceDetailViewModel balanceDetail, TimeMasterViewModel timeMaster, JiGeViewModel jiGe, UsageLogRepository usageLogRepo, SettingsService settingsService)
    {
        _balance = balance;
        Reminders = reminders;
        ApiKeys = apiKeys;
        Settings = settings;
        Chart = chart;
        Calendar = calendar;
        BalanceDetail = balanceDetail;
        TimeMaster = timeMaster;
        JiGe = jiGe;
        _usageLogRepo = usageLogRepo;
        _settings = settingsService;
        RefreshPeakStatus();
        RefreshTodayUsage();
        RefreshQuickTools();
        _balance.StateChanged += () => OnPropertyChanged(nameof(IsRefreshing));
    }

    /// <summary>中控台今日使用时长（总/办公/浏览器/游戏，口径与时间大师一致）。</summary>
    public void RefreshTodayUsage()
    {
        var (start, end) = UsageAggregator.RangeOf(UsageRangeKind.Today, DateTime.Now);
        var s = UsageAggregator.Summarize(_usageLogRepo.QueryRange(start, end));
        TodayTotalText = TimeMasterViewModel.FormatDuration(s.TotalSeconds);
        TodayOfficeText = TimeMasterViewModel.FormatDuration(s.OfficeSeconds);
        TodayBrowserText = TimeMasterViewModel.FormatDuration(s.BrowserSeconds);
        TodayGameText = TimeMasterViewModel.FormatDuration(s.GameSeconds);
    }

    /// <summary>按设置刷新中控台快捷启动工具列表。</summary>
    public void RefreshQuickTools()
    {
        var enabled = _settings.GetQuickTools();
        QuickTools.Clear();
        foreach (var t in ToolCatalog.All.Where(t => enabled.Contains(t.Executable)))
            QuickTools.Add(new ToolItemVm(t));
    }

    public bool IsRefreshing => _balance.IsRefreshing;

    public void RefreshPeakStatus()
    {
        var (text, isPeak) = PeakHourService.CurrentStatus(DateTime.Now);
        PeakStatusText = text;
        IsPeakHour = isPeak;
    }

    public void LoadAll()
    {
        Reminders.Reload();
        ApiKeys.Reload();
        Settings.Load();
    }

    // ---- 托盘命令 ----

    [RelayCommand]
    private void ToggleWindow() => ShowWindowRequested?.Invoke();

    [RelayCommand]
    private void ShowWindow() => ShowWindowRequested?.Invoke();

    [RelayCommand]
    private async Task RefreshBalance()
    {
        RefreshPeakStatus();
        await _balance.RefreshAllAsync();
        ApiKeys.Reload();
    }

    [RelayCommand]
    private void Quit() => QuitRequested?.Invoke();
}
