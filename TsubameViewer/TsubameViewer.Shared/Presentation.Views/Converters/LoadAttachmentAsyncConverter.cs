﻿using Microsoft.Toolkit.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using Windows.Storage;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Presentation.Views.Converters
{
    public sealed class LoadAttachmentAsyncConverter : IValueConverter
    {
        static BitmapImage _empty = new BitmapImage();
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is IImageGenerater imageSource)
            {
                return new NotifyTaskCompletion<BitmapImage>(imageSource.GenerateBitmapImageAsync());
            }
            else
            {
                return new NotifyTaskCompletion<BitmapImage>(Task.FromResult(_empty));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
