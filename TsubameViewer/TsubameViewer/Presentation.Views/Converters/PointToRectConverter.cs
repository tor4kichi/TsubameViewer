using System;
using System.Collections.Generic;
using System.Text;
using Windows.Foundation;
using Microsoft.UI.Xaml.Data;

namespace TsubameViewer.Presentation.Views.Converters
{
    public sealed class PointToRectConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Point pt)
            {
                return new Rect(new Point(), pt);
            }
            else if (value is Size size)
            {
                return new Rect(new Point(), size);
            }
            else if (value is double wh)
            {
                return new Rect(new Point(), new Size(wh, wh));
            }
            else if (value is int intWidthHeight)
            {
                return new Rect(new Point(), new Size(intWidthHeight, intWidthHeight));
            }

            throw new NotSupportedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
