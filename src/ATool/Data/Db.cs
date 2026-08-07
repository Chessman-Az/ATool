using Microsoft.Data.Sqlite;

namespace ATool.Data;

/// <summary>
/// SQLite 访问入口：管理数据目录、连接串、schema v1 初始化。
/// 连接串固定开启 ForeignKeys（api_keys 删除时级联清 balance_history）。
/// </summary>
public sealed class Db
{
    public string DataPath { get; private set; }

    public Db(string dataPath)
    {
        DataPath = dataPath;
        Directory.CreateDirectory(dataPath);
        // 关键修复：SQLite 列名均为下划线风格（total_balance/trigger_time...），
        // Dapper 默认不匹配下划线 → 映射静默失败返回默认值（余额恒 0、重复规则恒 Single）。
        // 全局开启下划线匹配，一次修复所有实体的列映射。
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    /// <summary>路径迁移后切换数据目录（连接串按当前路径实时构造）。</summary>
    public void ChangePath(string newPath)
    {
        DataPath = newPath;
        Directory.CreateDirectory(newPath);
    }

    public SqliteConnection GetConnection()
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(DataPath, "atool.db"),
            ForeignKeys = true,
        }.ToString());
        conn.Open();
        // 多实例/并发写时等待锁而不是立即失败（busy_timeout 5s）
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout = 5000";
        cmd.ExecuteNonQuery();
        return conn;
    }

    /// <summary>幂等建表（schema v1）。</summary>
    public void InitializeSchema()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS reminders (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              title TEXT NOT NULL,
              description TEXT NOT NULL DEFAULT '',
              repeat_type INTEGER NOT NULL,
              repeat_schedule TEXT NOT NULL DEFAULT '[]',
              trigger_time TEXT NOT NULL,
              end_type INTEGER NOT NULL DEFAULT 0,
              end_value TEXT,
              triggered_count INTEGER NOT NULL DEFAULT 0,
              notify_enabled INTEGER NOT NULL DEFAULT 1,
              status INTEGER NOT NULL DEFAULT 0,
              snooze_until TEXT,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS api_keys (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              alias TEXT NOT NULL,
              encrypted_key BLOB NOT NULL,
              last_error TEXT,
              created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS balance_history (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              api_key_id INTEGER NOT NULL REFERENCES api_keys(id) ON DELETE CASCADE,
              total_balance REAL NOT NULL,
              granted_balance REAL,
              topped_up_balance REAL,
              currency TEXT NOT NULL,
              queried_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS settings (
              key TEXT PRIMARY KEY,
              value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS usage_log (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              process_name TEXT NOT NULL,
              window_title TEXT NOT NULL DEFAULT '',
              category TEXT NOT NULL DEFAULT 'other',
              start_time TEXT NOT NULL,
              end_time TEXT,
              duration_sec INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS recharge_details (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              history_id INTEGER REFERENCES balance_history(id) ON DELETE CASCADE,
              delta_amount REAL NOT NULL,
              actual_amount REAL NOT NULL DEFAULT 0,
              commission_amount REAL NOT NULL DEFAULT 0,
              manual_time TEXT,
              updated_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_balance_history_key ON balance_history(api_key_id, queried_at);
            CREATE INDEX IF NOT EXISTS idx_usage_log_start ON usage_log(start_time);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_recharge_history ON recharge_details(history_id);
            """;
        cmd.ExecuteNonQuery();

        // schema 迁移：旧库 reminders 表补充 notify_enabled 列（重复执行忽略 duplicate column）
        try
        {
            using var mig = conn.CreateCommand();
            mig.CommandText = "ALTER TABLE reminders ADD COLUMN notify_enabled INTEGER NOT NULL DEFAULT 1";
            mig.ExecuteNonQuery();
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
            // 列已存在（新库 CREATE TABLE 已含）——忽略
        }

        // schema 迁移：recharge_details 补佣金列（旧库）
        try
        {
            using var mig2 = conn.CreateCommand();
            mig2.CommandText = "ALTER TABLE recharge_details ADD COLUMN commission_amount REAL NOT NULL DEFAULT 0";
            mig2.ExecuteNonQuery();
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
            // 列已存在——忽略
        }

        // schema 迁移：recharge_details 重建——history_id 可空 + manual_time 列（支持手动添加充值记录）
        try
        {
            using var chk = conn.CreateCommand();
            chk.CommandText = "SELECT COUNT(*) FROM pragma_table_info('recharge_details') WHERE name='manual_time'";
            if (Convert.ToInt32(chk.ExecuteScalar()) == 0)
            {
                using var tx = conn.BeginTransaction();
                using (var rebuild = conn.CreateCommand())
                {
                    rebuild.CommandText = """
                        CREATE TABLE recharge_details_new (
                          id INTEGER PRIMARY KEY AUTOINCREMENT,
                          history_id INTEGER REFERENCES balance_history(id) ON DELETE CASCADE,
                          delta_amount REAL NOT NULL,
                          actual_amount REAL NOT NULL DEFAULT 0,
                          commission_amount REAL NOT NULL DEFAULT 0,
                          manual_time TEXT,
                          updated_at TEXT NOT NULL
                        );
                        INSERT INTO recharge_details_new (id, history_id, delta_amount, actual_amount, commission_amount, updated_at)
                          SELECT id, history_id, delta_amount, actual_amount, commission_amount, updated_at FROM recharge_details;
                        DROP TABLE recharge_details;
                        ALTER TABLE recharge_details_new RENAME TO recharge_details;
                        CREATE UNIQUE INDEX IF NOT EXISTS idx_recharge_history ON recharge_details(history_id);
                        """;
                    rebuild.ExecuteNonQuery();
                }
                tx.Commit();
            }
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // 表不存在或已是最新——忽略
        }
    }
}
