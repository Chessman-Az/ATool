using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using Serilog;

namespace ATool.Services;

/// <summary>
/// 前台浏览器窗口 URL 读取（UIA 地址栏 ValuePattern）。
/// 仅用于 Windows；非浏览器窗口或读取失败返回 null（调用方退回标题兜底）。
/// </summary>
public static class BrowserUrlReader
{
    /// <summary>读取指定窗口句柄对应的浏览器地址栏 URL（Edge/Chrome/Firefox 等 Chromium 系）。</summary>
    public static string? TryGetUrl(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !OperatingSystem.IsWindows()) return null;
        try
        {
            // UIA 树查询可能较慢（Chromium 大窗口树 + 首次 COM 初始化），5s 轮询中给足余量
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
            return Task.Run(() => QueryUrl(hwnd), cts.Token)
                .WaitAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException or COMException or ElementNotAvailableException)
        {
            return null; // 超时/元素失效——正常降级
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "浏览器 URL 读取失败");
            return null;
        }
    }

    private static string? QueryUrl(IntPtr hwnd)
    {
        var root = AutomationElement.FromHandle(hwnd);
        var edits = root.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        // Chromium 地址栏：Edit + ValuePattern + 值为 http(s) URL；多标签时取第一个命中
        foreach (AutomationElement e in edits)
        {
            if (!(e.TryGetCurrentPattern(ValuePattern.Pattern, out var pat) && pat is ValuePattern vp))
                continue;
            var value = vp.Current.Value;
            if (!string.IsNullOrEmpty(value) && value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return value;
        }
        return null;
    }
}

/// <summary>主域名提取纯逻辑（可单测）：URL → 注册域展示名（去协议/路径/www 前缀）。</summary>
public static class SiteDomain
{
    /// <summary>
    /// 从 URL 提取主域名："https://www.douyin.com/jingxuan?x=1" → "douyin.com"。
    /// 无效 URL 返回 null（调用方退回标题兜底）。
    /// </summary>
    public static string? ExtractMainDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            return null;
        var host = uri.Host;
        // 去常见 www 前缀（"www.douyin.com" → "douyin.com"）；IPv4 直连保留原样
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            host = host[4..];
        return host;
    }
}
