using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Virgil.App.Controls
{
    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is Color col ? new SolidColorBrush(col) : DependencyProperty.UnsetValue;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public class EyeSeparationToMarginConverter : IValueConverter
    {
        // param "left" ou "right"
        public object Convert(object value, Type t, object param, CultureInfo c)
        {
            var sep = value is double d ? d : 34;
            if ((param as string) == "left")  return new Thickness(sep, 70, 0, 0);
            if ((param as string) == "right") return new Thickness(0, 70, sep, 0);
            return new Thickness(0);
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}
