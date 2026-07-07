using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace TsubameViewer.Views.Converters;

public sealed class SizeToRectConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Vector2 v)
        {
            return new Rect(0, 0, v.X, v.Y);
        }
        else if (value is Size size)
        {
            return new Rect(new(), size);
        }
        else { return new Rect(); }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
