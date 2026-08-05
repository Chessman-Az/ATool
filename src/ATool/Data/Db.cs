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
            CREATE INDEX IF NOT EXISTS idx_balance_history_key ON balance_history(api_key_id, queried_at);
            """;
        cmd.ExecuteNonQuery();
    }
}
