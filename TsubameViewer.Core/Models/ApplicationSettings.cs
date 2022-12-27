using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TsubameViewer.Core.Infrastructure;

namespace TsubameViewer.Core.Models;

public sealed class ApplicationSettings : FlagsRepositoryBase
{
    public ApplicationSettings()
    {
        _Theme = Read(ApplicationTheme.Default, nameof(Theme));
        _Locale = Read(default(string), nameof(Locale));
        _ForceXboxAppearanceModeEnabled = Read(false, nameof(ForceXboxAppearanceModeEnabled));
        _IsUINavigationFocusAssistanceEnabled = Read(false, nameof(IsUINavigationFocusAssistanceEnabled));
    }

    private ApplicationTheme _Theme;
    public ApplicationTheme Theme
    {
        get { return _Theme; }
        set { SetProperty(ref _Theme, value); }
    }



    private string _Locale;
    public string Locale
    {
        get { return _Locale; }
        set { SetProperty(ref _Locale, value); }
    }


    private bool _ForceXboxAppearanceModeEnabled;
    public bool ForceXboxAppearanceModeEnabled
    {
        get => _ForceXboxAppearanceModeEnabled;
        set => SetProperty(ref _ForceXboxAppearanceModeEnabled, value);
    }

    private bool _IsUINavigationFocusAssistanceEnabled;
    public bool IsUINavigationFocusAssistanceEnabled
    {
        get => _IsUINavigationFocusAssistanceEnabled;
        set => SetProperty(ref _IsUINavigationFocusAssistanceEnabled, value);
    }
}




public enum ApplicationTheme
{
    Default,
    Light,
    Dark,
}
