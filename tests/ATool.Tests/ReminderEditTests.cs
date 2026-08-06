using ATool.Data;
using ATool.Models;
using ATool.ViewModels;
using Xunit;

namespace ATool.Tests;

/// <summary>提醒编辑：触发时间时/分/秒三个下拉（默认当前时间）+ 保存格式。</summary>
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

        Assert.True(int.TryParse(vm.SelectedHour, out var hh) && hh is >= 0 and <= 23, $"SelectedHour 非法: {vm.SelectedHour}");
        Assert.True(int.TryParse(vm.SelectedMinute, out var mm) && mm is >= 0 and <= 59, $"SelectedMinute 非法: {vm.SelectedMinute}");
        Assert.True(int.TryParse(vm.SelectedSecond, out var ss) && ss is >= 0 and <= 59, $"SelectedSecond 非法: {vm.SelectedSecond}");
        var now = DateTime.Now;
        // 秒粒度比较（跨秒边界最多差 1 秒）
        var diffSec = Math.Abs((hh * 3600 + mm * 60 + ss) - (now.Hour * 3600 + now.Minute * 60 + now.Second));
        Assert.True(diffSec <= 1, $"默认时间 {hh:00}:{mm:00}:{ss:00} 与当前时间 {now:HH:mm:ss} 相差超过 1 秒");
    }

    [Fact]
    public void TimeOptions_时分秒选项数量正确且全部合法()
    {
        var vm = NewVm();
        Assert.Equal(24, vm.HourOptions.Count);
        Assert.Equal(60, vm.MinuteOptions.Count);
        Assert.Equal(60, vm.SecondOptions.Count);
        foreach (var t in vm.HourOptions)
            Assert.True(int.TryParse(t, out var h) && h is >= 0 and <= 23, $"非法小时项: {t}");
        foreach (var t in vm.MinuteOptions)
            Assert.True(int.TryParse(t, out var m) && m is >= 0 and <= 59, $"非法分钟项: {t}");
        foreach (var t in vm.SecondOptions)
            Assert.True(int.TryParse(t, out var s) && s is >= 0 and <= 59, $"非法秒项: {t}");
    }

    [Fact]
    public void Save_触发时间保存为选中时分秒()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-edit-save-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var repo = new ReminderRepository(db);
        var vm = new ReminderEditViewModel(repo);
        vm.BeginNew();

        vm.Title = "测试提醒";
        vm.SelectedHour = "14";
        vm.SelectedMinute = "35";
        vm.SelectedSecond = "07";
        vm.SaveCommand.Execute(null);

        var loaded = Assert.Single(repo.GetAll());
        Assert.Equal("14:35:07", loaded.TriggerTime);
    }

    [Fact]
    public void BeginEdit_回填时分秒()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-edit-load-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var repo = new ReminderRepository(db);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var id = repo.Insert(new Reminder
        {
            Title = "旧提醒",
            TriggerTime = "07:20:45",
            Status = ReminderStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        });
        var vm = new ReminderEditViewModel(repo);

        vm.BeginEdit(repo.Get(id)!);

        Assert.Equal("07", vm.SelectedHour);
        Assert.Equal("20", vm.SelectedMinute);
        Assert.Equal("45", vm.SelectedSecond);
    }
}
