using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI;
using Windows.UI.Xaml.Data;

namespace TsubameViewer.Presentation.Views.Converters
{
    public sealed class NullableColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                return Colors.Transparent;
            }
            if (value is Color color)
            {
                return color;
            }

            throw new NotSupportedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Color color)
            {
                return color == Colors.Transparent ? default(Color?) : color;
            }

            throw new NotSupportedException();
        }
    }
}
