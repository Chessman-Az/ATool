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

/// <summary>int 是否为零（单向，用于 IsVisible 空状态）：0 → true。</summary>
public class IsZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int n && n == 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool → 颜色刷（峰谷主按钮：高峰红 / 低谷绿）。</summary>
public class BoolToPeakBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Avalonia.Media.SolidColorBrush.Parse("#F87171")
            : Avalonia.Media.SolidColorBrush.Parse("#34D399");

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
            return Avalonia.Media.SolidColorBrush.Parse("#7E93AD");
        return text.StartsWith('-')
            ? Avalonia.Media.SolidColorBrush.Parse("#F87171")
            : Avalonia.Media.SolidColorBrush.Parse("#34D399");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>日历格子着色：选中→主色蓝 / 今天→红，其余透明或文字色。parameter：bg(背景)/fg(文字)/dot(标记点)。</summary>
public class CalendarDayBrushConverter : IMultiValueConverter
{
    private static readonly Avalonia.Media.IBrush Primary = Avalonia.Media.SolidColorBrush.Parse("#38BDF8");
    private static readonly Avalonia.Media.IBrush Today = Avalonia.Media.SolidColorBrush.Parse("#F87171");
    private static readonly Avalonia.Media.IBrush White = Avalonia.Media.SolidColorBrush.Parse("#FFFFFF");
    private static readonly Avalonia.Media.IBrush Text = Avalonia.Media.SolidColorBrush.Parse("#E2EAF6");
    private static readonly Avalonia.Media.IBrush Transparent = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Transparent);

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = values.Count > 0 && values[0] is true;
        var isToday = values.Count > 1 && values[1] is true;
        return parameter as string switch
        {
            "fg" => isSelected || isToday ? White : Text,
            "dot" => isSelected || isToday ? White : Primary,
            _ => isSelected ? Primary : isToday ? Today : Transparent, // bg
        };
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool → 文字装饰：true=删除线（已完成），false=无。</summary>
public class BoolToTextDecorationsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Avalonia.Media.TextDecorations.Strikethrough
            : new Avalonia.Media.TextDecorationCollection();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>余额变动金额标签底色：增加→淡绿、减少→淡红、空/持平→透明。</summary>
public class DeltaBackgroundBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrEmpty(text) || text == "—")
            return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Transparent);
        return text.StartsWith('-')
            ? Avalonia.Media.SolidColorBrush.Parse("#33F87171") // 减：半透明红
            : Avalonia.Media.SolidColorBrush.Parse("#3334D399"); // 增：半透明绿
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
