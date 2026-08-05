using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Serilog;
using ATool.Data;
using ATool.Models;
using ATool.Views;

namespace ATool.Services;

/// <summary>
/// 桌面提醒浮窗：屏幕角落热区（左上/右上/右下/左下）悬停弹出全部待办提醒，鼠标移走缩回。
/// 只在桌面显示——前台窗口为桌面（Progman/WorkerW）时才显示，打开其他软件自动隐藏。
/// </summary>
public sealed class FloatReminderService
{
    /// <summary>浮窗角落位置（与设置持久化的 int 一一对应）。</summary>
    public enum Corner { TopLeft = 0, TopRight = 1, BottomRight = 2, BottomLeft = 3 }

    private readonly SettingsService _settings;
    private readonly ReminderRepository _repo;
    private readonly FloatReminderWindow _window;
    private readonly DispatcherTimer _pollTimer;   // 前台窗口 + 鼠标热区轮询
    private readonly DispatcherTimer _animTimer;   // 展开/缩回位置插值
    private readonly DispatcherTimer _refreshTimer; // 提醒列表定时刷新
    private Window? _mainWindow;                   // 主窗口（全屏/最大化时隐藏浮窗）

    private double _curX, _curY;
    private double _targetX, _targetY;
    private bool _expanded;
    private bool _running;
    private bool _visible;
    private Corner _corner = Corner.TopLeft;
    private int _scope; // 0=仅未完成 1=全部

    /// <summary>窗口 DPI 缩放（高分屏下 DIP→物理像素换算；异常时按 1.0 兜底）。</summary>
    private double Scale()
    {
        try { return _window.RenderScaling > 0 ? _window.RenderScaling : 1.0; }
        catch { return 1.0; }
    }

    private double Phys(double dip) => dip * Scale();

    private const double Edge = 10;     // 常态露出的边缘条宽度（DIP）
    private const double ScreenMargin = 0; // 展开后浮窗与屏幕边缘的间距（DIP，当前贴边）
    private const double HotZone = 32;  // 角落热区尺寸（DIP）
    private const double WindowW = 260; // 窗口宽（DIP）
    private const double WindowH = 320; // 窗口高（DIP）
    private const double MouseBuffer = 24; // 展开后鼠标离开浮窗的缓冲（DIP）
    private const double RetractBuffer = 6; // 缩回细条附近的展开缓冲（DIP）

    public FloatReminderService(SettingsService settings, ReminderRepository repo)
    {
        _settings = settings;
        _repo = repo;
        _window = new FloatReminderWindow();
        _window.CompleteRequested += OnCompleteRequested;
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _pollTimer.Tick += (_, _) => Poll();
        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animTimer.Tick += (_, _) => TickAnimation();
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _refreshTimer.Tick += (_, _) => RefreshReminders();
    }

    /// <summary>点击待办圆圈 → 切换完成状态（未完成→完成；全部模式下已完成→恢复待办）并刷新列表。</summary>
    private void OnCompleteRequested(long reminderId)
    {
        try
        {
            var r = _repo.GetAll().FirstOrDefault(x => x.Id == reminderId);
            if (r is null) return;
            var newStatus = r.Status == ReminderStatus.Done ? ReminderStatus.Pending : ReminderStatus.Done;
            _repo.SetStatus(reminderId, newStatus);
            Log.Information("浮窗切换完成状态: Reminder#{Id} -> {Status}", reminderId, newStatus);
            RefreshReminders();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "浮窗切换完成状态失败: Reminder#{Id}", reminderId);
        }
    }

    /// <summary>绑定主窗口（用于检测全屏/最大化状态）。</summary>
    public void SetMainWindow(Window w) => _mainWindow = w;

    /// <summary>应用当前设置（启动时 / 保存设置后调用）。</summary>
    public void Apply()
    {
        try
        {
            if (!_settings.GetFloatReminderEnabled())
            {
                _running = false;
                _pollTimer.Stop();
                _animTimer.Stop();
                _refreshTimer.Stop();
                _window.Hide();
                _visible = false;
                return;
            }
            _corner = (Corner)_settings.GetFloatReminderCorner();
            _scope = _settings.GetFloatReminderScope();
            RefreshReminders();
            _expanded = false;
            _window.Show();
            _visible = true;
            _window.EnsureHiddenFromTaskbar(); // 任务栏隐藏兜底（Win32 工具窗口样式）
            _window.Width = WindowW;
            _window.Height = WindowH;
            // 透明度只作用于背景，文字保持不透明
            _window.ApplyBackgroundOpacity(_settings.GetFloatReminderOpacity() / 100.0);
            PlaceWindow();
            _running = true;
            _pollTimer.Start();
            _refreshTimer.Start();
            Log.Information("桌面提醒浮窗已开启: 角落={Corner} 缩放={Scale:F2}", _corner, Scale());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "浮窗启用失败（不影响主程序）");
            _running = false;
        }
    }

    /// <summary>按当前角落计算浮窗目标位置（静态纯函数，可测）。expanded=true 为完全展开（与屏幕边缘留 margin 间距）。</summary>
    public static (double X, double Y) ComputeTarget(Corner corner, double screenX, double screenY, double screenW, double screenH, double winW, double winH, double edge, double margin, bool expanded)
    {
        double x = corner switch
        {
            Corner.TopLeft or Corner.BottomLeft => expanded ? screenX + margin : screenX - winW + edge,
            _ => expanded ? screenX + screenW - winW - margin : screenX + screenW - edge,
        };
        double y = corner switch
        {
            Corner.TopLeft or Corner.TopRight => expanded ? screenY + margin : screenY,
            _ => expanded ? screenY + screenH - winH - margin : screenY + screenH - winH,
        };
        return (x, y);
    }

    /// <summary>鼠标是否在角落热区内（静态纯函数，可测）。</summary>
    public static bool InHotZone(Corner corner, double mx, double my, double screenX, double screenY, double screenW, double screenH, double hot)
        => corner switch
        {
            Corner.TopLeft => mx >= screenX && mx <= screenX + hot && my >= screenY && my <= screenY + hot,
            Corner.TopRight => mx >= screenX + screenW - hot && mx <= screenX + screenW && my >= screenY && my <= screenY + hot,
            Corner.BottomRight => mx >= screenX + screenW - hot && mx <= screenX + screenW && my >= screenY + screenH - hot && my <= screenY + screenH,
            _ => mx >= screenX && mx <= screenX + hot && my >= screenY + screenH - hot && my <= screenY + screenH,
        };

    /// <summary>浮窗展示范围过滤（静态纯函数，可测）：scope=1 全部，否则仅未完成。</summary>
    public static IEnumerable<Reminder> FilterScope(IEnumerable<Reminder> all, int scope)
        => scope == 1 ? all : all.Where(r => r.Status == ReminderStatus.Pending);

    private void RefreshReminders()
    {
        try
        {
            var items = FilterScope(_repo.GetAll(), _scope)
                .OrderBy(r => r.TriggerTime)
                .Select(r => new FloatReminderItem(r.Id, r.Title, r.Status == ReminderStatus.Done))
                .ToList();
            _window.SetReminders(items);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "浮窗提醒列表刷新失败");
        }
    }

    private void Poll()
    {
        if (!_running) return;
        try
        {
            // 主窗口全屏/最大化 → 隐藏浮窗（避免被遮挡的半截框显示在角落）
            if (_mainWindow is { WindowState: WindowState.Maximized or WindowState.FullScreen })
            {
                if (_visible) { _window.Hide(); _visible = false; }
                return;
            }
            // 前台是本进程窗口（主窗口/浮窗自身）、桌面或任务栏 → 显示；其他软件 → 隐藏
            var visible = IsDesktopForeground();
            if (visible && !_visible)
            {
                _window.Show();
                _visible = true;
            }
            else if (!visible && _visible)
            {
                _window.Hide();
                _visible = false;
            }
            if (!visible) return;

            var scr = Screen();
            GetCursorPos(out var pt);
            // 热区 = 屏幕对应角落 ∪ 浮窗缩回可见细条（鼠标移到细条上立即展开）
            var inHot = InHotZone(_corner, pt.X, pt.Y, scr.Bounds.X, scr.Bounds.Y, scr.Bounds.Width, scr.Bounds.Height, Phys(HotZone))
                        || MouseOverRetracted(pt);
            if (inHot && !_expanded)
            {
                _expanded = true;
                Log.Information("浮窗展开: 角落={Corner} 鼠标=({X},{Y})", _corner, pt.X, pt.Y);
                RefreshReminders();
                Animate();
            }
            else if (!inHot && _expanded && !MouseOverWindow(pt))
            {
                _expanded = false;
                Log.Information("浮窗缩回");
                Animate();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "浮窗轮询异常（已忽略）");
        }
    }

    private void Animate()
    {
        var scr = Screen();
        var (tx, ty) = ComputeTarget(_corner, scr.Bounds.X, scr.Bounds.Y, scr.Bounds.Width, scr.Bounds.Height, Phys(WindowW), Phys(WindowH), Phys(Edge), Phys(ScreenMargin), _expanded);
        _targetX = tx;
        _targetY = ty;
        _animTimer.Start();
    }

    private void TickAnimation()
    {
        _curX += (_targetX - _curX) * 0.25;
        _curY += (_targetY - _curY) * 0.25;
        if (Math.Abs(_targetX - _curX) < 1 && Math.Abs(_targetY - _curY) < 1)
        {
            _curX = _targetX;
            _curY = _targetY;
            _animTimer.Stop();
        }
        _window.Position = new PixelPoint((int)_curX, (int)_curY);
    }

    /// <summary>直接定位（无动画）到缩回位置。</summary>
    private void PlaceWindow()
    {
        var scr = Screen();
        (_curX, _curY) = ComputeTarget(_corner, scr.Bounds.X, scr.Bounds.Y, scr.Bounds.Width, scr.Bounds.Height, Phys(WindowW), Phys(WindowH), Phys(Edge), Phys(ScreenMargin), expanded: false);
        _targetX = _curX;
        _targetY = _curY;
        _window.Position = new PixelPoint((int)_curX, (int)_curY);
    }

    private bool MouseOverWindow(POINT pt)
    {
        var w = Phys(WindowW);
        var h = Phys(WindowH);
        var buf = Phys(MouseBuffer);
        return pt.X >= _curX - buf && pt.X <= _curX + w + buf
            && pt.Y >= _curY - buf && pt.Y <= _curY + h + buf;
    }

    /// <summary>鼠标是否在浮窗缩回时的可见区域（屏幕内的细条 + 小缓冲）上——移到细条上即展开。</summary>
    private bool MouseOverRetracted(POINT pt)
    {
        var scr = Screen();
        var (rx, ry) = ComputeTarget(_corner, scr.Bounds.X, scr.Bounds.Y, scr.Bounds.Width, scr.Bounds.Height, Phys(WindowW), Phys(WindowH), Phys(Edge), Phys(ScreenMargin), expanded: false);
        var buf = Phys(RetractBuffer);
        var left = Math.Max(scr.Bounds.X, rx) - buf;
        var right = Math.Min(scr.Bounds.X + scr.Bounds.Width, rx + Phys(WindowW)) + buf;
        var top = Math.Max(scr.Bounds.Y, ry) - buf;
        var bottom = Math.Min(scr.Bounds.Y + scr.Bounds.Height, ry + Phys(WindowH)) + buf;
        return pt.X >= left && pt.X <= right && pt.Y >= top && pt.Y <= bottom;
    }

    private Avalonia.Platform.Screen Screen()
        => _window.Screens.Primary ?? _window.Screens.All.FirstOrDefault()
           ?? throw new InvalidOperationException("无可用屏幕");

    // ---- Win32：前台窗口检测 + 全局鼠标位置 ----

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>前台窗口是否可见浮窗（静态纯函数，可测）：本进程窗口（主窗口/浮窗自身）、桌面（Progman/WorkerW）或任务栏（Shell_TrayWnd）→ 可见；其他软件 → 隐藏。</summary>
    public static bool IsForegroundVisible(uint fgPid, string fgClass, uint ownPid)
        => fgPid == 0 || fgPid == ownPid || fgClass is "Progman" or "WorkerW" or "Shell_TrayWnd";

    /// <summary>前台窗口是否允许浮窗显示（Win32 实读 + 纯函数判定）。</summary>
    private static bool IsDesktopForeground()
    {
        var h = GetForegroundWindow();
        if (h == IntPtr.Zero) return true;
        GetWindowThreadProcessId(h, out var pid);
        var sb = new StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        return IsForegroundVisible(pid, sb.ToString(), GetCurrentProcessId());
    }
}
