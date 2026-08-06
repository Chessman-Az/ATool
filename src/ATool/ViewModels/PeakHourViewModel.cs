using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>峰谷计价详情（内嵌于余额界面）：24h 时段表 + 价格对比 + 当前状态横幅。</summary>
public partial class PeakHourViewModel : ObservableObject
{
    public IReadOnlyList<PeakHourService.PeakPeriod> Periods { get; } = PeakHourService.GetPeriods();

    public string StatusText => PeakHourService.CurrentStatus(DateTime.Now).Text;
    public string StatusIcon => PeakHourService.IsPeakHour(DateTime.Now) ? "🐟" : "💪";
    public IBrush StatusBrush => PeakHourService.IsPeakHour(DateTime.Now)
          ? new SolidColorBrush(Color.Parse("#F87171"))
          : new SolidColorBrush(Color.Parse("#34D399"));
}
