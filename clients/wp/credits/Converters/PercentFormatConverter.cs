using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Credits.Converters
{
    public class PercentFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string res = String.Empty;
            if (value != null && value is Rate)
            {
                Rate rate = (value as Rate);
                if (parameter as string == "Percentage")
                {
                    res = "От " + rate.MinValue.ToString() + " до " + rate.MaxValue.ToString();
                }
                else if (parameter as string == "MinInitialInstalment")
                {
                    res = rate.MinInitialInstalment.ToString() + "%";
                }
            }

            return res;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
