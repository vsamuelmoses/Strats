using System;
using System.Globalization;
using System.Windows.Data;

namespace Carvers.Infra.ViewModels.Converters
{
    public class BoolToValueConverter : IValueConverter
    {
        public object TrueValue { get; set; }
        public object FalseValue { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool v = false;
            if (value is bool)
            {
                v = (bool)value;
            }
            if (value is int)
            {
                v = ((int)value) >= 1;
            }

            return v ? TrueValue : FalseValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
