using ATool.Data;
using ATool.Models;
using Xunit;

namespace ATool.Tests;

/// <summary>「是否提醒」开关：模型默认值 + notify_enabled 列持久化（旧数据默认开启弹窗）。</summary>
public class NotifyEnabledTests
{
    [Fact]
    public void 新建提醒默认不提醒()
    {
        var r = new Reminder { Title = "t" };
        Assert.False(r.NotifyEnabled); // 默认不勾选「是否提醒」
    }

    [Fact]
    public void NotifyEnabled_插入读取与更新持久化()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-notify-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var repo = new ReminderRepository(db);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var id = repo.Insert(new Reminder
        {
            Title = "静默提醒",
            TriggerTime = "09:00:00",
            Status = ReminderStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            NotifyEnabled = false, // 不勾选「是否提醒」
        });

        var loaded = repo.Get(id);
        Assert.NotNull(loaded);
        Assert.False(loaded!.NotifyEnabled);

        // 编辑为开启后再次读回
        loaded.NotifyEnabled = true;
        repo.Update(loaded);
        Assert.True(repo.Get(id)!.NotifyEnabled);
    }

    [Fact]
    public void 未指定NotifyEnabled插入_默认不勾选()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-notify-def-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var repo = new ReminderRepository(db);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var id = repo.Insert(new Reminder
        {
            Title = "默认提醒",
            TriggerTime = "09:00:00",
            Status = ReminderStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            // 不设置 NotifyEnabled → 模型默认 false（不勾选）
        });

        Assert.False(repo.Get(id)!.NotifyEnabled);
    }
}
