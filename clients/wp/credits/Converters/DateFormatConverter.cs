using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Credits.Converters
{
    public class DateFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string res = String.Empty;
            if (value != null && parameter != null)
            {
                if ((parameter.ToString() == "news" || parameter.ToString() == "news_fav") 
                    && DateTime.Now.Date == ((DateTime)value).Date)
                {
                    res = ((DateTime)value).ToString("t", new CultureInfo("nl-BE"));
                }
                else if (parameter.ToString() == "news")
                {
                    res = ((DateTime)value).ToString("g", new CultureInfo("ru-RU"));
                }
                else
                {
                    res = ((DateTime)value).ToString("g", new CultureInfo("ru-RU"));
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
