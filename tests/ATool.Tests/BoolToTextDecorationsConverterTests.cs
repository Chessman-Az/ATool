using System.Globalization;
using Avalonia.Media;
using ATool.Views;
using Xunit;

namespace ATool.Tests;

/// <summary>已完成提醒文字删除线转换器（主窗口列表 + 浮窗共用）。</summary>
public class BoolToTextDecorationsConverterTests
{
    private readonly BoolToTextDecorationsConverter _conv = new();

    [Fact]
    public void Convert_真值返回删除线()
    {
        var result = _conv.Convert(true, typeof(TextDecorationCollection), null, CultureInfo.InvariantCulture);
        Assert.IsType<TextDecorationCollection>(result);
        Assert.NotEmpty((TextDecorationCollection)result!);
    }

    [Fact]
    public void Convert_假值返回空装饰()
    {
        var result = _conv.Convert(false, typeof(TextDecorationCollection), null, CultureInfo.InvariantCulture);
        Assert.IsType<TextDecorationCollection>(result);
        Assert.Empty((TextDecorationCollection)result!);
    }
}
