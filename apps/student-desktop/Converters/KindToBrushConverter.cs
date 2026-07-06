using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace StudentDesktop.Converters;

public class KindToBrushConverter : IValueConverter
{
    public static readonly KindToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value as string) switch
    {
        "header" => new SolidColorBrush(Color.Parse("#F0F0F0")),
        "hour" => Brushes.Transparent,
        "class_session" => new SolidColorBrush(Color.Parse("#D6E4FF")),
        "college_event-grid" => new SolidColorBrush(Color.Parse("#D6F5DD")),
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
