namespace ATool.Services;

/// <summary>全部 Key 余额汇总（null 视为 0，暂无余额的 Key 不参与）。</summary>
public static class BalanceSummaryService
{
    public static decimal Sum(IEnumerable<decimal?> balances)
    {
        decimal total = 0;
        foreach (var b in balances)
            if (b is { } v) total += v;
        return total;
    }
}
