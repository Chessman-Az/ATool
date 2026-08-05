using Dapper;
using ATool.Models;

namespace ATool.Data;

public sealed class ReminderRepository(Db db)
{
    public List<Reminder> GetAll(ReminderStatus? status = null)
    {
        using var conn = db.GetConnection();
        return status is null
            ? conn.Query<Reminder>("SELECT * FROM reminders ORDER BY trigger_time").ToList()
            : conn.Query<Reminder>("SELECT * FROM reminders WHERE status = @s ORDER BY trigger_time", new { s = (int)status.Value }).ToList();
    }

    public Reminder? Get(long id)
    {
        using var conn = db.GetConnection();
        return conn.QueryFirstOrDefault<Reminder>("SELECT * FROM reminders WHERE id = @id", new { id });
    }

    public long Insert(Reminder r)
    {
        using var conn = db.GetConnection();
        return conn.ExecuteScalar<long>(
            """
            INSERT INTO reminders (title, description, repeat_type, repeat_schedule, trigger_time, end_type, end_value, triggered_count, status, snooze_until, created_at, updated_at)
            VALUES (@Title, @Description, @RepeatType, @RepeatSchedule, @TriggerTime, @EndType, @EndValue, @TriggeredCount, @Status, @SnoozeUntil, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();
            """, r);
    }

    public void Update(Reminder r)
    {
        using var conn = db.GetConnection();
        conn.Execute(
            """
            UPDATE reminders SET title=@Title, description=@Description, repeat_type=@RepeatType, repeat_schedule=@RepeatSchedule,
            trigger_time=@TriggerTime, end_type=@EndType, end_value=@EndValue, triggered_count=@TriggeredCount,
            status=@Status, snooze_until=@SnoozeUntil, updated_at=@UpdatedAt WHERE id=@Id
            """, r);
    }

    public void Delete(long id)
    {
        using var conn = db.GetConnection();
        conn.Execute("DELETE FROM reminders WHERE id=@id", new { id });
    }

    public void SetStatus(long id, ReminderStatus status)
    {
        using var conn = db.GetConnection();
        conn.Execute("UPDATE reminders SET status=@s, updated_at=@now WHERE id=@id",
            new { id, s = (int)status, now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
    }

    public void IncrementTriggeredCount(long id)
    {
        using var conn = db.GetConnection();
        conn.Execute("UPDATE reminders SET triggered_count = triggered_count + 1, updated_at=@now WHERE id=@id",
            new { id, now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
    }

    /// <summary>写入延迟截止时间（本次延迟跳过，周期不变）。</summary>
    public void SetSnooze(long id, DateTime until)
    {
        using var conn = db.GetConnection();
        conn.Execute("UPDATE reminders SET snooze_until=@until, updated_at=@now WHERE id=@id",
            new { id, until = until.ToString("yyyy-MM-dd HH:mm:ss"), now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
    }
}
