#nullable enable
using System;
using System.Globalization;
using System.Windows;            // Visibility, Thickness
using System.Windows.Media;      // SolidColorBrush

namespace Virgil.App.Controls
{
    /// <summary>
    /// Convertisseurs / helpers WPF sans référence à WinForms.
    /// Tous les types ambigus (Binding, Color, etc.) sont pleinement qualifiés côté WPF.
    /// </summary>
    public static class Converters
    {
        public static System.Windows.Media.Color ColorFromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return System.Windows.Media.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

            var obj = System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return obj is System.Windows.Media.Color c
                ? c
                : System.Windows.Media.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        }

        public static System.Windows.Media.SolidColorBrush BrushFromHex(string hex)
            => new System.Windows.Media.SolidColorBrush(ColorFromHex(hex));

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

        public static System.Windows.Data.Binding BindOneTime(
            string path,
            object? source = null,
            System.Windows.Data.IValueConverter? converter = null)
            => Bind(path, source, converter,
                    System.Windows.Data.BindingMode.OneTime,
                    System.Windows.Data.UpdateSourceTrigger.PropertyChanged);
    }

    public sealed class ColorToBrushConverter : System.Windows.Data.IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is System.Windows.Media.Color c)
                return new System.Windows.Media.SolidColorBrush(c);

            if (value is string s && !string.IsNullOrWhiteSpace(s))
                return new System.Windows.Media.SolidColorBrush(Converters.ColorFromHex(s));

            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0xAA, 0xB7, 0xC4));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

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
            => System.Windows.Data.Binding.DoNothing;
    }

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
            => System.Windows.Data.Binding.DoNothing;
    }

    public sealed class EyeSeparationToMarginConverter : System.Windows.Data.IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            double sep = 0;
            try
            {
                sep = value switch
                {
                    double d => d,
                    float f => f,
                    int i => i,
                    string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var dd) => dd,
                    _ => 0
                };
            }
            catch { sep = 0; }

            bool isLeft = false;
            double topOffset = 0;

            if (parameter is string param && !string.IsNullOrWhiteSpace(param))
            {
                var parts = param.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var raw in parts)
                {
                    var p = raw.Trim();
                    if (p.Equals("Left", StringComparison.OrdinalIgnoreCase)) isLeft = true;
                    else if (p.Equals("Right", StringComparison.OrdinalIgnoreCase)) isLeft = false;
                    else if (p.StartsWith("Top=", StringComparison.OrdinalIgnoreCase))
                    {
                        var v = p.Substring(4);
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var tv))
                            topOffset = tv;
                    }
                }
            }

            double dx = (sep / 2.0) * (isLeft ? -1 : +1);
            return new Thickness(dx, topOffset, 0, 0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }
}
