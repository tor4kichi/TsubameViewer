﻿using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace TsubameViewer.Views.Converters
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
