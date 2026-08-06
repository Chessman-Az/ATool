using ATool.Data;
using ATool.Models;
using ATool.ViewModels;
using ATool.Views;
using Avalonia.Threading;

namespace ATool.Services;

/// <summary>一次弹窗中的提醒项（含错过次数）。</summary>
public sealed record ReminderPopupItem(Reminder Reminder, int MissedCount);

/// <summary>
/// 提醒弹窗服务：聚合同时触发的多条提醒为一个置顶窗口；
/// 提供 完成 / 延迟（本次不影响周期）动作。
/// 单次提醒：弹窗关闭即视为完成（防无限弹）。
/// </summary>
public sealed class ReminderPopupService
{
    private readonly ReminderRepository _repo;
    private readonly ToastService _toast;

    public ReminderPopupService(ReminderRepository repo, ToastService toast)
    {
        _repo = repo;
        _toast = toast;
    }

    public void Show(IReadOnlyList<(Reminder Reminder, int MissedCount)> items)
    {
        // 备用渠道：Windows Toast（锁屏可见）；失败静默降级
        foreach (var (r, missed) in items)
            _toast.Show(r.Title, missed > 1 ? $"提醒触发（错过 {missed} 次）" : "提醒触发");
        // 窗口创建与显示必须在 UI 线程（调度器在 Timer 线程触发 Tick，直接 Show 会静默失败）
        Dispatcher.UIThread.Post(() =>
        {
            var vm = new ReminderPopupViewModel(this, items.Select(x => new ReminderPopupItem(x.Reminder, x.MissedCount)).ToList());
            var window = new ReminderPopupWindow { DataContext = vm };
            // 关闭窗口时：未处理的单次提醒视为完成
            window.Closed += (_, _) =>
            {
                foreach (var item in items)
                {
                    if (item.Reminder.RepeatType == RepeatType.Single && item.Reminder.Status != ReminderStatus.Done)
                        Complete(item.Reminder);
                }
            };
            window.Show();
        });
    }

    /// <summary>完成：单次→标记 Done；周期→无额外动作（TriggeredCount 已由调度器累加）。</summary>
    public void Complete(Reminder r)
    {
        if (r.Status != ReminderStatus.Done)
        {
            _repo.SetStatus(r.Id, ReminderStatus.Done);
            r.Status = ReminderStatus.Done;
            Log("完成", r);
        }
    }

    /// <summary>延迟：写入 snooze_until，本次跳过，周期属性不变。</summary>
    public void Snooze(Reminder r, int minutes)
    {
        var until = DateTime.Now.AddMinutes(minutes);
        _repo.SetSnooze(r.Id, until);
        r.SnoozeUntil = until.ToString("yyyy-MM-dd HH:mm:ss");
        Log($"延迟 {minutes} 分钟", r);
    }

    private static void Log(string action, Reminder r) =>
        Serilog.Log.Information("提醒{Action}: {Title}", action, r.Title);
}
