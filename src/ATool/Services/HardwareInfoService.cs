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
            var display = ReadRegistry(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion", "");
            var edition = ReadRegistry(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "EditionID", "");
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
