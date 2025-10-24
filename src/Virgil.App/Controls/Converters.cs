#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

// IMPORTANT : ce namespace doit correspondre à celui que tu utilises dans XAML :
// xmlns:controls="clr-namespace:Virgil.App.Controls"
namespace Virgil.App.Controls
{
    /// <summary>
    /// Convertit un Color (ou string) en SolidColorBrush. Accepte déjà un Brush en entrée.
    /// </summary>
    public sealed class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Déjà un Brush -> on renvoie tel quel
            if (value is Brush b) return b;

            // Color WPF
            if (value is Color c) return new SolidColorBrush(c);

            // String -> Color (ex: "#FF00FF" ou "White")
            if (value is string s && !string.IsNullOrWhiteSpace(s))
            {
                try
                {
                    var obj = ColorConverter.ConvertFromString(s);
                    if (obj is Color cc) return new SolidColorBrush(cc);
                }
                catch { /* ignore parsing errors */ }
            }

            // Valeur par défaut
            return Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush sb) return sb.Color;
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Décale chaque œil vers la gauche ou la droite via une Margin,
    /// en fonction de la séparation (double) et du ConverterParameter "left"/"right".
    /// </summary>
    public sealed class EyeSeparationToMarginConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Séparation par défaut si null/non num.
            double sep = 8.0;
            if (value is double d) sep = Math.Abs(d);

            string side = (parameter as string ?? "left").Trim().ToLowerInvariant();

            // On renvoie une Thickness différente selon le côté.
            // (AUCUN opérateur '?:' ici → pas d'erreur de syntaxe)
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
