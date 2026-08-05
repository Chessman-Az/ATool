using ATool.Data;
using ATool.Models;
using ATool.ViewModels;
using Xunit;

namespace ATool.Tests;

/// <summary>提醒列表「点击圆圈 = 完成」行为的回归锚点。</summary>
public class ReminderCompleteTests
{
    private static (Db Db, ReminderRepository Repo, ReminderListViewModel Vm) Create()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-test-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var repo = new ReminderRepository(db);
        var vm = new ReminderListViewModel(repo, new ReminderEditViewModel(repo));
        return (db, repo, vm);
    }

    private static Reminder NewReminder(string title = "测试提醒") => new()
    {
        Title = title,
        TriggerTime = "09:00:00",
        CreatedAt = "2026-08-05 10:00:00",
        UpdatedAt = "2026-08-05 10:00:00",
    };

    [Fact]
    public void Complete_标记为已完成_并从待提醒列表移除()
    {
        var (db, repo, vm) = Create();
        var id = repo.Insert(NewReminder());
        vm.Reload();
        var item = Assert.Single(vm.Items);

        vm.ToggleComplete(item);

        Assert.Equal(ReminderStatus.Done, repo.Get(id)!.Status);
        vm.Reload();
        Assert.DoesNotContain(vm.Items, i => i.Reminder.Id == id);
    }

    [Fact]
    public void ToggleComplete_完成后再点_还原为待提醒()
    {
        var (db, repo, vm) = Create();
        var id = repo.Insert(NewReminder());
        vm.Reload();
        var item = vm.Items.First();

        vm.ToggleComplete(item); // 完成
        Assert.Equal(ReminderStatus.Done, repo.Get(id)!.Status);

        vm.Reload(); // 待提醒列表已移除
        Assert.DoesNotContain(vm.Items, i => i.Reminder.Id == id);

        vm.FilterDoneCommand.Execute(null); // 切到已完成筛选
        var doneItem = vm.Items.First(i => i.Reminder.Id == id);
        vm.ToggleComplete(doneItem); // 还原为待提醒
        Assert.Equal(ReminderStatus.Pending, repo.Get(id)!.Status);
    }

    [Fact]
    public void FilterAll_包含待提醒与已完成()
    {
        var (db, repo, vm) = Create();
        var a = repo.Insert(NewReminder("A"));
        var b = repo.Insert(NewReminder("B"));
        repo.SetStatus(a, ReminderStatus.Done);
        vm.Reload(); // 默认待提醒筛选：只含 B
        Assert.Single(vm.Items);

        vm.FilterAllCommand.Execute(null);
        Assert.Equal(2, vm.Items.Count); // 全部：A(已完成) + B(待提醒)
    }

    [Fact]
    public void Complete_只影响目标提醒()
    {
        var (db, repo, vm) = Create();
        var a = repo.Insert(NewReminder("A"));
        var b = repo.Insert(NewReminder("B"));
        vm.Reload();

        vm.ToggleComplete(vm.Items.First(i => i.Reminder.Id == a));

        Assert.Equal(ReminderStatus.Done, repo.Get(a)!.Status);
        Assert.Equal(ReminderStatus.Pending, repo.Get(b)!.Status);
    }
}
