using ATool.Models;

namespace ATool.Services;

/// <summary>余额变动明细行组装（纯函数，可单测）。</summary>
public static class BalanceHistoryDetailService
{
    public sealed record HistoryRow(string QueriedAt, string Alias, decimal Balance, string DeltaText);

    /// <summary>
    /// 记录 → 明细行：按 queried_at 升序计算变动（后一条 - 前一条，首条为「—」），
    /// 输出按时间倒序（最新在前）。
    /// </summary>
    public static List<HistoryRow> BuildRows(IEnumerable<BalanceRecord> records)
    {
        var ordered = records
            .Where(r => DateTime.TryParse(r.QueriedAt, out _))
            .OrderBy(r => DateTime.Parse(r.QueriedAt))
            .ToList();

        var rows = new List<HistoryRow>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var r = ordered[i];
            var deltaText = i == 0
                ? "—"
                : FormatDelta(r.TotalBalance - ordered[i - 1].TotalBalance);
            rows.Add(new HistoryRow(r.QueriedAt, r.Alias ?? "", r.TotalBalance, deltaText));
        }
        rows.Reverse();
        return rows;
    }

    private static string FormatDelta(decimal d) => d >= 0 ? $"+{d:F2}" : $"{d:F2}";
}
