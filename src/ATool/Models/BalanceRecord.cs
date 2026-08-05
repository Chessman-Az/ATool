namespace ATool.Models;

/// <summary>一次余额查询的历史记录。Delta 为较上次查询的变动金额（查询时计算）。</summary>
public sealed class BalanceRecord
{
    public long Id { get; set; }
    public long ApiKeyId { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal? GrantedBalance { get; set; }
    public decimal? ToppedUpBalance { get; set; }
    public string Currency { get; set; } = "CNY";
    public string QueriedAt { get; set; } = ""; // yyyy-MM-dd HH:mm:ss
    public decimal? Delta { get; set; }

    /// <summary>查询时 JOIN 注入的 Key 别名（不落库）。</summary>
    public string? Alias { get; set; }
}
