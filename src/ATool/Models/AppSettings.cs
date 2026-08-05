namespace ATool.Models;

/// <summary>设置快照（settings 表键值对的强类型视图）。</summary>
public sealed class AppSettings
{
    public string DataPath { get; set; } = "";
    public string LogPath { get; set; } = "";
    public int RefreshMinutes { get; set; } = 30; // 最低 5
    public bool AutoStart { get; set; }
}
