using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using ATool.Data;
using ATool.Services;

namespace ATool.ViewModels;

public enum ChartRange { Days7 = 7, Days30 = 30, Year1 = 365 }

/// <summary>
/// 余额趋势图：按选中 Key + 时间范围（7天/30天/1年/自定义）加载历史序列。
/// 悬停显示精确数值（LiveCharts 默认 tooltip）。
/// </summary>
public partial class BalanceChartViewModel : ObservableObject
{
    private readonly BalanceHistoryRepository _history;
    private readonly ApiKeysViewModel _keys;

    /// <summary>图表序列（带通知，赋值时同步通知 HasData）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasData))]
    private ISeries[] _series = [];

    /// <summary>是否有数据可画（图表可见 / 空提示可见的开关）。</summary>
    public bool HasData => Series.Length > 0;

    public Axis[] XAxes { get; private set; } = [];
    public Axis[] YAxes { get; private set; } = [];

    [ObservableProperty]
    private ChartRange _range = ChartRange.Days7;

    [ObservableProperty]
    private string _emptyHint = "选择左侧 Key 查看余额趋势";

    // ---- 范围单选（RadioButton 双向绑定）----
    [ObservableProperty] private bool _isDays7 = true;
    [ObservableProperty] private bool _isDays30;
    [ObservableProperty] private bool _isYear1;

    partial void OnIsDays7Changed(bool value) { if (value) { Range = ChartRange.Days7; Refresh(); } }
    partial void OnIsDays30Changed(bool value) { if (value) { Range = ChartRange.Days30; Refresh(); } }
    partial void OnIsYear1Changed(bool value) { if (value) { Range = ChartRange.Year1; Refresh(); } }

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
        // 坐标轴美化：文字浅灰、分隔线浅灰实线，X 轴显示日期（MM-dd）
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
                Labeler = v => v.ToString("F2"),
                TextSize = 11,
                MinLimit = 0, // 纵轴从 0 开始
                LabelsPaint = labelPaint,
                SeparatorsPaint = sepPaint,
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
        var from = Range switch
        {
            ChartRange.Days7 => now.AddDays(-7),
            ChartRange.Days30 => now.AddDays(-30),
            _ => now.AddDays(-365),
        };
        var to = now;

        var records = _history.GetByKey(item.Key.Id, from, to);
        if (records.Count == 0)
        {
            Series = [];
            EmptyHint = "该时间范围内暂无余额记录（每次刷新自动记录）";
            return;
        }

        var points = ChartDataConverter.BuildPoints(records);
          var lineColor = SKColor.Parse("#38BDF8");
        Series = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Values = points,
                Name = item.Alias,
                GeometrySize = 7,
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometryStroke = new SolidColorPaint(lineColor) { StrokeThickness = 2 },
                Stroke = new SolidColorPaint(lineColor) { StrokeThickness = 2.5f },
                // 线下方淡蓝渐变填充
                Fill = new LinearGradientPaint(
                    new SKColor(59, 111, 224, 45),
                    new SKColor(59, 111, 224, 0)),
                LineSmoothness = 0.7,
            }
        };
        EmptyHint = "";
    }
}
