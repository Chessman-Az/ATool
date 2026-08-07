using Dapper;
using ATool.Models;

namespace ATool.Data;

public sealed class BalanceHistoryRepository(Db db)
{
    public void Insert(BalanceRecord r)
    {
        using var conn = db.GetConnection();
        conn.Execute(
            "INSERT INTO balance_history (api_key_id, total_balance, granted_balance, topped_up_balance, currency, queried_at) VALUES (@ApiKeyId, @TotalBalance, @GrantedBalance, @ToppedUpBalance, @Currency, @QueriedAt)",
            r);
    }

    /// <summary>该 Key 最近一条历史（用于计算变动金额）；无记录返回 null。</summary>
    public BalanceRecord? GetLatest(long apiKeyId)
    {
        using var conn = db.GetConnection();
        return conn.QueryFirstOrDefault<BalanceRecord>(
            "SELECT * FROM balance_history WHERE api_key_id=@id ORDER BY id DESC LIMIT 1", new { id = apiKeyId });
    }

    public List<BalanceRecord> GetByKey(long apiKeyId, DateTime from, DateTime to)
    {
        using var conn = db.GetConnection();
        return conn.Query<BalanceRecord>(
            "SELECT * FROM balance_history WHERE api_key_id=@id AND queried_at >= @from AND queried_at <= @to ORDER BY queried_at",
            new { id = apiKeyId, from = from.ToString("yyyy-MM-dd HH:mm:ss"), to = to.ToString("yyyy-MM-dd HH:mm:ss") }).ToList();
    }

    /// <summary>全部余额记录 + Key 别名（明细页用；Alias 由 JOIN 注入）。</summary>
    public List<BalanceRecord> GetAllWithAlias()
    {
        using var conn = db.GetConnection();
        return conn.Query<BalanceRecord>(
            "SELECT h.*, k.alias AS Alias FROM balance_history h JOIN api_keys k ON h.api_key_id = k.id ORDER BY h.queried_at").ToList();
    }

    /// <summary>
    /// 总充值金额 / 总消费金额：按 Key 分组、时间升序，相邻余额差 &gt;0 累加为充值、&lt;0 累加为消费（绝对值）。
    /// 首条无前一条不计。
    /// </summary>
    public (decimal Recharge, decimal Consume) GetTotals(long? apiKeyId = null)
    {
        var records = GetAllWithAlias();
        decimal recharge = 0, consume = 0;
        foreach (var group in records.Where(r => apiKeyId is null || r.ApiKeyId == apiKeyId).GroupBy(r => r.ApiKeyId))
        {
            var ordered = group
                .Where(r => DateTime.TryParse(r.QueriedAt, out _))
                .OrderBy(r => DateTime.Parse(r.QueriedAt))
                .ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var delta = ordered[i].TotalBalance - ordered[i - 1].TotalBalance;
                if (delta > 0) recharge += delta;
                else if (delta < 0) consume += -delta;
            }
        }
        return (recharge, consume);
    }
}
