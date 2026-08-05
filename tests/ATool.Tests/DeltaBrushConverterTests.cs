using System.Globalization;
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
        var color = ((Avalonia.Media.ISolidColorBrush)brush!).Color.ToString();
        Assert.Equal("#FF2E9E5B", color); // SuccessBrush
    }

    [Fact]
    public void Convert_减少_返回红色()
    {
        var brush = _conv.Convert("-7.00", typeof(IBrush), null, CultureInfo.InvariantCulture);
        var color = ((Avalonia.Media.ISolidColorBrush)brush!).Color.ToString();
        Assert.Equal("#FFD64545", color); // DangerBrush
    }

    [Fact]
    public void Convert_持平或空_返回灰色()
    {
        var brush = _conv.Convert("—", typeof(IBrush), null, CultureInfo.InvariantCulture);
        var color = ((Avalonia.Media.ISolidColorBrush)brush!).Color.ToString();
        Assert.Equal("#FF6B7280", color); // TextSecondary
    }
}
