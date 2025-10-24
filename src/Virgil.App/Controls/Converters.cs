#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WMedia = System.Windows.Media;

namespace Virgil.App.Controls
{
    /// <summary>
    /// Convertit Color/string/Brush en SolidColorBrush (gère aussi System.Drawing.Color).
    /// </summary>
    public sealed class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // 1) Déjà un Brush → renvoyer tel quel
            if (value is WMedia.Brush existing) return existing;

            // 2) WPF Color → SolidColorBrush
            if (value is WMedia.Color wpfColor)
                return CreateFrozenBrush(wpfColor);

            // 3) System.Drawing.Color → SolidColorBrush (sans import System.Drawing pour éviter l'ambiguïté)
            if (value is global::System.Drawing.Color dColor)
            {
                var w = WMedia.Color.FromArgb(dColor.A, dColor.R, dColor.G, dColor.B);
                return CreateFrozenBrush(w);
            }

            // 4) string → essayer de parser via ColorConverter ("#FF00FF", "#AA112233", "White", etc.)
            if (value is string s && !string.IsNullOrWhiteSpace(s))
            {
                try
                {
                    var obj = WMedia.ColorConverter.ConvertFromString(s);
                    if (obj is WMedia.Color parsed)
                        return CreateFrozenBrush(parsed);
                }
                catch
                {
                    // on ignore le parsing error → fallback transparent
                }
            }

            // 5) fallback : Transparent
            return WMedia.Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is WMedia.SolidColorBrush sb) return sb.Color;

            // ConvertBack non supporté pour les autres types → on rend la main au binding
            return DependencyProperty.UnsetValue;
        }

        private static WMedia.SolidColorBrush CreateFrozenBrush(WMedia.Color c)
        {
            var b = new WMedia.SolidColorBrush(c);
            if (b.CanFreeze) b.Freeze();
            return b;
        }
    }

    /// <summary>
    /// Décale chaque œil via Margin selon la séparation (double) et le paramètre "left"/"right".
    /// </summary>
    public sealed class EyeSeparationToMarginConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Séparation par défaut
            double sep = 8.0;
            if (value is double d && double.IsFinite(d)) sep = Math.Abs(d);

            var side = (parameter as string ?? "left").Trim().ToLowerInvariant();

            if (side == "left")
                return new Thickness(-sep, 0, sep, 0);

            if (side == "right")
                return new Thickness(sep, 0, -sep, 0);

            return new Thickness(0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}
