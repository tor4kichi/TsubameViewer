using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace TsubameViewer.Presentation.Views.Converters
{
    public sealed class AutoSuggestBoxQuerySubmittedEventArgsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is AutoSuggestBoxQuerySubmittedEventArgs args)
            {
                return args.ChosenSuggestion ?? args.QueryText;
            }

            throw new NotSupportedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
