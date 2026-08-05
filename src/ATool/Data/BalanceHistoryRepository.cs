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
}
