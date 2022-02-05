using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Presentation.Views.Converters
{
    public sealed class EqualToConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Enum enumValue)
            {
                if (parameter is Enum enumParam)
                {
                    return enumValue.Equals(enumParam);
                }
                else if (parameter is string strParam)
                {
                    return enumValue.Equals(Enum.Parse(value.GetType(), strParam));
                }
                else 
                {
                    return enumValue.Equals(Enum.ToObject(value.GetType(), parameter));
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }


    public sealed class NotEqualToConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Enum enumValue)
            {
                if (parameter is Enum enumParam)
                {
                    return !enumValue.Equals(enumParam);
                }
                else if (parameter is string strParam)
                {
                    return !enumValue.Equals(Enum.Parse(value.GetType(), strParam));
                }
                else
                {
                    return !enumValue.Equals(Enum.ToObject(value.GetType(), parameter));
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
