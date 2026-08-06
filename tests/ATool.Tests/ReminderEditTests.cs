using ATool.Data;
using ATool.Models;
using ATool.ViewModels;
using Xunit;

namespace ATool.Tests;

/// <summary>提醒编辑：触发时间下拉选择（默认当前时间）+ 保存格式。</summary>
public class ReminderEditTests
{
    private static ReminderEditViewModel NewVm()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-edit-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        return new ReminderEditViewModel(new ReminderRepository(db));
    }

    [Fact]
    public void BeginNew_默认选中当前时间()
    {
        var vm = NewVm();
        vm.BeginNew();

        var ok = TimeOnly.TryParse(vm.SelectedTime, out var sel);
        Assert.True(ok, $"SelectedTime 应为 HH:mm 格式，实际: {vm.SelectedTime}");
        var now = TimeOnly.FromDateTime(DateTime.Now);
        // 分钟粒度比较（跨分钟边界最多差 1 分钟）
        var diffMin = Math.Abs((sel.Hour * 60 + sel.Minute) - (now.Hour * 60 + now.Minute));
        Assert.True(diffMin <= 1, $"默认时间 {vm.SelectedTime} 与当前时间 {now:HH:mm} 相差超过 1 分钟");
    }

    [Fact]
    public void TimeOptions_48项且全部为合法HHmm()
    {
        var vm = NewVm();
        Assert.Equal(48, vm.TimeOptions.Count);
        foreach (var t in vm.TimeOptions)
            Assert.True(TimeOnly.TryParse(t, out _), $"非法时间项: {t}");
    }

    [Fact]
    public void Save_触发时间保存为选中时间加秒()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-edit-save-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var repo = new ReminderRepository(db);
        var vm = new ReminderEditViewModel(repo);
        vm.BeginNew();

        vm.Title = "测试提醒";
        vm.SelectedTime = "14:35";
        vm.SaveCommand.Execute(null);

        var loaded = Assert.Single(repo.GetAll());
        Assert.Equal("14:35:00", loaded.TriggerTime);
    }

    [Fact]
    public void BeginEdit_回填选中时间()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-edit-load-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var repo = new ReminderRepository(db);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var id = repo.Insert(new Reminder
        {
            Title = "旧提醒",
            TriggerTime = "07:20:00",
            Status = ReminderStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        });
        var vm = new ReminderEditViewModel(repo);

        vm.BeginEdit(repo.Get(id)!);

        Assert.Equal("07:20", vm.SelectedTime);
    }
}
