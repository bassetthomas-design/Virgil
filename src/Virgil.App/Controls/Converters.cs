#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;       // Binding (WPF)
using System.Windows.Media;     // Color/Brush (WPF)

namespace Virgil.App.Controls
{
    // ----------- Color -> SolidColorBrush -----------
    public sealed class ColorToBrushConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color c) return new SolidColorBrush(c);
            if (value is string s)
            {
                try
                {
                    var cc = (Color)ColorConverter.ConvertFromString(s)!;
                    return new SolidColorBrush(cc);
                }
                catch { }
            }
            // Transparence par défaut
            return new SolidColorBrush(Color.FromArgb(0x00, 0, 0, 0));
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is SolidColorBrush b) ? b.Color : DependencyProperty.UnsetValue;
    }

    // ----------- double (séparation des yeux) -> Thickness (Margin) -----------
    public sealed class EyeSeparationToMarginConverter : IValueConverter
    {
        // parameter attendu : "left" ou "right" (par défaut: "left")
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double sep = 0;
            try { sep = System.Convert.ToDouble(value, CultureInfo.InvariantCulture); } catch { }

            string side = (parameter as string ?? "left").Trim().ToLowerInvariant();

            // On répartit la moitié de la séparation de part et d’autre
            double half = Math.Max(0, sep) / 2.0;

            // Margin(left, top, right, bottom)
            return side == "right"
                ? new Thickness(half, 0, 0, 0)
                : new Thickness(-half, 0, 0, 0);
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    // ----------- (Optionnel) bool -> Visibility (si tu n’utilises pas BooleanToVisibilityConverter) -----------
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public bool CollapseWhenFalse { get; set; } = true;

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool v = false;
            if (value is bool b) v = b;
            else if (value is bool? nb && nb.HasValue) v = nb.Value;

            if (v) return Visibility.Visible;
            return CollapseWhenFalse ? Visibility.Collapsed : Visibility.Hidden;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility vis) return vis == Visibility.Visible;
            return false;
        }
    }
}
