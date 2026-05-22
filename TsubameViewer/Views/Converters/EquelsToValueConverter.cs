using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace TsubameViewer.Views.Converters;

internal class EquelsToValueConverter : DependencyObject, IValueConverter
{
    public double EquelsTo
    {
        get { return (double)GetValue(EquelsToProperty); }
        set { SetValue(EquelsToProperty, value); }
    }

    public static readonly DependencyProperty EquelsToProperty =
        DependencyProperty.Register(nameof(EquelsTo), typeof(double), typeof(EquelsToValueConverter), new PropertyMetadata(0d));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        double v = value switch
        {
            double d => d,
            float f => f,
            long l => l,
            int i => i,
            uint u => u,
            ulong ul => ul,
            _ => throw new InvalidOperationException(),
        };

        return v == EquelsTo;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
