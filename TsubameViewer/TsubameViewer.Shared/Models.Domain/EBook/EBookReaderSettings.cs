using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.EBook
{
    public sealed class EBookReaderSettings : FlagsRepositoryBase
    {
        public static double DefaultRootFontSizeInPixel = 18.0;
        public static double DefaultLetterSpacingInPixel = 0.0;
        public static double DefaultLineHeightInNoUnit = 1.5;
        public static double DefaultRubySizeInPixel = 12.0;


        public EBookReaderSettings()
        {
            _Theme = Read(ApplicationTheme.Default, nameof(Theme));
            _RootFontSizeInPixel = Read(DefaultRootFontSizeInPixel, nameof(RootFontSizeInPixel));
            _LetterSpacingInPixel = Read(DefaultLetterSpacingInPixel, nameof(LetterSpacingInPixel));
            _LineHeightInNoUnit = Read(DefaultLineHeightInNoUnit, nameof(LineHeightInNoUnit));
            _RubySizeInPixel = Read(DefaultRubySizeInPixel, nameof(RubySizeInPixel));
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

        private double _RootFontSizeInPixel;
        public double RootFontSizeInPixel
        {
            get { return _RootFontSizeInPixel; }
            set { SetProperty(ref _RootFontSizeInPixel, value); }
        }

        private double _LetterSpacingInPixel;
        public double LetterSpacingInPixel
        {
            get { return _LetterSpacingInPixel; }
            set { SetProperty(ref _LetterSpacingInPixel, value); }
        }

        private double _LineHeightInNoUnit;
        public double LineHeightInNoUnit
        {
            get { return _LineHeightInNoUnit; }
            set { SetProperty(ref _LineHeightInNoUnit, value); }
        }

        private double _RubySizeInPixel;
        public double RubySizeInPixel
        {
            get { return _RubySizeInPixel; }
            set { SetProperty(ref _RubySizeInPixel, value); }
        }
    }


    public enum ApplicationTheme
    {
        Default,
        Light,
        Dark,
    }
}
