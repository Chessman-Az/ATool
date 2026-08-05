using Dapper;
using ATool.Models;

namespace ATool.Data;

public sealed class ApiKeyRepository(Db db)
{
    public List<ApiKey> GetAll()
    {
        using var conn = db.GetConnection();
        return conn.Query<ApiKey>("SELECT * FROM api_keys ORDER BY id").ToList();
    }

    public long Insert(ApiKey key)
    {
        using var conn = db.GetConnection();
        return conn.ExecuteScalar<long>(
            "INSERT INTO api_keys (alias, encrypted_key, last_error, created_at) VALUES (@Alias, @EncryptedKey, @LastError, @CreatedAt); SELECT last_insert_rowid();",
            key);
    }

    public void Delete(long id)
    {
        using var conn = db.GetConnection();
        conn.Execute("DELETE FROM api_keys WHERE id=@id", new { id }); // 级联清 balance_history
    }

    public void UpdateLastError(long id, string? error)
    {
        using var conn = db.GetConnection();
        conn.Execute("UPDATE api_keys SET last_error=@e WHERE id=@id", new { id, e = error });
    }
}
