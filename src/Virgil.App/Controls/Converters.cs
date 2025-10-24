#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
// On alias tout le namespace Media pour lever les ambiguïtés (Color, Brush, Brushes…)
using WMedia = System.Windows.Media;

namespace Virgil.App.Controls
{
    /// <summary>
    /// Convertit un Color (ou string) en SolidColorBrush. Accepte déjà un Brush en entrée.
    /// </summary>
    public sealed class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Déjà un Brush -> renvoyer tel quel
            if (value is WMedia.Brush b) return b;

            // Color WPF -> SolidColorBrush
            if (value is WMedia.Color c) return new WMedia.SolidColorBrush(c);

            // String -> Color (ex: "#FF00FF" ou "White")
            if (value is string s && !string.IsNullOrWhiteSpace(s))
            {
                try
                {
                    var obj = WMedia.ColorConverter.ConvertFromString(s);
                    if (obj is WMedia.Color cc) return new WMedia.SolidColorBrush(cc);
                }
                catch { /* ignore parsing errors */ }
            }

            // Valeur par défaut
            return WMedia.Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is WMedia.SolidColorBrush sb) return sb.Color;
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Décale chaque œil via la Margin selon la séparation (double) et le paramètre "left"/"right".
    /// </summary>
    public sealed class EyeSeparationToMarginConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Séparation par défaut si null/non num.
            double sep = 8.0;
            if (value is double d) sep = Math.Abs(d);

            string side = (parameter as string ?? "left").Trim().ToLowerInvariant();

            if (side == "left")
            {
                // œil gauche : marge négative à gauche, positive à droite
                return new Thickness(-sep, 0, sep, 0);
            }
            else if (side == "right")
            {
                // œil droit : marge positive à gauche, négative à droite
                return new Thickness(sep, 0, -sep, 0);
            }

            // fallback neutre
            return new Thickness(0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Pas de conversion retour
            return DependencyProperty.UnsetValue;
        }
    }
}
