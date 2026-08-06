using System.Globalization;
using Avalonia.Media;
using ATool.Views;
using Xunit;

namespace ATool.Tests;

/// <summary>余额变动金额着色（增绿/减红/持平灰）的回归锚点。</summary>
public class DeltaBrushConverterTests
{
    private readonly DeltaBrushConverter _conv = new();

    [Fact]
    public void Convert_增加_返回绿色()
    {
        var brush = _conv.Convert("+5.00", typeof(IBrush), null, CultureInfo.InvariantCulture);
        var c = ((ISolidColorBrush)brush!).Color;
        Assert.Equal((0x34, 0xD3, 0x99), (c.R, c.G, c.B)); // SuccessBrush 绿
    }

    [Fact]
    public void Convert_减少_返回红色()
    {
        var brush = _conv.Convert("-7.00", typeof(IBrush), null, CultureInfo.InvariantCulture);
        var c = ((ISolidColorBrush)brush!).Color;
        Assert.Equal((0xF8, 0x71, 0x71), (c.R, c.G, c.B)); // DangerBrush 红
    }

    [Fact]
    public void Convert_持平或空_返回灰色()
    {
        var brush = _conv.Convert("—", typeof(IBrush), null, CultureInfo.InvariantCulture);
        var c = ((ISolidColorBrush)brush!).Color;
        Assert.Equal((0x7E, 0x93, 0xAD), (c.R, c.G, c.B)); // TextSecondary 灰
    }
}
