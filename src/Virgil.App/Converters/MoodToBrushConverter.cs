using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Virgil.App.Converters
{
    public class MoodToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = (value?.ToString() ?? "").ToLowerInvariant();
            Color c = key switch
            {
                "happy" => (Color)ColorConverter.ConvertFromString("#00FFC8"),
                "tired" => (Color)ColorConverter.ConvertFromString("#6F7A8C"),
                "angry" => (Color)ColorConverter.ConvertFromString("#FF4D4D"),
                "focused" => (Color)ColorConverter.ConvertFromString("#5AD1FF"),
                _ => (Color)ColorConverter.ConvertFromString("#B0B0B0"),
            };
            return new SolidColorBrush(c);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
