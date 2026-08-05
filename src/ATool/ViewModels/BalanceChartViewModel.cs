using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using ATool.Data;
using ATool.Services;

namespace ATool.ViewModels;

public enum ChartRange { Days7 = 7, Days30 = 30, Year1 = 365, Custom = 0 }

/// <summary>
/// 余额趋势图：按选中 Key + 时间范围（7天/30天/1年/自定义）加载历史序列。
/// 悬停显示精确数值（LiveCharts 默认 tooltip）。
/// </summary>
public partial class BalanceChartViewModel : ObservableObject
{
    private readonly BalanceHistoryRepository _history;
    private readonly ApiKeysViewModel _keys;

    public ISeries[] Series { get; private set; } = [];
    public Axis[] XAxes { get; private set; } = [];
    public Axis[] YAxes { get; private set; } = [];

    [ObservableProperty]
    private ChartRange _range = ChartRange.Days7;

    [ObservableProperty]
    private string _emptyHint = "选择左侧 Key 查看余额趋势";

    [ObservableProperty]
    private DateTimeOffset? _customFrom;

    [ObservableProperty]
    private DateTimeOffset? _customTo;

    // ---- 范围单选（RadioButton 双向绑定）----
    [ObservableProperty] private bool _isDays7 = true;
    [ObservableProperty] private bool _isDays30;
    [ObservableProperty] private bool _isYear1;
    [ObservableProperty] private bool _isCustom;

    partial void OnIsDays7Changed(bool value) { if (value) { Range = ChartRange.Days7; Refresh(); } }
    partial void OnIsDays30Changed(bool value) { if (value) { Range = ChartRange.Days30; Refresh(); } }
    partial void OnIsYear1Changed(bool value) { if (value) { Range = ChartRange.Year1; Refresh(); } }
    partial void OnIsCustomChanged(bool value) { if (value) { Range = ChartRange.Custom; Refresh(); } }

    public BalanceChartViewModel(BalanceHistoryRepository history, ApiKeysViewModel keys)
    {
        _history = history;
        _keys = keys;
        _keys.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ApiKeysViewModel.SelectedKey))
                Refresh();
        };
        BuildAxes();
    }

    private void BuildAxes()
    {
        XAxes = new[]
        {
            new Axis
            {
                Labeler = v => new DateTime((long)v).ToString("MM-dd HH:mm"),
                TextSize = 11,
            }
        };
        YAxes = new[]
        {
            new Axis
            {
                Labeler = v => v.ToString("F2"),
                TextSize = 11,
            }
        };
    }

    /// <summary>按当前范围 + 选中 Key 刷新图表数据。</summary>
    public void Refresh()
    {
        if (_keys.SelectedKey is not { } item)
        {
            Series = [];
            EmptyHint = "选择左侧 Key 查看余额趋势";
            return;
        }

        var now = DateTime.Now;
        var (from, to) = Range switch
        {
            ChartRange.Days7 => (now.AddDays(-7), now),
            ChartRange.Days30 => (now.AddDays(-30), now),
            ChartRange.Year1 => (now.AddDays(-365), now),
            _ => (CustomFrom?.LocalDateTime ?? now.AddDays(-7), CustomTo?.LocalDateTime ?? now)
        };

        var records = _history.GetByKey(item.Key.Id, from, to);
        if (records.Count == 0)
        {
            Series = [];
            EmptyHint = "该时间范围内暂无余额记录（每次刷新自动记录）";
            return;
        }

        var points = ChartDataConverter.BuildPoints(records);
        Series = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Values = points,
                Name = item.Alias,
                GeometrySize = 6,
                LineSmoothness = 0.6,
            }
        };
        EmptyHint = "";
    }

    [RelayCommand]
    private void ApplyCustomRange() => Refresh();
}
