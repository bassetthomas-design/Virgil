using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Virgil.App.Controls
{
    public sealed class HexToBrushConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && !string.IsNullOrWhiteSpace(s))
            {
                // Use fully-qualified static call to avoid any ambiguity or instance pattern
                var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(s);
                return new SolidColorBrush(color);
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
