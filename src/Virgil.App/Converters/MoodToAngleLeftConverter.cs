using System;
using System.Globalization;
using System.Windows.Data;

namespace Virgil.App.Converters
{
    public class MoodToAngleLeftConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = (value?.ToString() ?? string.Empty).ToLowerInvariant();
            return key switch { "happy" => -5.0, "tired" => 5.0, "angry" => -8.0, "focused" => 0.0, _ => 0.0 };
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
