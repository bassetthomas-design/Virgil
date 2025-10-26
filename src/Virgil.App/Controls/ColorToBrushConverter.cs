using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Virgil.App.Controls
{
    public sealed class ColorToBrushConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color c) return new SolidColorBrush(c);
            if (value is string s && (s.StartsWith("#") || s.Contains(",")))
            {
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(s)!); }
                catch { }
            }
            return Brushes.Transparent;
        }
        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is SolidColorBrush b ? b.Color : null;
    }
}
