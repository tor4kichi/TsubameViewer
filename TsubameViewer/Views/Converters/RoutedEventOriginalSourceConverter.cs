using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;

namespace TsubameViewer.Views.Converters
{
    public sealed class RoutedEventOriginalSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is RoutedEventArgs routedEventArgs)
            {
                return (routedEventArgs.OriginalSource as FrameworkElement).DataContext;
            }
            else
            {
                throw new NotSupportedException(value?.GetType().Name);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
