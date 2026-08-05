using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>峰谷计价详情（内嵌于余额界面）：24h 时段表 + 价格对比 + 当前状态横幅。</summary>
public partial class PeakHourViewModel : ObservableObject
{
    public IReadOnlyList<PeakHourService.PeakPeriod> Periods { get; } = PeakHourService.GetPeriods();

    /// <summary>示例价格（低谷 ¥1.0 / 高峰 ¥2.0 每百万 tokens，仅为对比展示）。</summary>
    public string PriceCompareText =>
        "价格对比（示例：每百万 tokens）低谷 ¥1.00 ／ 高峰 ¥2.00（2 倍）";

    public string StatusText => PeakHourService.CurrentStatus(DateTime.Now).Text;
    public string StatusIcon => PeakHourService.IsPeakHour(DateTime.Now) ? "🐟" : "💪";
    public IBrush StatusBrush => PeakHourService.IsPeakHour(DateTime.Now)
        ? new SolidColorBrush(Color.Parse("#D64545"))
        : new SolidColorBrush(Color.Parse("#2E9E5B"));
}
