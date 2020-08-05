using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.EBook
{
    public sealed class EBookReaderSettings : FlagsRepositoryBase
    {
        public EBookReaderSettings()
        {
            _Theme = Read(ApplicationTheme.Default, nameof(Theme));
            _RootFontSizeInPixel = Read(24, nameof(RootFontSizeInPixel));
        }


        private ApplicationTheme _Theme;
        public ApplicationTheme Theme
        {
            get { return _Theme; }
            set { SetProperty(ref _Theme, value); }
        }


        public static ApplicationTheme GetSystemTheme()
        {
            return App.Current.RequestedTheme switch
            {
                Windows.UI.Xaml.ApplicationTheme.Dark => ApplicationTheme.Dark,
                Windows.UI.Xaml.ApplicationTheme.Light => ApplicationTheme.Light,
                _ => throw new NotSupportedException()
            };
        }


        private int _RootFontSizeInPixel;
        public int RootFontSizeInPixel
        {
            get { return _RootFontSizeInPixel; }
            set { SetProperty(ref _RootFontSizeInPixel, value); }
        }
    }


    public enum ApplicationTheme
    {
        Default,
        Light,
        Dark,
    }
}
