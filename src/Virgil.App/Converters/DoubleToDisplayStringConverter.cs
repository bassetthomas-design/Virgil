using System;
using System.Globalization;
using System.Windows.Data;

namespace Virgil.App.Converters;

public sealed class DoubleToDisplayStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IConvertible convertible)
        {
            return "—";
        }

        var numericValue = convertible.ToDouble(culture);
        if (double.IsNaN(numericValue) || double.IsInfinity(numericValue))
        {
            return "—";
        }

        var format = parameter as string;
        if (string.IsNullOrWhiteSpace(format))
        {
            return numericValue.ToString(culture);
        }

        return string.Format(culture, format, numericValue);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
