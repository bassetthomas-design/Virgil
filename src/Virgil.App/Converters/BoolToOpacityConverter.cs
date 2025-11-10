using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Virgil.App.Converters
{
    public class BoolToOpacityConverter : IValueConverter
    {
        public double TrueOpacity { get; set; } = 0.0;
        public double FalseOpacity { get; set; } = 1.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? TrueOpacity : FalseOpacity;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}
