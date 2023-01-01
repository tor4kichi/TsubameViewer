using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Composition.Interactions;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace TsubameViewer.Views.Converters
{
    public sealed class NavigationViewItemInvokedEventArgsConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
            {
                return args.InvokedItemContainer?.DataContext ?? args.InvokedItem;
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
