using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace StudentDesktop.Converters;

public class BoolToRegisterLabelConverter : IValueConverter
{
    public static readonly BoolToRegisterLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Registered" : "Register";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
