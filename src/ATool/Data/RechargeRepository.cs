using Dapper;

namespace ATool.Data;

/// <summary>充值明细行（recharge_details JOIN balance_history；手动记录无 history 关联）。</summary>
public sealed class RechargeRow
{
    public long Id { get; set; }
    public long? HistoryId { get; set; }
    public string Alias { get; set; } = "";
    public string QueriedAt { get; set; } = "";
    public decimal Delta { get; set; }
    public decimal Actual { get; set; }
    public decimal Commission { get; set; }
}

/// <summary>充值明细仓储：自动为余额增加记录建行（幂等）、更新实际充值金额、查询全部。</summary>
public sealed class RechargeRepository(Db db)
{
    /// <summary>
    /// 确保每条余额增加记录都有充值明细行（幂等：已存在 history_id 不重复建），返回全部充值行（按时间倒序）。
    /// 新行 actual_amount 默认 = delta_amount。别名归属：手动行用 r.alias（空则「手动记录」），自动行取 Key 别名。
    /// </summary>
    public List<RechargeRow> EnsureAndGetAll()
    {
        using var conn = db.GetConnection();
        // 找出尚未建行的余额增加记录（按 key 分组、时间升序、delta>0）
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        conn.Execute(
            """
            INSERT OR IGNORE INTO recharge_details (history_id, delta_amount, actual_amount, updated_at)
            SELECT h.id, h.total_balance - prev.total_balance, h.total_balance - prev.total_balance, @now
            FROM balance_history h
            JOIN balance_history prev
              ON prev.api_key_id = h.api_key_id
             AND prev.id = (
                   SELECT MAX(p2.id) FROM balance_history p2
                   WHERE p2.api_key_id = h.api_key_id AND p2.id < h.id)
            WHERE h.total_balance - prev.total_balance > 0
            """, new { now });
        return conn.Query<RechargeRow>(
            """
            SELECT r.id AS Id, r.history_id AS HistoryId,
                   CASE WHEN r.alias IS NULL OR r.alias = '' THEN COALESCE(k.alias, '手动记录') ELSE r.alias END AS Alias,
                   COALESCE(h.queried_at, r.manual_time) AS QueriedAt, r.delta_amount AS Delta,
                   r.actual_amount AS Actual, r.commission_amount AS Commission
            FROM recharge_details r
            LEFT JOIN balance_history h ON h.id = r.history_id
            LEFT JOIN api_keys k ON k.id = h.api_key_id
            ORDER BY QueriedAt DESC
            """).ToList();
    }

    /// <summary>全部充值别名（自动行取 Key 别名 + 手动行归属别名，去重升序）。</summary>
    public List<string> GetAliases()
    {
        using var conn = db.GetConnection();
        return conn.Query<string>(
            """
            SELECT alias FROM (
                SELECT alias AS alias FROM api_keys
                UNION
                SELECT alias FROM recharge_details WHERE history_id IS NULL AND alias IS NOT NULL AND alias != ''
            ) WHERE alias IS NOT NULL AND alias != ''
            ORDER BY alias
            """).ToList();
    }

    /// <summary>手动添加一条充值记录（无对应余额历史，如历史充值补录）；alias 为空显示「手动记录」。</summary>
    public void InsertManual(string time, decimal delta, decimal actual, decimal commission, string alias = "")
    {
        using var conn = db.GetConnection();
        conn.Execute(
            """
            INSERT INTO recharge_details (delta_amount, actual_amount, commission_amount, manual_time, alias, updated_at)
            VALUES (@d, @a, @c, @t, @alias, @now)
            """,
            new { d = delta, a = actual, c = commission, t = time, alias, now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
    }

    /// <summary>
    /// 手动补录充值合计（history_id 为空的行）；自动识别行已在余额相邻差中，不计入。
    /// alias 为 null 统计全部手动行；指定别名只统计归属该别名的手动行（旧记录无别名不计入任何别名）。
    /// </summary>
    public decimal GetManualTotal(string? alias = null)
    {
        using var conn = db.GetConnection();
        return alias is null
            ? conn.ExecuteScalar<decimal>(
                "SELECT COALESCE(SUM(delta_amount), 0) FROM recharge_details WHERE history_id IS NULL")
            : conn.ExecuteScalar<decimal>(
                "SELECT COALESCE(SUM(delta_amount), 0) FROM recharge_details WHERE history_id IS NULL AND alias = @alias",
                new { alias });
    }

    /// <summary>更新一条充值记录的实际充值金额。</summary>
    public void UpdateActual(long id, decimal actualAmount)
    {
        using var conn = db.GetConnection();
        conn.Execute(
            "UPDATE recharge_details SET actual_amount = @a, updated_at = @now WHERE id = @id",
            new { id, a = actualAmount, now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
    }

    /// <summary>更新一条充值记录的佣金。</summary>
    public void UpdateCommission(long id, decimal commission)
    {
        using var conn = db.GetConnection();
        conn.Execute(
            "UPDATE recharge_details SET commission_amount = @c, updated_at = @now WHERE id = @id",
            new { id, c = commission, now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
    }

    /// <summary>
    /// 删除一条充值记录。注意：自动识别行（history_id 非空）由余额历史驱动，
    /// 下次 EnsureAndGetAll 会重建——仅手动补录行（history_id 为空）删除是持久的。
    /// </summary>
    public void Delete(long id)
    {
        using var conn = db.GetConnection();
        conn.Execute("DELETE FROM recharge_details WHERE id = @id", new { id });
    }
}
