using LiveChartsCore.Defaults;
using ATool.Models;

namespace ATool.Services;

/// <summary>余额历史 → 图表点集转换（纯函数，可单测）。</summary>
public static class ChartDataConverter
{
    /// <summary>
    /// 历史记录转折线图点集：按 queried_at 升序；非法时间与负余额记录被过滤（防御脏数据）。
    /// </summary>
    public static List<DateTimePoint> BuildPoints(IEnumerable<BalanceRecord> records) =>
        records
            .Where(r => r.TotalBalance >= 0 && DateTime.TryParse(r.QueriedAt, out _))
            .Select(r => new DateTimePoint(DateTime.Parse(r.QueriedAt), (double)r.TotalBalance))
            .OrderBy(p => p.DateTime)
            .ToList();
}
