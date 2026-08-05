using Dapper;
using Microsoft.Data.Sqlite;
using ATool.Models;

namespace ATool.Data;

public sealed class ApiKeyRepository(Db db)
{
    /// <summary>
    /// 手动映射（不用 Dapper 属性映射）：Dapper 对 SQLite BLOB → byte[] 属性
    /// 会静默返回空数组（实测），导致密文读回为空、解密失败、余额不显示。
    /// </summary>
    public List<ApiKey> GetAll()
    {
        using var conn = db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, alias, encrypted_key, last_error, created_at FROM api_keys ORDER BY id";
        using var reader = cmd.ExecuteReader();
        var list = new List<ApiKey>();
        while (reader.Read())
        {
            list.Add(new ApiKey
            {
                Id = reader.GetInt64(0),
                Alias = reader.GetString(1),
                EncryptedKey = reader.GetFieldValue<byte[]>(2),
                LastError = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = reader.GetString(4),
            });
        }
        return list;
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
