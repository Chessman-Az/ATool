using Dapper;
using ATool.Data;
using ATool.ViewModels;
using Xunit;

namespace ATool.Tests;

/// <summary>充值明细 VM：按别名筛选分离（一个别名一个明细），汇总随筛选联动。</summary>
public class RechargeViewModelTests
{
    private static (RechargeRepository repo, Db db) NewRepo()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-recharge-vm-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        return (new RechargeRepository(db), db);
    }

    /// <summary>造一个 Key 及其一次余额增加（自动识别为充值）。</summary>
    private static void InsertKeyWithRecharge(Db db, long keyId, string alias, decimal from, decimal to)
    {
        using var conn = db.GetConnection();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        conn.Execute("INSERT INTO api_keys (alias, encrypted_key, created_at) VALUES (@a, X'01', @t)",
            new { a = alias, t = now });
        conn.Execute("INSERT INTO balance_history (api_key_id, total_balance, currency, queried_at) VALUES (@k, @b, 'CNY', @t)",
            new { k = keyId, b = from, t = now });
        conn.Execute("INSERT INTO balance_history (api_key_id, total_balance, currency, queried_at) VALUES (@k, @b, 'CNY', @t)",
            new { k = keyId, b = to, t = now });
    }

    [Fact]
    public void 默认选中第一个别名_明细与汇总按别名分离()
    {
        var (repo, db) = NewRepo();
        InsertKeyWithRecharge(db, 1, "key1", 10m, 20m); // 充值 +10
        InsertKeyWithRecharge(db, 2, "key2", 5m, 25m);  // 充值 +20

        var vm = new RechargeViewModel(repo);

        // 默认第一个别名（GetAliases 升序 → key1），明细与汇总只含 key1
        Assert.Equal("key1", vm.SelectedAlias);
        Assert.Single(vm.Rows);
        Assert.Equal("¥10.00", vm.TotalDeltaText);
        Assert.Equal("¥10.00", vm.TotalActualText);
        Assert.Equal("¥0.00", vm.DiffText);
    }

    [Fact]
    public void 切换别名_明细与汇总联动()
    {
        var (repo, db) = NewRepo();
        InsertKeyWithRecharge(db, 1, "key1", 10m, 20m);
        InsertKeyWithRecharge(db, 2, "key2", 5m, 25m);

        var vm = new RechargeViewModel(repo);

        vm.SelectedAlias = "key2";
        Assert.Single(vm.Rows);
        Assert.Equal("¥20.00", vm.TotalDeltaText);
        Assert.Equal("key2", vm.Rows[0].Alias);

        vm.SelectedAlias = RechargeViewModel.AllAliases;
        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal("¥30.00", vm.TotalDeltaText);
        Assert.Equal("¥0.00", vm.DiffText); // 30 - 30 + 0
    }

    [Fact]
    public void 手动添加_归属所选别名_汇总计入该别名()
    {
        var (repo, db) = NewRepo();
        InsertKeyWithRecharge(db, 1, "key1", 10m, 20m);

        var vm = new RechargeViewModel(repo);
        vm.ManualAlias = "key1";
        vm.NewDelta = 5m;
        vm.NewActual = 4m;
        vm.NewCommission = 0.5m;
        vm.NewTime = "2026-07-01 12:00:00";
        vm.AddCommand.Execute(null);

        Assert.Equal("¥15.00", vm.TotalDeltaText); // 10 + 5
        Assert.Equal(2, vm.Rows.Count);
        Assert.All(vm.Rows, r => Assert.Equal("key1", r.Alias));
    }
}
