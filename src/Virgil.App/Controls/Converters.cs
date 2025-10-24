#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Virgil.App.Controls
{
    // Convertit Color ou string vers Brush
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
                    var cc = new ColorConverter();
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

    // Double -> Thickness (pour espacement des yeux)
    public sealed class EyeSeparationToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double sep = 0;
            if (value is double d) sep = d;

            var side = (parameter as string)?.ToLowerInvariant();
            return side switch
            {
                "left" => new Thickness(-sep, 0, sep, 0),
                "right" => new Thickness(sep, 0, -sep, 0),
                _ => new Thickness(0)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
