using Dapper;

namespace ATool.Data;

/// <summary>
/// 前台窗口使用时长的一段记录（usage_log 表行）。
/// Dapper 全局下划线映射已在 Db 构造开启，列名自动映射到 PascalCase 属性。
/// </summary>
public sealed class UsageLog
{
    public long Id { get; set; }
    public string ProcessName { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string Category { get; set; } = "other";
    public string StartTime { get; set; } = "";
    public string? EndTime { get; set; }
    public int DurationSec { get; set; }
}

/// <summary>usage_log 表仓储：开段/闭段/区间查询/过期清理。</summary>
public sealed class UsageLogRepository(Db db)
{
    /// <summary>打开新段：写入未闭合记录，返回自增 id。</summary>
    public long Insert(string process, string title, string category, DateTime startTime)
    {
        using var conn = db.GetConnection();
        return conn.ExecuteScalar<long>(
            """
            INSERT INTO usage_log (process_name, window_title, category, start_time)
            VALUES (@process, @title, @category, @start);
            SELECT last_insert_rowid();
            """,
            new { process, title, category, start = startTime.ToString("yyyy-MM-dd HH:mm:ss") });
    }

    /// <summary>闭合一段：写入 end_time 与累计时长（秒）。</summary>
    public void CloseSegment(long id, DateTime endTime, int durationSec)
    {
        using var conn = db.GetConnection();
        conn.Execute(
            "UPDATE usage_log SET end_time=@end, duration_sec=@dur WHERE id=@id",
            new { id, end = endTime.ToString("yyyy-MM-dd HH:mm:ss"), dur = durationSec });
    }

    /// <summary>查询 [start, end) 左闭右开区间内的记录，按开始时间升序。</summary>
    public List<UsageLog> QueryRange(DateTime start, DateTime end)
    {
        using var conn = db.GetConnection();
        return conn.Query<UsageLog>(
            "SELECT * FROM usage_log WHERE start_time >= @start AND start_time < @end ORDER BY start_time",
            new
            {
                start = start.ToString("yyyy-MM-dd HH:mm:ss"),
                end = end.ToString("yyyy-MM-dd HH:mm:ss"),
            }).ToList();
    }

    /// <summary>清理 start_time 早于 now-days 天的记录，返回删除行数（幂等）。</summary>
    public int DeleteBefore(int days)
    {
        using var conn = db.GetConnection();
        var cutoff = DateTime.Now.AddDays(-days).ToString("yyyy-MM-dd HH:mm:ss");
        return conn.Execute("DELETE FROM usage_log WHERE start_time < @cutoff", new { cutoff });
    }
}
