using ATool.Models;

namespace ATool.Services;

/// <summary>充值识别候选：一条余额增加记录。</summary>
public sealed record RechargeCandidate(long HistoryId, string Alias, string QueriedAt, decimal Delta);

/// <summary>充值汇总：充值金额合计 / 实际充值合计 / 佣金合计 / 差值（充值-实际+佣金）。</summary>
public sealed record RechargeSummary(decimal TotalDelta, decimal TotalActual, decimal TotalCommission, decimal Diff);

/// <summary>充值明细纯逻辑（无 I/O，可单测）：从余额记录识别充值（余额增加），汇总差值。</summary>
public static class RechargeService
{
    /// <summary>
    /// 识别充值记录：按 Key 分组、queried_at 升序，后一条余额 - 前一条 &gt; 0 即充值；
    /// 首条无前一条不计。输出按时间倒序（最新在前）。
    /// </summary>
    public static List<RechargeCandidate> DetectRecharges(IEnumerable<BalanceRecord> records)
    {
        var result = new List<RechargeCandidate>();
        foreach (var group in records.GroupBy(r => r.ApiKeyId))
        {
            var ordered = group
                .Where(r => DateTime.TryParse(r.QueriedAt, out _))
                .OrderBy(r => DateTime.Parse(r.QueriedAt))
                .ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var delta = ordered[i].TotalBalance - ordered[i - 1].TotalBalance;
                if (delta > 0)
                    result.Add(new RechargeCandidate(ordered[i].Id, ordered[i].Alias ?? "", ordered[i].QueriedAt, delta));
            }
        }
        result.Sort((a, b) => string.CompareOrdinal(b.QueriedAt, a.QueriedAt));
        return result;
    }

    /// <summary>汇总：(变动充值金额, 实际充值金额, 佣金) → 合计；差值 = 充值 - 实际 + 佣金。</summary>
    public static RechargeSummary Summarize(IEnumerable<(decimal Delta, decimal Actual, decimal Commission)> items)
    {
        decimal totalDelta = 0, totalActual = 0, totalCommission = 0;
        foreach (var (d, a, c) in items)
        {
            totalDelta += d;
            totalActual += a;
            totalCommission += c;
        }
        return new RechargeSummary(totalDelta, totalActual, totalCommission, totalDelta - totalActual + totalCommission);
    }
}
