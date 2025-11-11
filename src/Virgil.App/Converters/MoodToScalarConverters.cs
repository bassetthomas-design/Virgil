using System;
using System.Globalization;
using System.Windows.Data;

namespace Virgil.App.Converters
{
    public class MoodToScaleYConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var m = (value as string)?.ToLowerInvariant() ?? string.Empty;
            if (m.Contains("tired")) return 0.6;
            if (m.Contains("focused")) return 0.75;
            return 1.0; // calm, happy, default
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class MoodToScaleXConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var m = (value as string)?.ToLowerInvariant() ?? string.Empty;
            if (m.Contains("alert") || m.Contains("overheat") || m.Contains("angry")) return 1.2;
            return 1.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class MoodToAngleLeftConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var m = (value as string)?.ToLowerInvariant() ?? string.Empty;
            if (m.Contains("tired")) return 8.0;
            if (m.Contains("alert")) return -6.0;
            if (m.Contains("happy")) return -4.0;
            return 0.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class MoodToAngleRightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var m = (value as string)?.ToLowerInvariant() ?? string.Empty;
            if (m.Contains("tired")) return -8.0;
            if (m.Contains("alert")) return 6.0;
            if (m.Contains("happy")) return 4.0;
            return 0.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
