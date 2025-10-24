#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;        // Binding
using System.Windows.Media;       // Color/Brush

namespace Virgil.App.Controls
{
    // string/hex -> Brush (SolidColorBrush)
    public sealed class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Brush b) return b;

            if (value is Color c) return new SolidColorBrush(c);

            if (value is string s && !string.IsNullOrWhiteSpace(s))
            {
                try
                {
                    var cc = new ColorConverter();              // WPF ColorConverter
                    var col = (Color)cc.ConvertFromString(s)!;
                    return new SolidColorBrush(col);
                }
                catch { }
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // double (séparation des yeux) -> Thickness (marges opposées)
    public sealed class EyeSeparationToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double sep = 0;
            if (value is double d) sep = d;
            else if (value is float f) sep = f;

            // param = "Left" ou "Right"
            var side = parameter as string;
            if (string.Equals(side, "Left", StringComparison.OrdinalIgnoreCase))
                return new Thickness(-sep, 0, sep, 0);

            if (string.Equals(side, "Right", StringComparison.OrdinalIgnoreCase))
                return new Thickness(sep, 0, -sep, 0);

            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
