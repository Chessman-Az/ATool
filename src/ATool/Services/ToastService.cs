using Serilog;

namespace ATool.Services;

/// <summary>
/// Windows Toast 通知（锁屏备用提醒渠道）。
/// 当前实现状态：降级为仅置顶弹窗（计划 Wave 2 待验证假设失败——
/// WindowsAppSDK 2.3.1 与 Toolkit.Uwp.Notifications 7.1.3 的发送 API
/// 均要求 net8.0-windows10.x TFM，与纯 net8.0 跨平台 TFM 冲突）。
/// 未来若切换为 -windows TFM，在此补实现；接口保持，调用方不变。
/// </summary>
public sealed class ToastService
{
    private bool _usable;

    public void Initialize()
    {
        if (!OperatingSystem.IsWindows())
        {
            _usable = false;
            return;
        }
        Log.Warning("Toast 通道不可用（需要 -windows TFM），已降级：提醒以置顶弹窗 + 唤醒补发覆盖");
        _usable = false;
    }

    /// <summary>降级实现：不发送（日志记录），主渠道为置顶弹窗。</summary>
    public void Show(string title, string message)
    {
        Log.Information("Toast(降级跳过): {Title} - {Message}", title, message);
    }

    public void Shutdown()
    {
    }
}
