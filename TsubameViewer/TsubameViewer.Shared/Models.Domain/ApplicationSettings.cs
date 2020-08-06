using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain
{
    public sealed class ApplicationSettings : FlagsRepositoryBase
    {
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
    }


    public enum ApplicationTheme
    {
        Default,
        Light,
        Dark,
    }
}
