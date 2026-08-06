using ATool.Data;
using Xunit;

namespace ATool.Tests;

/// <summary>usage_log 仓储往返测试：建表 → Insert → CloseSegment → QueryRange → DeleteBefore。</summary>
public class UsageLogRepositoryTests
{
    private static (Db db, UsageLogRepository repo) NewRepo()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-usage-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        return (db, new UsageLogRepository(db));
    }

    [Fact]
    public void Insert_返回自增ID_可回读()
    {
        var (_, repo) = NewRepo();
        var start = DateTime.Now;

        var id = repo.Insert("chrome", "百度 - Google Chrome", "browser", start);

        Assert.True(id > 0);
        var all = repo.QueryRange(DateTime.MinValue, DateTime.MaxValue);
        var row = Assert.Single(all);
        Assert.Equal(id, row.Id);
        Assert.Equal("chrome", row.ProcessName);
        Assert.Equal("百度 - Google Chrome", row.WindowTitle);
        Assert.Equal("browser", row.Category);
        Assert.Equal(start.ToString("yyyy-MM-dd HH:mm:ss"), row.StartTime);
        Assert.Null(row.EndTime);
        Assert.Equal(0, row.DurationSec);
    }

    [Fact]
    public void CloseSegment_写入结束时间与时长()
    {
        var (_, repo) = NewRepo();
        var start = DateTime.Now.AddMinutes(-5);
        var id = repo.Insert("steam", "Steam", "game", start);

        repo.CloseSegment(id, DateTime.Now, 300);

        var row = repo.QueryRange(DateTime.MinValue, DateTime.MaxValue).Single();
        Assert.NotNull(row.EndTime);
        Assert.Equal(300, row.DurationSec);
    }

    [Fact]
    public void QueryRange_按区间过滤_升序返回()
    {
        var (_, repo) = NewRepo();
        var day = new DateTime(2026, 8, 6, 0, 0, 0);
        repo.Insert("a", "A", "other", day.AddHours(9));
        repo.Insert("b", "B", "other", day.AddHours(10));
        repo.Insert("c", "C", "other", day.AddDays(1).AddHours(9)); // 区间外

        var rows = repo.QueryRange(day, day.AddDays(1));

        Assert.Equal(2, rows.Count);
        Assert.Equal("a", rows[0].ProcessName);
        Assert.Equal("b", rows[1].ProcessName);
    }

    [Fact]
    public void DeleteBefore_清理早于截止日期的记录()
    {
        var (_, repo) = NewRepo();
        var now = DateTime.Now;
        repo.Insert("keep", "K", "other", now);                 // 保留
        repo.Insert("old", "O", "other", now.AddDays(-91));     // 清理

        var deleted = repo.DeleteBefore(90);

        Assert.Equal(1, deleted);
        var remaining = repo.QueryRange(DateTime.MinValue, DateTime.MaxValue);
        var row = Assert.Single(remaining);
        Assert.Equal("keep", row.ProcessName);
    }
}
