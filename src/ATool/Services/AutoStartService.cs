using Microsoft.Win32;

namespace ATool.Services;

/// <summary>开机自启（Windows 注册表 Run 键）。非 Windows 平台 IsEnabled 恒 false、SetEnabled 抛 PlatformNotSupportedException。</summary>
public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ATool";

    public static bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        var v = key?.GetValue(ValueName) as string;
        return v is not null && v.Contains(Environment.ProcessPath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("开机自启仅 Windows 支持");
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
        {
            var exe = Environment.ProcessPath
                ?? throw new InvalidOperationException("无法确定程序路径");
            key.SetValue(ValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
