using ATool.Data;
using ATool.Models;
using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>桌面提醒浮窗：角落位置计算（纯函数）+ 设置读写（临时目录，不污染真实数据）。</summary>
public class FloatReminderTests
{
    private const double W = 260, H = 320;
    private const double SX = 0, SY = 0, SW = 1920, SH = 1080;

    [Theory]
    [InlineData(FloatReminderService.Corner.TopLeft, false, -260, 0)]      // 缩回完全缩进屏幕外（Edge=0）
    [InlineData(FloatReminderService.Corner.TopLeft, true, 0, 0)]
    [InlineData(FloatReminderService.Corner.TopRight, false, 1920, 0)]
    [InlineData(FloatReminderService.Corner.TopRight, true, 1660, 0)]
    [InlineData(FloatReminderService.Corner.BottomRight, false, 1920, 760)]
    [InlineData(FloatReminderService.Corner.BottomRight, true, 1660, 760)]
    [InlineData(FloatReminderService.Corner.BottomLeft, false, -260, 760)]
    [InlineData(FloatReminderService.Corner.BottomLeft, true, 0, 760)]
    public void ComputeTarget_四角落缩回与展开位置(FloatReminderService.Corner corner, bool expanded, double ex, double ey)
    {
        var (x, y) = FloatReminderService.ComputeTarget(corner, SX, SY, SW, SH, W, H, 0, 0, expanded);
        Assert.Equal(ex, x, 3);
        Assert.Equal(ey, y, 3);
    }

    [Theory]
    [InlineData(FloatReminderService.Corner.TopLeft, 10, 10, true)]
    [InlineData(FloatReminderService.Corner.TopLeft, 100, 100, false)]
    [InlineData(FloatReminderService.Corner.TopRight, 1910, 10, true)]
    [InlineData(FloatReminderService.Corner.TopRight, 1800, 100, false)]
    [InlineData(FloatReminderService.Corner.BottomRight, 1910, 1070, true)]
    [InlineData(FloatReminderService.Corner.BottomRight, 1070, 1070, false)]
    [InlineData(FloatReminderService.Corner.BottomLeft, 10, 1070, true)]
    [InlineData(FloatReminderService.Corner.BottomLeft, 100, 1070, false)]
    public void InHotZone_仅对应角落热区命中(FloatReminderService.Corner corner, double mx, double my, bool expected)
    {
        Assert.Equal(expected, FloatReminderService.InHotZone(corner, mx, my, SX, SY, SW, SH, 20));
    }

    [Fact]
    public void 浮窗设置读写_默认关闭且角落为左上()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-float-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var repo = new SettingsRepository(db);
        repo.Set("data_path", dir); // 防止 SettingsService 构造时切到用户真实默认路径
        repo.Set("log_path", Path.Combine(dir, "logs"));
        var settings = new SettingsService(repo, db);

        Assert.False(settings.GetFloatReminderEnabled());
        Assert.Equal(0, settings.GetFloatReminderCorner());

        settings.SetFloatReminderEnabled(true);
        settings.SetFloatReminderCorner(2);
        Assert.True(settings.GetFloatReminderEnabled());
        Assert.Equal(2, settings.GetFloatReminderCorner());

        settings.SetFloatReminderCorner(9); // 非法值
        Assert.Equal(0, settings.GetFloatReminderCorner());

        // 透明度：默认 100，合法区间 0-100，非法回退
        Assert.Equal(100, settings.GetFloatReminderOpacity());
        settings.SetFloatReminderOpacity(60);
        Assert.Equal(60, settings.GetFloatReminderOpacity());
        settings.SetFloatReminderOpacity(0);
        Assert.Equal(0, settings.GetFloatReminderOpacity());
        settings.SetFloatReminderOpacity(101);
        Assert.Equal(100, settings.GetFloatReminderOpacity());
    }

    [Fact]
    public void 浮窗标记完成_待办列表移除该提醒()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-float-done-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var repo = new ReminderRepository(db);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var id = repo.Insert(new Reminder
        {
            Title = "浮窗测试提醒",
            TriggerTime = "09:00:00",
            Status = ReminderStatus.Pending,
            CreatedAt = now,
        });

        Assert.Single(repo.GetAll(ReminderStatus.Pending));

        repo.SetStatus(id, ReminderStatus.Done); // 浮窗圆圈点击 → 标记完成

        Assert.Empty(repo.GetAll(ReminderStatus.Pending));
    }

    [Fact]
    public void 浮窗展示范围设置_默认仅未完成()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atool-float-scope-" + Guid.NewGuid().ToString("N"));
        var db = new Db(dir);
        db.InitializeSchema();
        var repo = new SettingsRepository(db);
        repo.Set("data_path", dir);
        repo.Set("log_path", Path.Combine(dir, "logs"));
        var settings = new SettingsService(repo, db);

        Assert.Equal(0, settings.GetFloatReminderScope()); // 默认仅未完成
        settings.SetFloatReminderScope(1);
        Assert.Equal(1, settings.GetFloatReminderScope());
        settings.SetFloatReminderScope(2); // 非法值
        Assert.Equal(0, settings.GetFloatReminderScope());
    }

    [Fact]
    public void FilterScope_全部模式含已完成_未完成模式仅待办()
    {
        var pending = new Reminder { Title = "p", Status = ReminderStatus.Pending };
        var done = new Reminder { Title = "d", Status = ReminderStatus.Done };
        var all = new[] { pending, done };

        Assert.Equal(2, FloatReminderService.FilterScope(all, 1).Count()); // 全部
        var only = FloatReminderService.FilterScope(all, 0).ToList();      // 仅未完成
        Assert.Single(only);
        Assert.Equal("p", only[0].Title);
    }

    [Theory]
    [InlineData(123u, "Progman", 456u, true)]   // 桌面
    [InlineData(123u, "WorkerW", 456u, true)]   // 桌面（Win11）
    [InlineData(123u, "Shell_TrayWnd", 456u, true)] // 任务栏（最小化后点任务栏回到桌面环境）
    [InlineData(456u, "ATool", 456u, true)]     // 本进程窗口（主窗口/浮窗自身）→ 不隐藏
    [InlineData(123u, "Chrome_WidgetWin_1", 456u, false)] // 其他软件 → 隐藏
    [InlineData(0u, "", 456u, true)]            // 无前台窗口 → 按桌面处理
    public void IsForegroundVisible_桌面或本进程可见_其他软件隐藏(uint fgPid, string cls, uint ownPid, bool expected)
    {
        Assert.Equal(expected, FloatReminderService.IsForegroundVisible(fgPid, cls, ownPid));
    }
}
