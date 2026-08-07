using Dapper;
using ATool.Data;
using ATool.Models;
using Xunit;

namespace ATool.Tests;

/// <summary>充值明细仓储：自动为增加记录建行（幂等）、更新实际金额、查询汇总。</summary>
public class RechargeRepositoryTests
{
    private static (RechargeRepository repo, Db db) NewRepo()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-recharge-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        return (new RechargeRepository(db), db);
    }

    private static long InsertHistory(Db db, long keyId, decimal balance, string at)
    {
        using var conn = db.GetConnection();
        conn.ExecuteScalar<long>($"""
            INSERT INTO balance_history (api_key_id, total_balance, currency, queried_at)
            VALUES ({keyId}, {balance.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 'CNY', '{at}');
            SELECT last_insert_rowid();
            """);
        return 0; // 实际 id 由查询获取
    }

    [Fact]
    public void EnsureAndGetAll_为充值记录建行_默认实际等于变动()
    {
        var (repo, db) = NewRepo();
        // 造 2 条记录：10 → 11（充值 +1）
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        using (var conn = db.GetConnection())
        {
            conn.Execute("INSERT INTO api_keys (alias, encrypted_key, created_at) VALUES ('k1', X'01', @a)", new { a = now });
            conn.ExecuteScalar<long>(
                "INSERT INTO balance_history (api_key_id, total_balance, currency, queried_at) VALUES (1, 10, 'CNY', @a); SELECT last_insert_rowid();", new { a = now });
            conn.ExecuteScalar<long>(
                "INSERT INTO balance_history (api_key_id, total_balance, currency, queried_at) VALUES (1, 11, 'CNY', @b); SELECT last_insert_rowid();", new { b = now });
        }

        var rows = repo.EnsureAndGetAll();

        var r = Assert.Single(rows);
        Assert.Equal(1m, r.Delta);
        Assert.Equal(1m, r.Actual); // 默认实际 = 变动
    }

    [Fact]
    public void EnsureAndGetAll_幂等_不重复建行()
    {
        var (repo, db) = NewRepo();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        using var conn = db.GetConnection();
        conn.Execute("INSERT INTO api_keys (alias, encrypted_key, created_at) VALUES ('k1', X'01', @a)", new { a = now });
        conn.Execute("INSERT INTO balance_history (api_key_id, total_balance, currency, queried_at) VALUES (1, 10, 'CNY', @a)", new { a = now });
        conn.Execute("INSERT INTO balance_history (api_key_id, total_balance, currency, queried_at) VALUES (1, 12, 'CNY', @a)", new { a = now });

        _ = repo.EnsureAndGetAll();
        var rows = repo.EnsureAndGetAll();

        Assert.Single(rows);
    }

    [Fact]
    public void InsertManual_手动记录可查_别名手动记录()
    {
        var (repo, db) = NewRepo();

        repo.InsertManual("2026-07-01 12:00:00", 5m, 4.5m, 0.3m);

        var rows = repo.EnsureAndGetAll();
        var r = Assert.Single(rows);
        Assert.Equal("手动记录", r.Alias);
        Assert.Equal("2026-07-01 12:00:00", r.QueriedAt);
        Assert.Equal(5m, r.Delta);
        Assert.Equal(4.5m, r.Actual);
        Assert.Equal(0.3m, r.Commission);
    }

    [Fact]
    public void GetManualTotal_只统计手动记录()
    {
        var (repo, db) = NewRepo();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        using (var conn = db.GetConnection())
        {
            conn.Execute("INSERT INTO api_keys (alias, encrypted_key, created_at) VALUES ('k1', X'01', @a)", new { a = now });
            conn.Execute("INSERT INTO balance_history (api_key_id, total_balance, currency, queried_at) VALUES (1, 10, 'CNY', @a)", new { a = now });
            conn.Execute("INSERT INTO balance_history (api_key_id, total_balance, currency, queried_at) VALUES (1, 12, 'CNY', @a)", new { a = now }); // 自动充值 +2
        }
        _ = repo.EnsureAndGetAll(); // 自动行 +2
        repo.InsertManual("2026-07-01 12:00:00", 5m, 4.5m, 0.3m); // 手动 +5

        var manual = repo.GetManualTotal();

        Assert.Equal(5m, manual); // 只算手动记录，自动 +2 不计
    }

    [Fact]
    public void UpdateActual_写入实际金额_回读生效()
    {
        var (repo, db) = NewRepo();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        using (var conn = db.GetConnection())
        {
            conn.Execute("INSERT INTO api_keys (alias, encrypted_key, created_at) VALUES ('k1', X'01', @a)", new { a = now });
            conn.Execute("INSERT INTO balance_history (api_key_id, total_balance, currency, queried_at) VALUES (1, 10, 'CNY', @a)", new { a = now });
            conn.Execute("INSERT INTO balance_history (api_key_id, total_balance, currency, queried_at) VALUES (1, 11, 'CNY', @a)", new { a = now });
        }

        var rows = repo.EnsureAndGetAll();
        repo.UpdateActual(rows[0].Id, 0.5m);

        var after = repo.EnsureAndGetAll();
        Assert.Equal(0.5m, after[0].Actual);
    }
}
