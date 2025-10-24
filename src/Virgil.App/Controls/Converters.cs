#nullable enable
using System;
using System.Globalization;
using System.Windows;            // Visibility
using System.Windows.Media;      // SolidColorBrush

namespace Virgil.App.Controls
{
    /// <summary>
    /// Convertisseurs / helpers WPF sans aucune référence WinForms.
    /// Tous les types WPF (Binding, Color, etc.) sont pleinement qualifiés.
    /// </summary>
    public static class Converters
    {
        /// <summary>
        /// Color depuis un hex (#RRGGBB ou #AARRGGBB).
        /// </summary>
        public static System.Windows.Media.Color ColorFromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return System.Windows.Media.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

            var obj = System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return obj is System.Windows.Media.Color c
                ? c
                : System.Windows.Media.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        }

        /// <summary>
        /// SolidColorBrush depuis un hex (#RRGGBB ou #AARRGGBB).
        /// </summary>
        public static System.Windows.Media.SolidColorBrush BrushFromHex(string hex)
            => new System.Windows.Media.SolidColorBrush(ColorFromHex(hex));

        /// <summary>
        /// Crée un Binding WPF sans ambiguïté avec WinForms.Binding.
        /// </summary>
        public static System.Windows.Data.Binding Bind(
            string path,
            object? source = null,
            System.Windows.Data.IValueConverter? converter = null,
            System.Windows.Data.BindingMode mode = System.Windows.Data.BindingMode.OneWay,
            System.Windows.Data.UpdateSourceTrigger update = System.Windows.Data.UpdateSourceTrigger.PropertyChanged)
        {
            return new System.Windows.Data.Binding(path)
            {
                Source = source,
                Converter = converter,
                Mode = mode,
                UpdateSourceTrigger = update
            };
        }

        /// <summary>
        /// Binding OneTime simplifié.
        /// </summary>
        public static System.Windows.Data.Binding BindOneTime(
            string path,
            object? source = null,
            System.Windows.Data.IValueConverter? converter = null)
            => Bind(path, source, converter,
                    System.Windows.Data.BindingMode.OneTime,
                    System.Windows.Data.UpdateSourceTrigger.PropertyChanged);
    }

    /// <summary>
    /// Exemple de converter: Mood -> Brush (utilisable en XAML).
    /// </summary>
    public sealed class MoodToBrushConverter : System.Windows.Data.IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var mood = value as string ?? "neutral";
            return mood switch
            {
                "proud"    => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x46, 0xFF, 0x7A)),
                "vigilant" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0xFF, 0xE4, 0x6B)),
                "alert"    => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0xFF, 0x69, 0x61)),
                _          => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0xAA, 0xB7, 0xC4)),
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing; // fully qualified
    }

    /// <summary>
    /// bool -> Visibility (si tu ne veux pas utiliser le BoolToVisibilityConverter built-in).
    /// </summary>
    public sealed class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public bool Invert { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool v = value is bool b && b;
            if (Invert) v = !v;
            return v ? Visibility.Visible : Visibility.Collapsed;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing; // fully qualified
    }
}
