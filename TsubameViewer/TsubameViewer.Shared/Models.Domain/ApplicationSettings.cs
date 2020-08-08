using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain
{
    public sealed class ApplicationSettings : FlagsRepositoryBase
    {
        public ApplicationSettings()
        {
            _Theme = Read(ApplicationTheme.Default, nameof(Theme));
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
    }


    public static class SystemThemeHelper
    {
        static string _uiTheme = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Background).ToString();

        public static ApplicationTheme GetSystemTheme()
        {
            ApplicationTheme appTheme;
            if (_uiTheme == "#FF000000")
            {
                appTheme = ApplicationTheme.Dark;
            }
            else
            {
                appTheme = ApplicationTheme.Light;
            }

            return appTheme;
        }
    }

    public enum ApplicationTheme
    {
        Default,
        Light,
        Dark,
    }
}
