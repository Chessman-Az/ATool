using ATool.Data;
using ATool.Models;
using Serilog;

namespace ATool.Services;

/// <summary>
/// 提醒调度器：System.Timers.Timer 每分钟扫描一次。
/// 休眠唤醒检测：now - _lastTick &gt; 90s → 补发窗口 (lastTick, now] 内所有触发点。
/// 触发后：单次提醒由弹窗服务决定完成/延迟；周期提醒累加 TriggeredCount。
/// </summary>
public sealed class ReminderSchedulerService : IDisposable
{
    private readonly ReminderRepository _repo;
    private readonly ReminderPopupService _popup;
    private readonly System.Timers.Timer _timer = new(TimeSpan.FromSeconds(60));
    private DateTime _lastTick = DateTime.Now;
    private readonly object _lock = new();

    public ReminderSchedulerService(ReminderRepository repo, ReminderPopupService popup)
    {
        _repo = repo;
        _popup = popup;
        _timer.Elapsed += (_, _) => Tick();
        _timer.AutoReset = true;
    }

    public void Start()
    {
        lock (_lock) _lastTick = DateTime.Now;
        _timer.Start();
        Log.Information("提醒调度器已启动（每分钟扫描）");
    }

    /// <summary>扫描一次：枚举窗口内触发点并弹窗。测试可直接调用。</summary>
    public void Tick()
    {
        List<(Reminder Reminder, int MissedCount)> due = [];
        var now = DateTime.Now;
        lock (_lock)
        {
            var suspended = now - _lastTick > TimeSpan.FromSeconds(90);
            foreach (var r in _repo.GetAll(ReminderStatus.Pending))
            {
                var points = ReminderScheduler.EnumerateTriggers(r, _lastTick, now);
                if (points.Count > 0)
                {
                    due.Add((r, points.Count));
                    if (r.RepeatType != RepeatType.Single)
                        _repo.IncrementTriggeredCount(r.Id);
                }
            }
            if (suspended && due.Count > 0)
                Log.Information("检测到休眠唤醒，补发 {Count} 条提醒", due.Count);
            _lastTick = now;
        }

        if (due.Count > 0)
        {
            foreach (var (r, missed) in due)
                Log.Information("提醒触发: {Title}（错过 {Missed} 次）", r.Title, missed);
            _popup.Show(due);
        }
    }

    public void Dispose() => _timer.Dispose();
}
