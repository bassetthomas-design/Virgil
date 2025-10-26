using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Virgil.App.Controls
{
    /// <summary>
    /// Convertit une valeur (séparation des yeux) en Margin horizontal.
    /// Exemple : 20 -> new Thickness(10, 0, 10, 0)
    /// </summary>
    public sealed class EyeSeparationToMarginConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                // accepte double/int/decimal/chaîne numérique
                double sep = value switch
                {
                    double d  => d,
                    float f   => f,
                    int i     => i,
                    decimal m => (double)m,
                    string s  => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0d,
                    _         => 0d
                };

                // moitié à gauche / moitié à droite
                var half = sep / 2.0;
                return new Thickness(half, 0, half, 0);
            }
            catch
            {
                return new Thickness(0);
            }
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Thickness t) return (t.Left + t.Right);
            return 0d;
        }
    }
}
