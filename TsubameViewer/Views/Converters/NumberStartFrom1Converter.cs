using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml.Data;

namespace TsubameViewer.Views.Converters
{
    public sealed class NumberStartFrom1Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double d)
            {
                return (int)d + 1;
            }
            else if (value is int i)
            {
                return i + 1;
            }

            throw new NotSupportedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
