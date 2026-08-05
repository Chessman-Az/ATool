using Serilog;
using ATool.Data;

namespace ATool.Services;

/// <summary>
/// 设置服务：数据/日志路径、自动刷新间隔、开机自启。
/// 路径迁移：复制全部文件 → 校验成功才更新配置（失败=配置不变=回滚）→ 删除旧目录（失败仅日志）。
/// </summary>
public sealed class SettingsService
{
    private readonly SettingsRepository _repo;
    private readonly Db _db;

    public string DataPath { get; private set; }
    public string LogPath { get; private set; }

    public SettingsService(SettingsRepository repo, Db db)
    {
        _repo = repo;
        _db = db;
        DataPath = _repo.Get("data_path") ?? DefaultDataPath();
        LogPath = _repo.Get("log_path") ?? DefaultLogPath();
        if (_repo.Get("data_path") is null)
        {
            _repo.Set("data_path", DataPath);
            _repo.Set("log_path", LogPath);
            _repo.Set("refresh_minutes", "30");
        }
        _db.ChangePath(DataPath);
        _db.InitializeSchema();
    }

    public static string DefaultDataPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATool", "data");

    public static string DefaultLogPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ATool", "logs");

    public int GetRefreshMinutes() =>
        int.TryParse(_repo.Get("refresh_minutes"), out var m) && m >= 5 ? m : 30;

    public void SetRefreshMinutes(int minutes)
    {
        if (minutes < 5) throw new ArgumentException("自动刷新间隔最低 5 分钟");
        _repo.Set("refresh_minutes", minutes.ToString());
    }

    public void SetLogPath(string newPath)
    {
        var full = Path.GetFullPath(newPath);
        Directory.CreateDirectory(full);
        _repo.Set("log_path", full);
        LogPath = full;
    }

    /// <summary>迁移数据目录。失败（复制异常/目标不可写）→ 抛异常，配置与运行路径不变。</summary>
    public void ChangeDataPath(string newPath)
    {
        var full = Path.GetFullPath(newPath);
        Directory.CreateDirectory(full);

        // 目标可写校验
        var probe = Path.Combine(full, $".probe_{Guid.NewGuid():N}");
        File.WriteAllText(probe, "probe");
        File.Delete(probe);

        // 复制旧目录全部文件（任一失败 → 抛异常，配置未动 = 回滚）
        if (Directory.Exists(DataPath) && Path.GetFullPath(DataPath) != full)
        {
            foreach (var file in Directory.GetFiles(DataPath))
                File.Copy(file, Path.Combine(full, Path.GetFileName(file)), overwrite: true);
        }

        var old = DataPath;
        _repo.Set("data_path", full);
        _db.ChangePath(full);
        _db.InitializeSchema();
        DataPath = full;
        Log.Information("数据目录已迁移: {Old} -> {New}", old, full);

        // 删除旧目录（失败仅日志，不阻塞）
        try
        {
            if (Directory.Exists(old) && Path.GetFullPath(old) != full)
                Directory.Delete(old, recursive: true);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "迁移后删除旧目录失败（可手动清理）: {Old}", old);
        }
    }

    public bool IsAutoStartEnabled() => AutoStartService.IsEnabled();
    public void SetAutoStart(bool enabled) => AutoStartService.SetEnabled(enabled);

    // ---- 桌面提醒浮窗 ----

    public bool GetFloatReminderEnabled() => _repo.Get("float_reminder_enabled") == "1";

    public void SetFloatReminderEnabled(bool enabled) => _repo.Set("float_reminder_enabled", enabled ? "1" : "0");

    /// <summary>浮窗角落：0=左上 1=右上 2=右下 3=左下（非法值回退左上）。</summary>
    public int GetFloatReminderCorner() =>
        int.TryParse(_repo.Get("float_reminder_corner"), out var c) && c is >= 0 and <= 3 ? c : 0;

    public void SetFloatReminderCorner(int corner) => _repo.Set("float_reminder_corner", corner.ToString());

    /// <summary>浮窗透明度（10-100，百分比；非法值回退 100）。</summary>
    public int GetFloatReminderOpacity() =>
        int.TryParse(_repo.Get("float_reminder_opacity"), out var o) && o is >= 10 and <= 100 ? o : 100;

    public void SetFloatReminderOpacity(int opacity) => _repo.Set("float_reminder_opacity", opacity.ToString());
}
