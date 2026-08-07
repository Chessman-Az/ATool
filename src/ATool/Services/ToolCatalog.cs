namespace ATool.Services;

/// <summary>机哥内置工具条目。</summary>
public sealed class ToolEntry
{
    /// <summary>显示名。</summary>
    public string Name { get; init; } = "";

    /// <summary>分类：disk（磁盘工具）/ other（其他工具）。</summary>
    public string Category { get; init; } = "disk";

    /// <summary>tools 目录下的可执行文件名（便携版）。</summary>
    public string Executable { get; init; } = "";

    /// <summary>官方下载页（保证正版来源）。</summary>
    public string OfficialUrl { get; init; } = "";

    /// <summary>一句话说明。</summary>
    public string Note { get; init; } = "";
}

/// <summary>
/// 机哥内置工具清单（全部官方免费可分发版本；tools 目录随软件发布，无需本机安装）。
/// 检测逻辑：可执行文件与 ATool.exe 同级 tools\ 子目录内。
/// </summary>
public static class ToolCatalog
{
    public static readonly ToolEntry[] All =
    [
        // ---- 磁盘工具 ----
        new() { Name = "CrystalDiskInfo", Category = "disk", Executable = "DiskInfo64.exe",
                OfficialUrl = "https://crystalmark.info/en/software/crystaldiskinfo/",
                Note = "硬盘健康度 / 温度 / 通电时间（MIT 开源）" },
        new() { Name = "HD Tune 2.55", Category = "disk", Executable = "hdtune.exe",
                OfficialUrl = "https://www.hdtune.com/",
                Note = "磁盘基准测试 / 错误扫描（官方免费版 2.55）" },
        new() { Name = "DiskGenius", Category = "disk", Executable = "DiskGenius.exe",
                OfficialUrl = "https://www.diskgenius.cn/",
                Note = "分区管理 / 数据恢复（官方免费版）" },
        new() { Name = "SpaceSniffer", Category = "disk", Executable = "SpaceSniffer.exe",
                OfficialUrl = "http://www.uderzo.it/main_products/space_sniffer/index.html",
                Note = "磁盘空间可视化分析（免费软件）" },
        // ---- 其他工具 ----
        new() { Name = "Geek Uninstaller", Category = "other", Executable = "geek.exe",
                OfficialUrl = "https://geekuninstaller.com/",
                Note = "强制卸载 / 清理残留（官方便携版）" },
        new() { Name = "Rufus", Category = "other", Executable = "rufus.exe",
                OfficialUrl = "https://rufus.ie/zh/",
                Note = "U 盘启动盘制作（开源 GPL）" },
        new() { Name = "Everything", Category = "other", Executable = "Everything.exe",
                OfficialUrl = "https://www.voidtools.com/zh-cn/",
                Note = "秒级全盘文件搜索（免费软件）" },
    ];

    /// <summary>tools 目录（ATool.exe 同级 tools\；单文件发布用 ProcessPath）。</summary>
    public static string? ToolsDir
    {
        get
        {
            try
            {
                var exeDir = Path.GetDirectoryName(Environment.ProcessPath)
                             ?? AppContext.BaseDirectory;
                var dir = Path.Combine(exeDir, "tools");
                return Directory.Exists(dir) ? dir : null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>条目是否已内置（文件存在）。</summary>
    public static bool IsInstalled(ToolEntry t)
    {
        var dir = ToolsDir;
        return dir is not null && File.Exists(Path.Combine(dir, t.Executable));
    }
}
