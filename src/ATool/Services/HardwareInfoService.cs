using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ATool.Services;

/// <summary>硬件/系统信息（注册表 + 原生 API，无额外依赖）：机型、系统版本、处理器、内存、运行时间。</summary>
public sealed class HardwareInfoService
{
    /// <summary>电脑品牌（如 LENOVO / Dell Inc.）。</summary>
    public string Manufacturer => ReadRegistry(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemManufacturer", "未知");

    /// <summary>电脑型号（如 ThinkPad X1 Carbon / Inspiron 14）。</summary>
    public string Model => ReadRegistry(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName", "未知");

    /// <summary>系统名称 + 版本（如 Windows 11 专业版 24H2）。</summary>
    public string OsName
    {
        get
        {
            var name = ReadRegistry(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "Windows");
            var build = ReadRegistry(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild", "");
            var display = ReadRegistry(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion", "");
            var edition = ReadRegistry(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "EditionID", "");
            // 微软兼容性设计：Win11 的 ProductName 仍返回 "Windows 10 Pro"——按构建号（>=22000）修正
            if (int.TryParse(build, out var b) && b >= 22000)
                name = name.Replace("Windows 10", "Windows 11");
            var parts = new List<string> { name };
            if (!string.IsNullOrEmpty(edition)) parts.Add(edition.Replace("Professional", "专业版").Replace("Home", "家庭版"));
            if (!string.IsNullOrEmpty(display)) parts.Add(display);
            return string.Join(" ", parts);
        }
    }

    /// <summary>系统构建号（如 26100）。</summary>
    public string OsBuild => ReadRegistry(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild", "");

    /// <summary>处理器名称（如 13th Gen Intel(R) Core(TM) i7-13700H）。</summary>
    public string Processor => ReadRegistry(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "未知");

    /// <summary>逻辑处理器数（如 16 核 20 线程显示核数×2）。</summary>
    public string CpuCount => $"{Environment.ProcessorCount / 2} 核 {Environment.ProcessorCount} 线程";

    /// <summary>物理内存总量（GB）。</summary>
    public string TotalMemoryGb
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var st = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
                if (GlobalMemoryStatusEx(ref st))
                    return $"{st.ullTotalPhys / 1024.0 / 1024.0 / 1024.0:F1} GB";
            }
            return "未知";
        }
    }

    /// <summary>内存条列表（WMI Win32_PhysicalMemory：单条容量/频率/品牌/型号）。</summary>
    public List<string> MemoryModules { get; } = [];

    /// <summary>内存条数（0 表示 WMI 未取到）。</summary>
    public int MemoryModuleCount => MemoryModules.Count;

    /// <summary>系统运行时间（天/时/分）。</summary>
    public string Uptime
    {
        get
        {
            var ts = TimeSpan.FromMilliseconds(Environment.TickCount64);
            return $"{(int)ts.TotalDays} 天 {ts.Hours} 小时 {ts.Minutes} 分";
        }
    }

    /// <summary>操作系统位数（64 位 / 32 位）。</summary>
    public string Arch => Environment.Is64BitOperatingSystem ? "64 位" : "32 位";

    /// <summary>显卡列表（WMI Win32_VideoController：名称/显存/驱动）。</summary>
    public List<string> Gpus { get; } = [];

    /// <summary>硬盘列表（WMI Win32_DiskDrive：型号/容量/接口）。</summary>
    public List<string> Disks { get; } = [];

    /// <summary>显示器列表（WMI Win32_DesktopMonitor：名称/分辨率）。</summary>
    public List<string> Monitors { get; } = [];

    /// <summary>网卡列表（WMI Win32_NetworkAdapter：物理网卡/已连接）。</summary>
    public List<string> Nets { get; } = [];

    /// <summary>CPU 最大频率（MHz，注册表）。</summary>
    public string CpuMaxClock => ReadRegistry(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz", "");

    public HardwareInfoService()
    {
        if (!OperatingSystem.IsWindows()) return;
        try { QueryWmi(); } catch { /* WMI 不可用时静默降级（仅基础信息） */ }
    }

    /// <summary>WMI 查询显卡/硬盘/显示器（非 Windows 或权限不足时跳过）。</summary>
    private void QueryWmi()
    {
        try
        {
            using var mos = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController");
            foreach (var o in mos.Get())
            {
                var name = o["Name"]?.ToString() ?? "未知显卡";
                var ramMb = Convert.ToDouble(o["AdapterRAM"] ?? 0d) / 1024 / 1024;
                var ver = o["DriverVersion"]?.ToString() ?? "";
                Gpus.Add(ramMb >= 1 ? $"{name}（{ramMb:F0} MB · 驱动 {ver}）" : name);
            }
        }
        catch { /* 单类查询失败不影响其他 */ }

        try
        {
            using var mos = new ManagementObjectSearcher(
                "SELECT Model, Size, InterfaceType FROM Win32_DiskDrive");
            foreach (var o in mos.Get())
            {
                var model = o["Model"]?.ToString() ?? "未知硬盘";
                var sizeGb = Convert.ToDouble(o["Size"] ?? 0d) / 1024 / 1024 / 1024;
                var iface = o["InterfaceType"]?.ToString() ?? "";
                Disks.Add(sizeGb >= 1 ? $"{model}（{sizeGb:F0} GB · {iface}）" : model);
            }
        }
        catch { }

        try
        {
            using var mos = new ManagementObjectSearcher(
                "SELECT Capacity, Speed, Manufacturer, PartNumber, BankLabel FROM Win32_PhysicalMemory");
            foreach (var o in mos.Get())
            {
                var capacityGb = Convert.ToDouble(o["Capacity"] ?? 0d) / 1024 / 1024 / 1024;
                var speed = o["Speed"]?.ToString() ?? "";
                var manufacturer = o["Manufacturer"]?.ToString() ?? "";
                var part = o["PartNumber"]?.ToString() ?? "";
                var bank = o["BankLabel"]?.ToString() ?? "";
                if (capacityGb < 1) continue;
                var parts = new List<string> { $"{capacityGb:F0} GB" };
                if (!string.IsNullOrEmpty(speed)) parts.Add($"{speed} MHz");
                if (!string.IsNullOrEmpty(manufacturer) && !manufacturer.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                    parts.Add(manufacturer.Trim());
                if (!string.IsNullOrEmpty(part) && !part.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                    parts.Add(part.Trim());
                if (!string.IsNullOrEmpty(bank) && !bank.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                    parts.Add($"插槽 {bank.Trim()}");
                MemoryModules.Add(string.Join(" · ", parts));
            }
        }
        catch { /* 单类查询失败不影响其他 */ }

        try
        {
            using var mos = new ManagementObjectSearcher(
                "SELECT Name, ScreenWidth, ScreenHeight FROM Win32_DesktopMonitor");
            foreach (var o in mos.Get())
            {
                var name = o["Name"]?.ToString() ?? "未知显示器";
                var w = o["ScreenWidth"]?.ToString() ?? "";
                var h = o["ScreenHeight"]?.ToString() ?? "";
                Monitors.Add(string.IsNullOrEmpty(w) ? name : $"{name}（{w}×{h}）");
            }
        }
        catch { }

        try
        {
            using var mos = new ManagementObjectSearcher(
                "SELECT Name, Speed FROM Win32_NetworkAdapter WHERE PhysicalAdapter = true AND NetConnectionStatus = 2");
            foreach (var o in mos.Get())
            {
                var name = o["Name"]?.ToString() ?? "未知网卡";
                var speedMbps = Convert.ToDouble(o["Speed"] ?? 0d) / 1e6;
                Nets.Add(speedMbps >= 1 ? $"{name}（{speedMbps:F0} Mbps）" : name);
            }
        }
        catch { }
    }

    private static string ReadRegistry(string path, string name, string fallback)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return key?.GetValue(name)?.ToString() ?? fallback;
        }
        catch
        {
            return fallback; // 非 Windows 或权限不足——降级
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);
}
