namespace ATool.Services;

/// <summary>
/// DeepSeek 峰谷计价：北京时间每日 9:00-12:00 与 14:00-18:00 为高峰（2 倍价）。
/// 北京时间无夏令时，本地时间即北京时间（若用户机器时区非东八区，判断基于本地时间）。
/// 边界语义：9:00 整进入高峰，12:00 整退出；14:00 整进入，18:00 整退出。
/// </summary>
public static class PeakHourService
{
    public static bool IsPeakHour(DateTime local)
    {
        var t = local.TimeOfDay;
        return (t >= TimeSpan.FromHours(9) && t < TimeSpan.FromHours(12))
            || (t >= TimeSpan.FromHours(14) && t < TimeSpan.FromHours(18));
    }

    /// <summary>当前时段状态文案：高峰「摸鱼」/ 低谷「开干」（图标由界面单独渲染，避免重复）。</summary>
    public static (string Text, bool IsPeak) CurrentStatus(DateTime now)
        => IsPeakHour(now) ? ("摸鱼", true) : ("开干", false);

    /// <summary>24h 时段表（详情页展示）。</summary>
    public static IReadOnlyList<PeakPeriod> GetPeriods() =>
    [
        new("00:00", "09:00", false, 1m),
        new("09:00", "12:00", true, 2m),
        new("12:00", "14:00", false, 1m),
        new("14:00", "18:00", true, 2m),
        new("18:00", "24:00", false, 1m),
    ];

    public sealed record PeakPeriod(string Start, string End, bool IsPeak, decimal Multiplier)
    {
        /// <summary>展示文本：1 → 「原价」，2 → 「2 倍价」。</summary>
        public string DisplayMultiplier => Multiplier == 1m ? "原价" : $"{Multiplier} 倍价";
    }
}
