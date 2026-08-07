using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>
/// 机哥页 VM：左右布局——左分类（硬件信息 / 磁盘工具 / 其他工具），右对应页面。
/// 硬件信息：机型/系统/处理器/内存/运行时间；工具页：内置工具一键启动，未内置跳官方下载页。
/// </summary>
public partial class JiGeViewModel : ObservableObject
{
    private readonly HardwareInfoService _hardware;

    // ---- 分类单选 ----
    [ObservableProperty] private bool _isHardware = true;
    [ObservableProperty] private bool _isDiskTools;
    [ObservableProperty] private bool _isOtherTools;

    // ---- 硬件信息 ----
    public string Manufacturer => _hardware.Manufacturer;
    public string Model => _hardware.Model;
    public string OsName => _hardware.OsName;
    public string OsBuild => _hardware.OsBuild;
    public string Processor => _hardware.Processor;
    public string CpuCount => _hardware.CpuCount;

    /// <summary>CPU 最大频率（如 4500 MHz）。</summary>
    public string CpuClock => string.IsNullOrEmpty(_hardware.CpuMaxClock) ? "" : $"{_hardware.CpuMaxClock} MHz";

    public string TotalMemoryGb => _hardware.TotalMemoryGb;

    /// <summary>内存条列表（单条容量/频率/品牌/插槽）。</summary>
    public IReadOnlyList<string> MemoryModules => _hardware.MemoryModules;

    /// <summary>内存条数文案（如「共 2 根」；WMI 未取到时为空）。</summary>
    public string MemoryCountText => _hardware.MemoryModuleCount > 0 ? $"共 {_hardware.MemoryModuleCount} 根" : "";

    public string Uptime => _hardware.Uptime;
    public string Arch => _hardware.Arch;

    /// <summary>显卡列表（WMI）。</summary>
    public IReadOnlyList<string> Gpus => _hardware.Gpus;

    /// <summary>硬盘列表（WMI）。</summary>
    public IReadOnlyList<string> Disks => _hardware.Disks;

    /// <summary>显示器列表（WMI）。</summary>
    public IReadOnlyList<string> Monitors => _hardware.Monitors;

    /// <summary>网卡列表（WMI）。</summary>
    public IReadOnlyList<string> Nets => _hardware.Nets;

    /// <summary>磁盘工具列表。</summary>
    public ObservableCollection<ToolItemVm> DiskTools { get; } = [];

    /// <summary>其他工具列表。</summary>
    public ObservableCollection<ToolItemVm> OtherTools { get; } = [];

    public JiGeViewModel(HardwareInfoService hardware)
    {
        _hardware = hardware;
        foreach (var t in ToolCatalog.All.Where(t => t.Category == "disk"))
            DiskTools.Add(new ToolItemVm(t));
        foreach (var t in ToolCatalog.All.Where(t => t.Category == "other"))
            OtherTools.Add(new ToolItemVm(t));
    }
}

/// <summary>工具条目 VM：图标（exe 提取）+ 状态（已内置/未内置）+ 启动 / 打开官网命令。</summary>
public partial class ToolItemVm : ObservableObject
{
    public ToolEntry Entry { get; }

    public string Name => Entry.Name;
    public string Note => Entry.Note;
    public string StatusText => IsInstalled ? "✅ 已内置" : "⚠ 未内置";
    public bool IsInstalled => ToolCatalog.IsInstalled(Entry);

    /// <summary>软件图标（从可执行文件提取；提取失败或非 Windows 时为 null）。</summary>
    public Avalonia.Media.Imaging.Bitmap? Icon { get; }

    public ToolItemVm(ToolEntry entry)
    {
        Entry = entry;
        Icon = TryExtractIcon();
    }

    private Avalonia.Media.Imaging.Bitmap? TryExtractIcon()
    {
        var dir = ToolCatalog.ToolsDir;
        if (!OperatingSystem.IsWindows() || dir is null) return null;
        var path = Path.Combine(dir, Entry.Executable);
        if (!File.Exists(path)) return null;
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;
            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            return new Avalonia.Media.Imaging.Bitmap(ms);
        }
        catch
        {
            return null; // 图标提取失败（损坏/权限）——显示占位
        }
    }

    [RelayCommand]
    private void Launch()
    {
        var dir = ToolCatalog.ToolsDir;
        if (dir is null) return;
        var path = Path.Combine(dir, Entry.Executable);
        if (!File.Exists(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo(path) { WorkingDirectory = dir, UseShellExecute = true });
        }
        catch
        {
            // 启动失败（文件损坏/被占用）——静默，用户可点官网重下
        }
    }

    [RelayCommand]
    private void OpenOfficial()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Entry.OfficialUrl) { UseShellExecute = true });
        }
        catch
        {
            // 无默认浏览器——忽略
        }
    }
}
