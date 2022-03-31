using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;
using Windows.UI;

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
            _IsReversePageFliping_Scroll = Read(false, nameof(IsReversePageFliping_Scroll));
            _IsReversePageFliping_Button = Read(false, nameof(IsReversePageFliping_Button));
            _RootFontSizeInPixel = Read(DefaultRootFontSizeInPixel, nameof(RootFontSizeInPixel));
            _LetterSpacingInPixel = Read(DefaultLetterSpacingInPixel, nameof(LetterSpacingInPixel));
            _LineHeightInNoUnit = Read(DefaultLineHeightInNoUnit, nameof(LineHeightInNoUnit));
            _RubySizeInPixel = Read(DefaultRubySizeInPixel, nameof(RubySizeInPixel));
            _FontFamily = Read(default(string), nameof(FontFamily));
            _RubyFontFamily = Read(default(string), nameof(RubyFontFamily));
            _BackgroundColor = Read<Color>(Colors.Transparent, nameof(BackgroundColor));
            _ForegroundColor = Read<Color>(Colors.Transparent, nameof(ForegroundColor));
            _OverrideWritingMode = Read(WritingMode.Inherit, nameof(OverrideWritingMode));
            _ColumnCount = Read(MinColumnCount, nameof(ColumnCount));
            _MaxWidth = Read(1280.0d, nameof(MaxWidth));
            _MaxHeight = Read(720.0d, nameof(MaxHeight));
        }

        private bool _IsReversePageFliping_Scroll;
        public bool IsReversePageFliping_Scroll
        {
            get { return _IsReversePageFliping_Scroll; }
            set { SetProperty(ref _IsReversePageFliping_Scroll, value); }
        }


        private bool _IsReversePageFliping_Button;
        public bool IsReversePageFliping_Button
        {
            get { return _IsReversePageFliping_Button; }
            set { SetProperty(ref _IsReversePageFliping_Button, value); }
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

        private string _FontFamily;
        public string FontFamily
        {
            get { return _FontFamily; }
            set { SetProperty(ref _FontFamily, value); }
        }


        private string _RubyFontFamily;
        public string RubyFontFamily
        {
            get { return _RubyFontFamily; }
            set { SetProperty(ref _RubyFontFamily, value); }
        }

        private Color _BackgroundColor;
        public Color BackgroundColor
        {
            get { return _BackgroundColor; }
            set { SetProperty(ref _BackgroundColor, value); }
        }

        private Color _ForegroundColor;
        public Color ForegroundColor
        {
            get { return _ForegroundColor; }
            set { SetProperty(ref _ForegroundColor, value); }
        }


        private WritingMode _OverrideWritingMode;
        public WritingMode OverrideWritingMode
        {
            get { return _OverrideWritingMode; }
            set { SetProperty(ref _OverrideWritingMode, value); }
        }

        public const int MaxColumnCount = 10;
        public const int MinColumnCount = 1;

        private int _ColumnCount;
        public int ColumnCount
        {
            get => _ColumnCount;
            set => SetProperty(ref _ColumnCount, Math.Clamp(value, MinColumnCount, MaxColumnCount));
        }


        private double _MaxWidth;
        public double MaxWidth
        {
            get => _MaxWidth;
            set => SetProperty(ref _MaxWidth, Math.Clamp(value, 0.0, 100000.0));
        }

        private double _MaxHeight;
        public double MaxHeight
        {
            get => _MaxHeight;
            set => SetProperty(ref _MaxHeight, double.IsNaN(value) ? value : Math.Clamp(value, 0.0, 100000.0));
        }
    }


    public enum WritingMode
    {
        Inherit,
        Horizontal_TopToBottom,
        Vertical_RightToLeft,
        Vertical_LeftToRight,
    }

}
