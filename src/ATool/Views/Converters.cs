using System.Globalization;
using Avalonia.Data.Converters;

namespace ATool.Views;

/// <summary>枚举相等转换（单向，用于 IsVisible）：value.ToString() == parameter → true。</summary>
public class EnumEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool → 颜色刷（峰谷主按钮：高峰红 / 低谷绿）。</summary>
public class BoolToPeakBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Avalonia.Media.SolidColorBrush.Parse("#D64545")
            : Avalonia.Media.SolidColorBrush.Parse("#2E9E5B");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>余额变动金额着色：增加绿色 / 减少红色 / 持平或空灰色。</summary>
public class DeltaBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrEmpty(text) || text == "—")
            return Avalonia.Media.SolidColorBrush.Parse("#6B7280");
        return text.StartsWith('-')
            ? Avalonia.Media.SolidColorBrush.Parse("#D64545")
            : Avalonia.Media.SolidColorBrush.Parse("#2E9E5B");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
