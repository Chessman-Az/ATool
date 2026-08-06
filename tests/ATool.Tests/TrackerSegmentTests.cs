using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>
/// 采样分段纯逻辑（UsageTrackerService.TrackerSegmentLogic）：
/// 前后两次前台窗口采样决定动作——Skip（系统窗口/空）/ Flush（相同窗口续时）/ Switch（换窗口分段）。
/// </summary>
public class TrackerSegmentTests
{
    [Fact]
    public void Decide_相同前台_返回Flush()
    {
        var action = TrackerSegmentLogic.Decide(
            curProcess: "chrome", curTitle: "B站 - Google Chrome",
            prevProcess: "chrome", prevTitle: "B站 - Google Chrome",
            isSystemWindow: false);
        Assert.Equal(TrackerSegmentLogic.Action.Flush, action);
    }

    [Fact]
    public void Decide_前台变化_返回Switch()
    {
        var action = TrackerSegmentLogic.Decide(
            curProcess: "chrome", curTitle: "B站 - Google Chrome",
            prevProcess: "winword", prevTitle: "文档1 - Word",
            isSystemWindow: false);
        Assert.Equal(TrackerSegmentLogic.Action.Switch, action);
    }

    [Fact]
    public void Decide_标题变化但进程相同_返回Switch()
    {
        // 同一浏览器切标签页：标题变化即视为切换（网站时长按标题聚合）
        var action = TrackerSegmentLogic.Decide(
            curProcess: "chrome", curTitle: "GitHub - Google Chrome",
            prevProcess: "chrome", prevTitle: "B站 - Google Chrome",
            isSystemWindow: false);
        Assert.Equal(TrackerSegmentLogic.Action.Switch, action);
    }

    [Fact]
    public void Decide_系统窗口_返回Skip()
    {
        var action = TrackerSegmentLogic.Decide(
            curProcess: "explorer", curTitle: "",
            prevProcess: "chrome", prevTitle: "B站 - Google Chrome",
            isSystemWindow: true);
        Assert.Equal(TrackerSegmentLogic.Action.Skip, action);
    }

    [Fact]
    public void Decide_空标题非系统_回落进程名()
    {
        // UWP/特殊窗口标题为空：非系统窗口时按进程名记录（标题回落）
        var action = TrackerSegmentLogic.Decide(
            curProcess: "someapp", curTitle: "",
            prevProcess: "chrome", prevTitle: "B站 - Google Chrome",
            isSystemWindow: false);
        Assert.Equal(TrackerSegmentLogic.Action.Switch, action);
    }

    [Fact]
    public void IsSystemWindow_任务栏与桌面_为真()
    {
        Assert.True(TrackerSegmentLogic.IsSystemWindow("Shell_TrayWnd"));
        Assert.True(TrackerSegmentLogic.IsSystemWindow("Progman"));
        Assert.True(TrackerSegmentLogic.IsSystemWindow("WorkerW"));
    }

    [Fact]
    public void IsSystemWindow_普通窗口_为假()
    {
        Assert.False(TrackerSegmentLogic.IsSystemWindow("Chrome_WidgetWin_1"));
    }
}
