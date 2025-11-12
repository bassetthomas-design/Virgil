using System;
using System.Globalization;
using System.Windows.Data;

namespace Virgil.App.Converters
{
    public class MoodToScaleYConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = (value?.ToString() ?? string.Empty).ToLowerInvariant();
            return key switch { "happy" => 1.2, "tired" => 0.9, "angry" => 1.1, "focused" => 1.0, _ => 1.0 };
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
