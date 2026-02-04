using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Virgil.App.Converters;

public sealed class GreaterThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return Visibility.Collapsed;
        }

        if (value is int intValue)
        {
            return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (value is long longValue)
        {
            return longValue > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (value is double doubleValue)
        {
            return doubleValue > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (value is float floatValue)
        {
            return floatValue > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToDouble(culture) > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            catch (FormatException)
            {
                return Visibility.Collapsed;
            }
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
