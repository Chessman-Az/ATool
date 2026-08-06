using System.Globalization;
using Avalonia.Media;
using ATool.Views;
using Xunit;

namespace ATool.Tests;

/// <summary>余额变动金额标签底色：增加→淡绿、减少→淡红、空/持平→透明。</summary>
public class DeltaBackgroundBrushConverterTests
{
    private readonly DeltaBackgroundBrushConverter _conv = new();

    [Theory]
    [InlineData("+1.23", 52, 211, 153)]  // 增：半透明绿底
    [InlineData("-1.23", 248, 113, 113)]  // 减：半透明红底
    public void Convert_增减返回对应淡色底(string deltaText, byte r, byte g, byte b)
    {
        var brush = Assert.IsType<SolidColorBrush>(_conv.Convert(deltaText, typeof(IBrush), null, CultureInfo.InvariantCulture));
        Assert.Equal(r, brush.Color.R);
        Assert.Equal(g, brush.Color.G);
        Assert.Equal(b, brush.Color.B);
    }

    [Theory]
    [InlineData("")]
    [InlineData("—")]
    [InlineData(null)]
    public void Convert_空或持平返回透明(string? deltaText)
    {
        var brush = Assert.IsType<SolidColorBrush>(_conv.Convert(deltaText, typeof(IBrush), null, CultureInfo.InvariantCulture));
        Assert.Equal(0, brush.Color.A);
    }
}
