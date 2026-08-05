using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
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

    private double _curX, _curY;
    private double _targetX, _targetY;
    private bool _expanded;
    private bool _running;
    private Corner _corner = Corner.TopLeft;
    private int _hotStreak;

    /// <summary>窗口 DPI 缩放（高分屏下 DIP→物理像素换算；异常时按 1.0 兜底）。</summary>
    private double Scale()
    {
        try { return _window.RenderScaling > 0 ? _window.RenderScaling : 1.0; }
        catch { return 1.0; }
    }

    private double Phys(double dip) => dip * Scale();

    private const double Edge = 10;     // 常态露出的边缘条宽度（DIP）
    private const double HotZone = 20;  // 角落热区尺寸（DIP）
    private const double WindowW = 260; // 窗口宽（DIP）
    private const double WindowH = 320; // 窗口高（DIP）
    private const double MouseBuffer = 24; // 展开后鼠标离开浮窗的缓冲（DIP）
    private const int HotStreakThreshold = 2; // 热区连续命中 N 次（0.8s）才展开，防误触

    public FloatReminderService(SettingsService settings, ReminderRepository repo)
    {
        _settings = settings;
        _repo = repo;
        _window = new FloatReminderWindow();
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _pollTimer.Tick += (_, _) => Poll();
        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animTimer.Tick += (_, _) => TickAnimation();
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _refreshTimer.Tick += (_, _) => RefreshReminders();
    }

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
                return;
            }
            _corner = (Corner)_settings.GetFloatReminderCorner();
            RefreshReminders();
            _expanded = false;
            _hotStreak = 0;
            _window.Show();
            _window.Width = WindowW;
            _window.Height = WindowH;
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

    /// <summary>按当前角落计算浮窗目标位置（静态纯函数，可测）。expanded=true 为完全展开。</summary>
    public static (double X, double Y) ComputeTarget(Corner corner, double screenX, double screenY, double screenW, double screenH, double winW, double winH, double edge, bool expanded)
    {
        double x = corner switch
        {
            Corner.TopLeft or Corner.BottomLeft => expanded ? screenX : screenX - winW + edge,
            _ => expanded ? screenX + screenW - winW : screenX + screenW - edge,
        };
        double y = corner switch
        {
            Corner.TopLeft or Corner.TopRight => screenY,
            _ => screenY + screenH - winH,
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

    private void RefreshReminders()
    {
        try
        {
            var titles = _repo.GetAll(ReminderStatus.Pending)
                .OrderBy(r => r.TriggerTime)
                .Select(r => r.Title)
                .ToList();
            _window.SetReminders(titles);
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
            // 只在桌面显示：前台窗口不是桌面（Progman/WorkerW）→ 隐藏浮窗
            if (!IsDesktopForeground())
            {
                _window.Hide();
                return;
            }
            _window.Show();
            if (_window.Width != WindowW) { _window.Width = WindowW; _window.Height = WindowH; }

            var scr = Screen();
            GetCursorPos(out var pt);
            var inHot = InHotZone(_corner, pt.X, pt.Y, scr.Bounds.X, scr.Bounds.Y, scr.Bounds.Width, scr.Bounds.Height, Phys(HotZone));
            // 热区连续命中阈值后才展开（防误触：鼠标只是路过角落不弹）
            _hotStreak = inHot ? _hotStreak + 1 : 0;
            if (inHot && _hotStreak >= HotStreakThreshold && !_expanded)
            {
                _expanded = true;
                RefreshReminders();
                Animate();
            }
            else if (!inHot && _expanded && !MouseOverWindow(pt))
            {
                _expanded = false;
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
        var (tx, ty) = ComputeTarget(_corner, scr.Bounds.X, scr.Bounds.Y, scr.Bounds.Width, scr.Bounds.Height, Phys(WindowW), Phys(WindowH), Phys(Edge), _expanded);
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
        (_curX, _curY) = ComputeTarget(_corner, scr.Bounds.X, scr.Bounds.Y, scr.Bounds.Width, scr.Bounds.Height, Phys(WindowW), Phys(WindowH), Phys(Edge), expanded: false);
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

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>前台窗口是否为桌面（Progman / WorkerW）。句柄无效时按桌面处理（避免误隐藏）。</summary>
    private static bool IsDesktopForeground()
    {
        var h = GetForegroundWindow();
        if (h == IntPtr.Zero) return true;
        var sb = new StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        var cls = sb.ToString();
        return cls is "Progman" or "WorkerW";
    }
}
