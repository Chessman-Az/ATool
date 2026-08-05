using CommunityToolkit.Mvvm.ComponentModel;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>峰谷计价详情：24h 时段表 + 价格对比 + 当前状态。</summary>
public partial class PeakHourViewModel : ObservableObject
{
    public IReadOnlyList<PeakHourService.PeakPeriod> Periods { get; } = PeakHourService.GetPeriods();

    /// <summary>示例价格（低谷 ¥1.0 / 高峰 ¥2.0 每百万 tokens，仅为对比展示）。</summary>
    public string PriceCompareText =>
        "价格对比（示例：每百万 tokens）\n低谷 ¥1.00  高峰 ¥2.00（2 倍）";

    public string StatusText => PeakHourService.CurrentStatus(DateTime.Now).Text;
    public bool IsPeakNow => PeakHourService.IsPeakHour(DateTime.Now);
}
