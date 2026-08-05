using Dapper;

namespace ATool.Data;

public sealed class SettingsRepository(Db db)
{
    public string? Get(string key)
    {
        using var conn = db.GetConnection();
        return conn.QueryFirstOrDefault<string>("SELECT value FROM settings WHERE key=@key", new { key });
    }

    public void Set(string key, string value)
    {
        using var conn = db.GetConnection();
        conn.Execute(
            "INSERT INTO settings (key, value) VALUES (@key, @value) ON CONFLICT(key) DO UPDATE SET value=@value",
            new { key, value });
    }

    public Dictionary<string, string> GetAll()
    {
        using var conn = db.GetConnection();
        return conn.Query<(string Key, string Value)>("SELECT key, value FROM settings")
            .ToDictionary(x => x.Key, x => x.Value);
    }
}
