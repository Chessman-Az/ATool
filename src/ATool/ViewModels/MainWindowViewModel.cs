using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>
/// 主窗口 VM：页面导航（提醒/Key/设置）、托盘命令（显隐/刷新/退出）、峰谷主按钮状态。
/// 窗口操作（Show/Hide/关闭确认）经事件交给 MainWindow 处理。
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly BalanceService _balance;

    public ReminderListViewModel Reminders { get; }
    public ApiKeysViewModel ApiKeys { get; }
    public SettingsViewModel Settings { get; }
    public BalanceChartViewModel Chart { get; }
    public PeakHourViewModel PeakHour { get; } = new();
    public ReminderCalendarViewModel Calendar { get; }
    public BalanceDetailViewModel BalanceDetail { get; }

    public event Action? ShowWindowRequested;
    public event Action? QuitRequested;

    /// <summary>左侧导航选中项：0=提醒事项 1=DeepSeek 余额 2=系统设置。</summary>
    [ObservableProperty]
    private int _navIndex;

    [ObservableProperty]
    private bool _isPeakHour;

    [ObservableProperty]
    private string _peakStatusText = "";

    public MainWindowViewModel(BalanceService balance, ReminderListViewModel reminders, ApiKeysViewModel apiKeys, SettingsViewModel settings, BalanceChartViewModel chart, ReminderCalendarViewModel calendar, BalanceDetailViewModel balanceDetail)
    {
        _balance = balance;
        Reminders = reminders;
        ApiKeys = apiKeys;
        Settings = settings;
        Chart = chart;
        Calendar = calendar;
        BalanceDetail = balanceDetail;
        RefreshPeakStatus();
        _balance.StateChanged += () => OnPropertyChanged(nameof(IsRefreshing));
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
