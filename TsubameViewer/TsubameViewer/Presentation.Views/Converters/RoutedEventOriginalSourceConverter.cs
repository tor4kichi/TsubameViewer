using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;

namespace TsubameViewer.Presentation.Views.Converters
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
