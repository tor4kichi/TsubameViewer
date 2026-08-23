using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Controls;
using CommunityToolkit.WinUI.Helpers;
using I18NPortable;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Services;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Playback;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable
namespace TsubameViewer.Views;

[ObservableObject]
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

        InitialziePurchase().FireAndForgetSafe();
        ApplicationInfomationText.Text = GetAppInfoText().ToString();
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

    private void Segmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var control = (Selector)sender;
        var selectorSettingsItemVM = (ISelectorSettingsItemViewModel)(control).DataContext;
        if (!control.IsLoaded) { return; }
        
        selectorSettingsItemVM.SelectedItem = control.SelectedItem;
    }

    private async void ListView_Loaded(object sender, RoutedEventArgs e)
    {
        await Task.Delay(50);
        var control = (Selector)sender;
        var selectorSettingsItemVM = (ISelectorSettingsItemViewModel)(control).DataContext;

        control.SelectedItem = selectorSettingsItemVM.SelectedItem;
    }

    private void InteractionWall_Tapped(object sender, TappedRoutedEventArgs e)
    {
        this._messenger.Send<BackNavigationRequestMessage>();
    }

    #region Purchase Cheer Addon

    [RelayCommand(CanExecute = nameof(IsStoreAvairable))]
    async Task PurchaseAddonAsync()
    {
        var service = Ioc.Default.GetService<PurchaseAddonService>();
        if (service == null) { return; }
        var result = await service.PurchaseCheerAsync();
        Debug.WriteLine(result);

        if (result is Windows.Services.Store.StorePurchaseStatus.Succeeded or Windows.Services.Store.StorePurchaseStatus.AlreadyPurchased)
        {
            PurchaseThanksMassageFlyout.ShowAt(PurchaseAddonButton);
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PurchaseAddonCommand))]
    bool _isStoreAvairable;

    async Task InitialziePurchase()
    {
        var service = Ioc.Default.GetService<PurchaseAddonService>();
        if (service == null) { return; }

        if (string.IsNullOrEmpty(PurchaseConfirmFlyout_DescTextBlock.Text))
        {
            var info = await service.GetCheerAddonInfoAsync();
            if (info == null) { return; }
            PurchaseConfirmFlyout_DescTextBlock.Text = info?.Description ?? "";
            IsStoreAvairable = info != null;
        }
    }

    void ShowPurchaseConfirmFlyoutMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        PurchaseConfirmFlyout.ShowAt(PurchaseAddonButton);
    }

    #endregion
    #region Feedback

    [RelayCommand]
    void OpenStoreRatingPage()
    {
        _ = Microsoft.Toolkit.Uwp.Helpers.SystemInformation.LaunchStoreForReviewAsync();
    }

    [RelayCommand]
    void OpenMsFormFeedbackPage()
    {
        var appInfoText = ApplicationInfomationText.Text;
        var uri = new Uri($"https://forms.office.com/Pages/ResponsePage.aspx?id=DQSIkWdsW0yxEjajBLZtrQAAAAAAAAAAAAZAAObntfNUNVdWMThSTjFGMDhFWjI4TDJLSjUxTTM4SC4u&r8cc009228bff4265bf1eb48b0c408716={appInfoText}");
        _ = Launcher.LaunchUriAsync(uri);
    }

    [RelayCommand]
    void AppInformationCopyToClipboard()
    {
        var data = new DataPackage();
        data.SetText(ApplicationInfomationText.Text.ToString());
        Clipboard.SetContent(data);
        _messenger.SendShowTextNotificationMessage($"✅{"Copy".Translate()}");
    }

    StringBuilder GetAppInfoText()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.ApplicationName)
            .Append(" v").Append(Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.ApplicationVersion.ToFormattedString())
            .Append(" ");
        sb.Append(Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.OperatingSystem).Append(" ").Append(Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.OperatingSystemArchitecture)
            .Append("(").Append(Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.OperatingSystemVersion).Append(")")
            .Append(" ").Append(Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.DeviceFamily);
        return sb;
    }

    #endregion
}
