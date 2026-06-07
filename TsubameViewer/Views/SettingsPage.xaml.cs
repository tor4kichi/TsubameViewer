using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable
namespace TsubameViewer.Views;

public sealed partial class SettingsPage : Page, ITitlebarContentAware
{
    public DataTemplate? GetContent()
    {
        return TitlebarContent;
    }

    public R3.Observable<string> ObserveTitleChanged()
    {
        return R3.Observable.Return("Settings".Translate());
    }

    public SettingsPage()
    {
        this.InitializeComponent();

        DataContext = _vm = Ioc.Default.GetRequiredService<SettingsPageViewModel>();
        _messenger = Ioc.Default.GetRequiredService<IMessenger>();
    }

    readonly SettingsPageViewModel _vm;
    readonly IMessenger _messenger;

    void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var fe = (FrameworkElement)sender;
        if (fe.IsLoaded == false) { return; }

        var itemVM = (fe).DataContext as LocaleSelectSettingItemViewModel;
        if (itemVM == null) { return; }

        if (e.AddedItems[0] is PortableLanguage pl)
        {
            itemVM.SelectedLocale = pl;
        }
        else
        {
            itemVM.SelectedLocale = null;
        }
    }

    [RelayCommand]
    void BackNavigationRequest()
    {
        _messenger.Send(new BackNavigationRequestMessage());
    }
}
