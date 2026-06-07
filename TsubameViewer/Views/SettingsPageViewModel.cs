using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.UI.Xaml.Controls;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Maintenance;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Converters;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.ViewManagement;

#nullable enable
namespace TsubameViewer.ViewModels;

public sealed class SettingsPageViewModel : NavigationAwareViewModelBase
{
    readonly IMessenger _messenger;
    readonly ApplicationSettings _applicationSettings;
    readonly FolderListingSettings _folderListingSettings;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    readonly ImageViewerSettings _imageViewerPageSettings;
    readonly FolderListingSettings _folderListupSettings;
    readonly IThumbnailImageMaintenanceService _thumbnailImageMaintenanceService;

    public SettingsGroupViewModel[] SettingGroups { get; }
    public SettingsGroupViewModel[] AdvancedSettingGroups { get; }

    public RelayCommand AppInfoCopyToClipboard { get; }

    readonly ButtonSettingItemViewModel _cacheSizeButton;
    
    public bool IsForceXboxAppearanceModeEnabled
    {
        get => _applicationSettings.ForceXboxAppearanceModeEnabled;
        set
        {
            _applicationSettings.ForceXboxAppearanceModeEnabled = value;
            App.Current.Resources["DebugTVMode"] = value;
        }
    }

    public string ReportUserEnvString { get; } 
    public SettingsPageViewModel(
        IMessenger messenger,
        ApplicationSettings applicationSettings,
        FolderListingSettings folderListingSettings,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        ImageViewerSettings imageViewerPageSettings,
        FolderListingSettings folderListupSettings,
        IThumbnailImageMaintenanceService thumbnailImageMaintenanceService
        )
    {
        _messenger = messenger;
        _applicationSettings = applicationSettings;
        _folderListingSettings = folderListingSettings;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _imageViewerPageSettings = imageViewerPageSettings;
        _folderListupSettings = folderListupSettings;
        _thumbnailImageMaintenanceService = thumbnailImageMaintenanceService;
        _isThumbnailDeleteButtonActive = new ReactiveProperty<bool>();
        _thumbnailImagesCacheSizeText = new ReactivePropertySlim<string>();
        
        AppInfoCopyToClipboard = new RelayCommand(() =>
        {
            var data = new DataPackage();
            data.SetText(ReportUserEnvString);
            Clipboard.SetContent(data);
        });

        _cacheSizeButton = new ButtonSettingItemViewModel("DeleteThumbnailCache".Translate(), "ThumbnailCacheSize".Translate(), "?", () => DeleteAllThumbnailsAsync());

        SettingGroups = new[]
        {
            new SettingsGroupViewModel
            {
                Label = "GeneralUISettings".Translate(),
                Items =
                {
                    new ThemeSelectSettingItemViewModel("ApplicationTheme".Translate(), _applicationSettings, _messenger),
                    new LocaleSelectSettingItemViewModel("OverrideLocale".Translate(), _applicationSettings),
                    new ToggleSwitchSettingItemViewModel<ApplicationSettings>("IsAppMenuShowWithLeft".Translate(), _applicationSettings, x => x.IsAppMenuShowWithLeft),

                    new ToggleSwitchSettingItemViewModel<ApplicationSettings>("IsForceEnableXYNavigation".Translate(), _applicationSettings, x => x.IsUINavigationFocusAssistanceEnabled) { IsVisible = (Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.DeviceFamily == "Windows.Xbox") is false },
#if DEBUG
                    new ToggleSwitchSettingItemViewModel<ApplicationSettings>("ForceXboxAppearanceModeEnabled".Translate(), _applicationSettings, x => x.ForceXboxAppearanceModeEnabled) { IsVisible = (Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.DeviceFamily == "Windows.Xbox") is false },
#endif
                    new ToggleSwitchSettingItemViewModel<ApplicationSettings>("IsFullScreenOnAppLaunch".Translate(), _applicationSettings, x => x.IsFullScreenOnAppLaunch) { IsVisible = (Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.DeviceFamily == "Windows.Xbox") is false },
                }
            },
            new SettingsGroupViewModel
            {
                Label = "ImageViewerSettings".Translate(),
                Items =
                {
                    new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsEnablePrefetch".Translate(), _imageViewerPageSettings, x => x.IsEnablePrefetch),
                    new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsReverseImageFliping_MouseWheel".Translate(), _imageViewerPageSettings, x => x.IsReverseImageFliping_MouseWheel),
                    new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsEnableDoubleView".Translate(), _imageViewerPageSettings, x => x.IsEnableDoubleView),
                    new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsKeepSingleViewOnFirstPage".Translate(), _imageViewerPageSettings, x => x.IsKeepSingleViewOnFirstPage),
                    new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsLeftBindingView".Translate(), _imageViewerPageSettings, x => x.IsLeftBindingView),
                }
            },
            new SettingsGroupViewModel
            {
                Label = "FolderItemListingSettings".Translate(),
                Items =
                {
                    new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsGenerateImageFileThumbnail".Translate(), _folderListingSettings, x => x.IsImageFileGenerateThumbnailEnabled),
                    new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsGenerateFolderThumbnail".Translate(), _folderListingSettings, x => x.IsFolderGenerateThumbnailEnabled),
                    new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsGenerateArchiveFileThumbnail".Translate(), _folderListingSettings, x => x.IsArchiveFileGenerateThumbnailEnabled),
                    new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsGenerateArchiveEntryThumbnail".Translate(), _folderListingSettings, x => x.IsArchiveEntryGenerateThumbnailEnabled),
                    _cacheSizeButton,
                }
            },
            new SettingsGroupViewModel
            {
                Label = "SourceFoldersSettings".Translate(),
                Items =
                {
                    new StoredFoldersSettingItemViewModel(_messenger, _sourceStorageItemsRepository),
                }
            },
            new SettingsGroupViewModel
            {
                Label = "OtherSettings".Translate(),
                Items =
                {
                    new ToggleSwitchSettingItemViewModel<FolderListingSettings>("ShowWithIndexedFolderItemAccess".Translate(), _folderListupSettings, x => x.ShowWithIndexedFolderItemAccess),
                }
            },
        };

        AdvancedSettingGroups = new[]
        {
            new SettingsGroupViewModel
            {
                /*
                Label = "ThumbnailImageSettings".Translate(),
                Items =
                {
                    new ButtonSettingItemViewModel("DeleteThumbnailCache".Translate(), () => _ = DeleteThumnnailsAsync()),
                    new UpdatableTextSettingItemViewModel("ThumbnailCacheSize".Translate(), _ThumbnailImagesCacheSizeText)
                }
                */
            },
        };

        StringBuilder sb = new StringBuilder();
        sb.Append(SystemInformation.Instance.ApplicationName)
            .Append(" v").Append(SystemInformation.Instance.ApplicationVersion.ToFormattedString())
            .AppendLine();
        sb.Append(SystemInformation.Instance.OperatingSystem).Append(" ").Append(SystemInformation.Instance.OperatingSystemArchitecture)
            .Append("(").Append(SystemInformation.Instance.OperatingSystemVersion).Append(")")
            .Append(" ").Append(SystemInformation.Instance.DeviceFamily)
            ;
        ReportUserEnvString = sb.ToString();
    }

    public override void OnNavigatedFrom(INavigationParameters parameters)
    {
        foreach (var group in SettingGroups)
        {
            (group as IDisposable)?.Dispose();
        }

        foreach (var group in AdvancedSettingGroups)
        {
            (group as IDisposable)?.Dispose();
        }

        if (_applicationSettings.IsFullScreenOnAppLaunch)
        {
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.FullScreen;
            ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
        }
        else
        {
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
        }

        base.OnNavigatedFrom(parameters);
    }

    async Task DeleteAllThumbnailsAsync()
    {
        _thumbnailImagesCacheSizeText.Value = string.Empty;

        _isThumbnailDeleteButtonActive.Value = false;
        await _thumbnailImageMaintenanceService.DeleteAllThumbnailsAsync();

        RefreshThumbnailFilesSizeAsync(_navigationCt).FireAndForgetSafe();
    }

    ReactiveProperty<bool> _isThumbnailDeleteButtonActive;
    ReactivePropertySlim<string> _thumbnailImagesCacheSizeText;


    CancellationToken _navigationCt;
    public override Task OnNavigatedToAsync(INavigationParameters parameters, CancellationToken ct)
    {
        _navigationCt = ct;
        var mode = parameters.GetNavigationMode();
        if (mode == Windows.UI.Xaml.Navigation.NavigationMode.Refresh)
        {
            return Task.CompletedTask;
        }

        _isThumbnailDeleteButtonActive.Value = true;
        RefreshThumbnailFilesSizeAsync(ct).FireAndForgetSafe();
        // base.OnNavigatedToAsync(parameters, ct);

        return Task.CompletedTask;
    }

    async Task RefreshThumbnailFilesSizeAsync(CancellationToken ct)
    {
        try
        {
            var size = (ulong) await Task.Run(() => _thumbnailImageMaintenanceService.ComputeUsingSize(), ct);
            _cacheSizeButton.Description = ToUserFiendlyFileSizeText(size) + "B";
            //_ThumbnailImagesCacheSizeText.Value = ToUserFiendlyFileSizeText(size) + "B";
        }
        catch (OperationCanceledException)
        {

        }
    }
    static string ToUserFiendlyFileSizeText(ulong size)
    {
        var conv = new ToKMGTPEZYConverter();
        return (string)conv.Convert(size, typeof(string), null, CultureInfo.CurrentCulture.Name);
    }


}

public abstract class SettingItemViewModelBase : ObservableObject
{
    public bool IsVisible { get; set; } = true;
}

public sealed class SettingsGroupViewModel : IDisposable
{
    public bool IsVisible => Items?.Any(x => x.IsVisible) ?? false;

    public string Label { get; set; } = "";
    public List<SettingItemViewModelBase> Items { get; set; } = new List<SettingItemViewModelBase>();

    public void Dispose() 
    {
        foreach (var item in Items)
        {
            (item as IDisposable)?.Dispose();
        }
    }
}


public sealed class StoredFoldersSettingItemViewModel :  SettingItemViewModelBase
{
    readonly IMessenger _messenger;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;

    public ObservableCollection<StoredFolderViewModel> Folders { get; }
    public ObservableCollection<StoredFolderViewModel> TempFiles { get; }

    public StoredFoldersSettingItemViewModel(IMessenger messenger, SourceStorageItemsRepository sourceStorageItemsRepository)
    {
        _messenger = messenger;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        Folders = new ObservableCollection<StoredFolderViewModel>();
        TempFiles = new ObservableCollection<StoredFolderViewModel>();

        Init().FireAndForgetSafe();
    }

    async Task Init()
    {
        try
        {
            await foreach (var item in _sourceStorageItemsRepository.GetParsistantItems())
            {
                Folders.Add(new StoredFolderViewModel(_messenger, this, _sourceStorageItemsRepository)
                {
                    Item = item.item!,
                    FolderName = item.item?.Name ?? "???",
                    Path = item.item?.Path ?? "",
                    Token = item.token,
                });
            }
        }
        catch { }

        await foreach (var item in _sourceStorageItemsRepository.GetTemporaryItems())
        {
            TempFiles.Add(new StoredFolderViewModel(_messenger, this, _sourceStorageItemsRepository)
            {
                Item = item.item,
                FolderName = item.item.Name,
                Path = item.item.Path,
                Token = item.token,
            });
        }
    }

    internal void RemoveItem(StoredFolderViewModel childVM)
    {
        Folders.Remove(childVM);
        TempFiles.Remove(childVM);
    }
}

public sealed partial class StoredFolderViewModel
{
    readonly IMessenger _messenger;
    readonly StoredFoldersSettingItemViewModel _parentVM;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;

    public StoredFolderViewModel(IMessenger messenger, StoredFoldersSettingItemViewModel parentVM, SourceStorageItemsRepository sourceStorageItemsRepository)
    {
        _messenger = messenger;
        _parentVM = parentVM;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
    }

    public string? FolderName { get; set; }
    public string? Path { get; set; }
    public string? Token { get; set; }
    public IStorageItem? Item { get; set; }

    [RelayCommand]
    async Task DeleteStoredFolder()
    {
        Guard.IsNotNullOrEmpty(Token);
        // 削除済みフォルダであった場合は記録したパスを利用する            
        var path = !string.IsNullOrEmpty(Path) ? Path : _sourceStorageItemsRepository.GetPathFromToken(Token);
        if (path != null)
        {
            var result = await _messenger.WorkWithBusyWallAsync(async (ct) => await _messenger.Send(new SourceStorageItemIgnoringRequestMessage(path)), CancellationToken.None);
        }

        _sourceStorageItemsRepository.RemoveFolder(Token);
        _parentVM.RemoveItem(this);
    }
}



public interface IToggleSwitchSettingItemViewModel
{
    string Label { get; }
    ReactiveProperty<bool> ValueContainer { get; }
}

public class ToggleSwitchSettingItemViewModel<T> : SettingItemViewModelBase, IToggleSwitchSettingItemViewModel , IDisposable
    where T : INotifyPropertyChanged
{
    public ToggleSwitchSettingItemViewModel(string label, T value, Expression<Func<T, bool>> expression)
    {
        ValueContainer = value.ToReactivePropertyAsSynchronized(expression);
        Label = label;
    }

    public ReactiveProperty<bool> ValueContainer { get; }
    public string Label { get; }

    public void Dispose()
    {
        ((IDisposable)ValueContainer).Dispose();
    }
}

public class NumberBoxSettingItemViewModel : SettingItemViewModelBase
{
    readonly Action<double> _changedAction;
    
    public NumberBoxSettingItemViewModel(string label, double firstValue, double minValue, double maxValue, double valueStep, Action<double> changedAction)
    {
        Label = label;
        FirstValue = firstValue;
        MinValue = minValue;
        MaxValue = maxValue;
        ValueStep = valueStep;
        _changedAction = changedAction;
    }

    public string Label { get; }
    public double FirstValue { get; }
    public double MinValue { get; }
    public double MaxValue { get; }
    public double ValueStep { get; }

    public void OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        _changedAction(args.NewValue);
    }
}

public class UpdatableTextSettingItemViewModel : SettingItemViewModelBase, IDisposable 
{
    public string Label { get; }
    public IReadOnlyReactiveProperty<string?> Text { get; }
    public UpdatableTextSettingItemViewModel(string label, IObservable<string> textObservable)
    {
        Label = label;
        Text = textObservable.ToReadOnlyReactivePropertySlim();
    }

    public void Dispose()
    {
        Text.Dispose();
    }
}

public partial class ButtonSettingItemViewModel : SettingItemViewModelBase, IDisposable
{
    public ButtonSettingItemViewModel(string buttonLabel, string label, string description, Func<Task> buttonAction)
    {
        ButtonLabel = buttonLabel;
        Label = label;
        Description = description;
        ActionCommand = new AsyncReactiveCommand();
        ActionCommand.Subscribe(buttonAction);
    }

    public ButtonSettingItemViewModel(string buttonLabel, string label, string description, Func<Task> buttonAction, IObservable<bool> canExecuteObservable)
    {
        ButtonLabel = buttonLabel;
        Label = label;
        Description = description;
        ActionCommand = canExecuteObservable.ToAsyncReactiveCommand();
        ActionCommand.Subscribe(buttonAction);
    }

    public ButtonSettingItemViewModel(string buttonLabel, string label, string description, Action buttonAction, IObservable<bool> canExecuteObservable)
    {
        ButtonLabel = buttonLabel;
        Label = label;
        Description = description;
        ActionCommand = canExecuteObservable.ToAsyncReactiveCommand();
        ActionCommand.Subscribe(_ => 
        {
            buttonAction();
            return Task.CompletedTask;
        });
    }

    public string ButtonLabel { get; }
    public string Label { get; }


    [ObservableProperty]
    string? _description;

    public AsyncReactiveCommand ActionCommand { get; }

    public void Dispose()
    {
        ((IDisposable)ActionCommand).Dispose();
    }
}

public class ThemeSelectSettingItemViewModel : SettingItemViewModelBase, IDisposable
{
    readonly IMessenger _messenger;

    public ThemeSelectSettingItemViewModel(string label, ApplicationSettings applicationSettings, IMessenger messenger)
    {
        Label = label;
        _messenger = messenger;
        SelectedTheme = applicationSettings.ToReactivePropertyAsSynchronized(x => x.Theme);

        _themeChangedSubscriber = SelectedTheme.Subscribe(theme => 
        {
            _messenger.Send<ThemeChangeRequestMessage>(new (theme));
        });
    }

    public ReactiveProperty<ApplicationTheme> SelectedTheme { get; }

    public IReadOnlyList<ApplicationTheme> ThemeItems { get; } = new[] { ApplicationTheme.Default, ApplicationTheme.Light, ApplicationTheme.Dark };

    IDisposable _themeChangedSubscriber;

    public string Label { get; }

    public void Dispose()
    {
        ((IDisposable)SelectedTheme).Dispose();
        _themeChangedSubscriber.Dispose();
    }
}

public sealed partial class LocaleSelectSettingItemViewModel : SettingItemViewModelBase, IDisposable
{
    string _currentLocale = I18NPortable.I18N.Current.Locale;
    public LocaleSelectSettingItemViewModel(string label, ApplicationSettings applicationSettings)
    {
        Label = label;
        _applicationSettings = applicationSettings;
        SelectedLocale = Locales.FirstOrDefault(x =>x.Locale == applicationSettings.Locale);
    }

    private string? _restartTextTranslated;
    public string? RestartTextTranslated
    {
        get { return _restartTextTranslated; }
        set { SetProperty(ref _restartTextTranslated, value); }
    }

    private bool _isRequireRestart;
    public bool IsRequireRestart
    {
        get { return _isRequireRestart; }
        set { SetProperty(ref _isRequireRestart, value); }
    }

    [ObservableProperty]
    PortableLanguage? _selectedLocale;

    partial void OnSelectedLocaleChanged(PortableLanguage? value)
    {
        if (value != null)
        {
            _applicationSettings.Locale = value.Locale;

            I18NPortable.I18N.Current.Locale = value.Locale;
            IsRequireRestart = _currentLocale != value.Locale;
            RestartTextTranslated = "RequireRestartApplicationToRefrectSettings".Translate();
        }
    }

    public IReadOnlyList<PortableLanguage> Locales { get; } = I18NPortable.I18N.Current.Languages;

    public string Label { get; }

    public void Dispose()
    {
    }

    readonly ApplicationSettings _applicationSettings;

    [RelayCommand]
    void RestartApplication()
    {
        _ = Windows.ApplicationModel.Core.CoreApplication.RequestRestartAsync("");
    }
}
