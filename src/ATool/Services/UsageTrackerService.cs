using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;
using ATool.Data;

namespace ATool.Services;

/// <summary>前台窗口采样分段纯逻辑（无 I/O，可单测）。</summary>
public static class TrackerSegmentLogic
{
    public enum Action { Skip, Flush, Switch }

    /// <summary>系统窗口类名（桌面/任务栏等，不记录）。</summary>
    public static bool IsSystemWindow(string? className) =>
        className is "Progman" or "WorkerW" or "Shell_TrayWnd";

    /// <summary>
    /// 决定动作：系统窗口/空进程 → Skip；首次采样 → Switch（开新段）；与上次一致 → Flush（续时）；否则 → Switch。
    /// 标题变化即视为切换（浏览器切标签页按网站分段）。
    /// </summary>
    public static Action Decide(string? curProcess, string? curTitle,
        string? prevProcess, string? prevTitle, bool isSystemWindow)
    {
        if (isSystemWindow || string.IsNullOrWhiteSpace(curProcess)) return Action.Skip;
        if (prevProcess is null) return Action.Switch;
        var same = string.Equals(curProcess, prevProcess, StringComparison.OrdinalIgnoreCase)
                && string.Equals(curTitle ?? "", prevTitle ?? "", StringComparison.Ordinal);
        return same ? Action.Flush : Action.Switch;
    }
}

/// <summary>
/// 使用时长采样服务：每 5s 轮询前台窗口（进程名 + 标题），变化即分段写入 usage_log；
/// 每 60s flush 当前段时长；退出时闭合最后一段。非 Windows 平台不启动。
/// </summary>
public sealed class UsageTrackerService : IDisposable
{
    private readonly UsageLogRepository _repo;
    private readonly System.Timers.Timer _timer = new(TimeSpan.FromSeconds(5));
    private readonly object _lock = new();

    private long? _currentId;
    private string? _curProcess;
    private string? _curTitle;
    private DateTime _segmentStart;
    private int _ticksSinceFlush;

    /// <summary>当前正在采样的前台活动（"进程 · 窗口标题"），供时间大师页实时显示。</summary>
    public string CurrentActivity { get; private set; } = "采样中…";

    public UsageTrackerService(UsageLogRepository repo)
    {
        _repo = repo;
        _timer.Elapsed += (_, _) => Tick();
        _timer.AutoReset = true;
    }

    /// <summary>启动采样（Windows 守卫；启动时清理 90 天前的旧记录）。</summary>
    public void Start()
    {
        if (!OperatingSystem.IsWindows())
        {
            Log.Information("时间大师：非 Windows 平台，采样未启动");
            return;
        }
        _timer.Start();
        Log.Information("时间大师采样已启动（5 秒轮询前台窗口）");
        try { _repo.DeleteBefore(90); } catch (Exception ex) { Log.Warning(ex, "时间大师旧数据清理失败"); }
    }

    /// <summary>采样一次（测试可直接调用）。</summary>
    public void Tick()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        var pid = GetWindowThreadProcessId(hwnd, out _);
        if (pid == (uint)Environment.ProcessId) return; // 自己（弹窗/浮窗）不算使用

        var process = GetProcessName(pid);
        var title = GetWindowText(hwnd);
        var className = GetClassName(hwnd);
        var isSystem = TrackerSegmentLogic.IsSystemWindow(className);

        // 诊断：每 60 个 tick（5 分钟）记录一次前台窗口状态，定位真实环境不写库问题
        if (_ticksSinceFlush % 60 == 0)
            Serilog.Log.Information("时间大师采样诊断: pid={Pid} proc=[{Proc}] title=[{Title}] class=[{Class}] system={Sys}",
                pid, process, title, className, isSystem);

        var now = DateTime.Now;
        lock (_lock)
        {
            var action = TrackerSegmentLogic.Decide(process, title, _curProcess, _curTitle, isSystem);
            switch (action)
            {
                case TrackerSegmentLogic.Action.Switch:
                    CloseCurrent(now);
                    _curProcess = process;
                    _curTitle = title;
                    _segmentStart = now;
                    _currentId = _repo.Insert(process, title, AppUsageCategorizer.Categorize(process), now);
                    _ticksSinceFlush = 0;
                    CurrentActivity = string.IsNullOrWhiteSpace(title) ? process : $"{process} · {title}";
                    break;

                case TrackerSegmentLogic.Action.Flush:
                    // 每 12 tick（60s）刷新当前段时长
                    _ticksSinceFlush++;
                    if (_ticksSinceFlush >= 12) { FlushCurrent(now); _ticksSinceFlush = 0; }
                    break;
            }
        }
    }

    private void CloseCurrent(DateTime now)
    {
        if (_currentId is { } id)
        {
            _repo.CloseSegment(id, now, (int)(now - _segmentStart).TotalSeconds);
            _currentId = null;
        }
    }

    private void FlushCurrent(DateTime now)
    {
        if (_currentId is { } id)
            _repo.CloseSegment(id, now, (int)(now - _segmentStart).TotalSeconds);
    }

    public void Dispose()
    {
        _timer.Dispose();
        try { lock (_lock) CloseCurrent(DateTime.Now); } catch { /* 退出时尽力闭合 */ }
    }

    // ---- Win32 互操作（照 FloatReminderService 模式）----

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    private static string GetWindowText(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        return GetWindowText(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
    }

    private static string GetClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        return GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
    }

    private static string GetProcessName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return ""; // 进程已退出（竞态）
        }
    }
}
