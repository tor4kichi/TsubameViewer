using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.UI.Xaml.Controls;
using R3;
using R3.Extensions;
using Reactive.Bindings.R3.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
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
using Windows.UI.Xaml.Controls.Primitives;

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
    readonly StorageItemSettings _storageItemSettings;
    private readonly ViewerSettings _viewerSettings;
    readonly MovieViewerPageSettings _movieSettings;
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
        StorageItemSettings storageItemSettings,
        ViewerSettings viewerSettings,
        MovieViewerPageSettings movieSettings,
        IThumbnailImageMaintenanceService thumbnailImageMaintenanceService
        )
    {
        _messenger = messenger;
        _applicationSettings = applicationSettings;
        _folderListingSettings = folderListingSettings;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _imageViewerPageSettings = imageViewerPageSettings;
        _folderListupSettings = folderListupSettings;
        _storageItemSettings = storageItemSettings;
        _viewerSettings = viewerSettings;
        _movieSettings = movieSettings;
        _thumbnailImageMaintenanceService = thumbnailImageMaintenanceService;
        
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
                    new ToggleSwitchSettingItemViewModel<ApplicationSettings>("IsAppMenuShowWithLeft".Translate(), _applicationSettings, _applicationSettings.IsAppMenuShowWithLeft, (x, v) => x.IsAppMenuShowWithLeft = v),

                    new ToggleSwitchSettingItemViewModel<ApplicationSettings>("IsForceEnableXYNavigation".Translate(), _applicationSettings, _applicationSettings.IsUINavigationFocusAssistanceEnabled, (x, v) => x.IsUINavigationFocusAssistanceEnabled = v) { IsVisible = (Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.DeviceFamily == "Windows.Xbox") is false },
#if DEBUG
                    new ToggleSwitchSettingItemViewModel<ApplicationSettings>("ForceXboxAppearanceModeEnabled".Translate(), _applicationSettings, _applicationSettings.ForceXboxAppearanceModeEnabled, (x, v) => x.ForceXboxAppearanceModeEnabled = v) { IsVisible = (Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.DeviceFamily == "Windows.Xbox") is false },
#endif
                    new ToggleSwitchSettingItemViewModel<ApplicationSettings>("IsFullScreenOnAppLaunch".Translate(), _applicationSettings, _applicationSettings.IsFullScreenOnAppLaunch, (x, v) => x.IsFullScreenOnAppLaunch = v) { IsVisible = (Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.DeviceFamily == "Windows.Xbox") is false },
                }
            },
            new SettingsGroupViewModel
            {
                Label = "ImageViewerSettings".Translate(),
                Items =
                {
                    new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsEnablePrefetch".Translate(), _imageViewerPageSettings, _imageViewerPageSettings.IsEnablePrefetch, (x, v) => x.IsEnablePrefetch = v),
                    new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsReverseImageFliping_MouseWheel".Translate(), _imageViewerPageSettings, _imageViewerPageSettings.IsReverseImageFliping_MouseWheel, (x, v) => x.IsReverseImageFliping_MouseWheel = v),
                    new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsEnableDoubleView".Translate(), _imageViewerPageSettings, _imageViewerPageSettings.IsEnableDoubleView, (x, v) => x.IsEnableDoubleView = v),
                    new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsKeepSingleViewOnFirstPage".Translate(), _imageViewerPageSettings, _imageViewerPageSettings.IsKeepSingleViewOnFirstPage, (x, v) => x.IsKeepSingleViewOnFirstPage = v),
                    new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsLeftBindingView".Translate(), _imageViewerPageSettings, _imageViewerPageSettings.IsLeftBindingView, (x, v) => x.IsLeftBindingView = v),
                }
            },
            new SettingsGroupViewModel
            {
                Label = "MovieViewer_Settings".Translate(),
                Items =
                {
                    new ToggleSwitchSettingItemViewModel<MovieViewerPageSettings>("IsAutoRepeatEnablingEnabled".Translate(), _movieSettings, _movieSettings.IsAutoRepeatEnablingEnabled, (x, v) => x.IsAutoRepeatEnablingEnabled = v),
                    new NumberBoxSettingItemViewModel(
                        "AutoRepeatEnablingTime".Translate(),
                        _movieSettings.AutoRepeatEnablingTimeInSeconds.TotalSeconds,
                        1,
                        TimeSpan.FromHours(24).TotalSeconds,
                        30,
                        f => _movieSettings.AutoRepeatEnablingTimeInSeconds = TimeSpan.FromSeconds(f),
                        f => TimeSpanHelper.FormatTimeSpan(TimeSpan.FromSeconds(f))
                        ),                    
                }
            },
            new SettingsGroupViewModel
            {
                Label = "ViewerSettings".Translate(),
                Items =
                {
                    new ToggleSwitchSettingItemViewModel<ViewerSettings>(
                        "IsDetectSimiralyFileNameNeighborsEnabled".Translate(),
                        "IsDetectSimiralyFileNameNeighborsEnabled_Desc".Translate(),
                        viewerSettings,
                        viewerSettings.IsDetectSimiralyFileNameNeighborsEnabled,
                        (x, v) => x.IsDetectSimiralyFileNameNeighborsEnabled = v),
                    new SliderSettingItemViewModel(
                        "ThresholdOfSimilarityFileNameNaighborsNormalized".Translate(),
                        "ThresholdOfSimilarityFileNameNaighborsNormalized_Desc".Translate(),
                        viewerSettings.ThresholdOfSimilarityFileNameNaighborsNormalized,
                        0.01,
                        0.99,
                        0.01,
                        f => viewerSettings.ThresholdOfSimilarityFileNameNaighborsNormalized = f),
                    new ToggleSwitchSettingItemViewModel<ViewerSettings>(
                        "IsAutoMoveToNextEnabled".Translate(),
                        viewerSettings,
                        viewerSettings.IsAutoMoveToNextEnabled,
                        (x, v) => x.IsAutoMoveToNextEnabled = v),
                }
            },
            new SettingsGroupViewModel
            {
                Label = "FolderItemListingSettings".Translate(),
                Items =
                {
                    new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsGenerateImageFileThumbnail".Translate(), _folderListingSettings, _folderListingSettings.IsImageFileGenerateThumbnailEnabled, (x, v) => x.IsImageFileGenerateThumbnailEnabled = v),
                    new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsGenerateFolderThumbnail".Translate(), _folderListingSettings, _folderListingSettings.IsFolderGenerateThumbnailEnabled, (x, v) => x.IsFolderGenerateThumbnailEnabled = v),
                    new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsGenerateArchiveFileThumbnail".Translate(), _folderListingSettings, _folderListingSettings.IsArchiveFileGenerateThumbnailEnabled, (x, v) => x.IsArchiveFileGenerateThumbnailEnabled = v),
                    new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsGenerateArchiveEntryThumbnail".Translate(), _folderListingSettings, _folderListingSettings.IsArchiveEntryGenerateThumbnailEnabled, (x, v) => x.IsArchiveEntryGenerateThumbnailEnabled = v),
                    _cacheSizeButton,
                    new SelectorSettingsItemViewModel<ThumbnailDecodeMethod>("ThumbnailDecodeMethod".Translate(), "ThumbnailDecodeMethod_Desc".Translate(), [ThumbnailDecodeMethod.Skia, ThumbnailDecodeMethod.WindowsImageCodec, ThumbnailDecodeMethod.Win2D], _folderListingSettings.ThumbnailDecodeType, type => _folderListingSettings.ThumbnailDecodeType = type),
                    new SliderSettingItemViewModel(
                        "FolderItemThumbnailQuality".Translate(),                        
                        _folderListingSettings.FolderItemThumbnailQuality,
                        0.5,
                        1.5,
                        0.05,
                        f => _folderListingSettings.FolderItemThumbnailQuality = (float)f),
                    new ToggleSwitchSettingItemViewModel<StorageItemSettings>(
                        "StorageItemSettings_IsDisplayFolderItemsCount".Translate(),
                        "StorageItemSettings_IsDisplayFolderItemsCount_Desc".Translate(),
                        _storageItemSettings,
                        _storageItemSettings.IsDisplayFolderItemsCount,
                        (x, v) => x.IsDisplayFolderItemsCount = v),
                    new SliderSettingItemViewModel(
                        "StorageItemSettings_ReadingFinishedThresholdForImageViewer".Translate(),
                        _storageItemSettings.ReadingFinishedThresholdForImageViewer,
                        0.01,
                        0.99,
                        0.01,
                        f => _storageItemSettings.ReadingFinishedThresholdForImageViewer = (float)f),
                    new SliderSettingItemViewModel(
                        "StorageItemSettings_ReadingFinishedThresholdForEBookViewer".Translate(),
                        _storageItemSettings.ReadingFinishedThresholdForEBookViewer,
                        0.01,
                        0.99,
                        0.01,
                        f => _storageItemSettings.ReadingFinishedThresholdForEBookViewer = (float)f),
                    new SliderSettingItemViewModel(
                        "StorageItemSettings_ReadingFinishedThresholdForMovieViewer".Translate(),
                        _storageItemSettings.ReadingFinishedThresholdForMovieViewer,
                        0.01,
                        0.99,
                        0.01,
                        f => _storageItemSettings.ReadingFinishedThresholdForMovieViewer = (float)f),
                    new ToggleSwitchSettingItemViewModel<FolderListingSettings>(
                        "IsInPageSearchWithMigemo".Translate(),
                        "IsInPageSearchWithMigemo_Desc".Translate(),
                        _folderListingSettings,
                        _folderListingSettings.IsInPageSearchWithMigemo,
                        (x, v) => x.IsInPageSearchWithMigemo = v),                    
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
            //new SettingsGroupViewModel
            //{
            //    Label = "OtherSettings".Translate(),
            //    Items =
            //    {
            //    }
            //},
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
        await _thumbnailImageMaintenanceService.DeleteAllThumbnailsAsync();

        RefreshThumbnailFilesSizeAsync(_navigationCt).FireAndForgetSafe();
    }

    CancellationToken _navigationCt;
    public override Task OnNavigatedToAsync(INavigationParameters parameters, CancellationToken ct)
    {
        _navigationCt = ct;
        var mode = parameters.GetNavigationMode();
        if (mode == Windows.UI.Xaml.Navigation.NavigationMode.Refresh)
        {
            return Task.CompletedTask;
        }

        //RefreshThumbnailFilesSizeAsync(ct).FireAndForgetSafe();

        return Task.CompletedTask;
    }

    async Task RefreshThumbnailFilesSizeAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(1000);
            var size = (ulong) await Task.Run(() => _thumbnailImageMaintenanceService.ComputeUsingSize(), ct);
            _cacheSizeButton.Description = ToUserFiendlyFileSizeText(size) + "B";
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
            foreach (var item in await _sourceStorageItemsRepository.GetParsistantItems())
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
    string Desc { get; }
    bool Value { get; set; }
}

public partial class ToggleSwitchSettingItemViewModel<T> : SettingItemViewModelBase, IToggleSwitchSettingItemViewModel
    where T : INotifyPropertyChanged
{
    public ToggleSwitchSettingItemViewModel(string label, T model, bool firstValue, Action<T, bool> setValue)
    {
        Label = label;
        _model = model;
        _value = firstValue;
        _setValue = setValue;
        Desc = "";        
    }

    public ToggleSwitchSettingItemViewModel(string label, string desc, T model, bool firstValue, Action<T, bool> setValue)
    {
        Label = label;
        Desc = desc;
        _model = model;
        _value = firstValue;
        _setValue = setValue;
    }

    private readonly T _model;
    [ObservableProperty]
    bool _value;

    private readonly Action<T, bool> _setValue;

    partial void OnValueChanged(bool value) => _setValue(_model, value);

    public string Label { get; }
    public string Desc { get; }
}

public interface ISelectorSettingsItemViewModel : INotifyPropertyChanged
{
    public string Label { get; }
    public string Desc { get; }
    IList<object> Items { get; }
    object? SelectedItem { get; set; }
}

public sealed partial class SelectorSettingsItemViewModel<T> : SettingItemViewModelBase, ISelectorSettingsItemViewModel
{
    private readonly Action<T> _onChangedAction;

    public SelectorSettingsItemViewModel(string label, string desc, List<T> items, T firstSelectItem, Action<T> onChangedAction)
    {
        Label = label;
        Desc = desc;
        Items = items;
        _onChangedAction = onChangedAction;
        _selectedItem = firstSelectItem;
    }

    [ObservableProperty]
    T? _selectedItem;

    partial void OnSelectedItemChanged(T value)
    {
        _onChangedAction(value);
    }

    public string Label { get; }
    public string Desc { get; }
    public List<T> Items { get; }

    IList<object> ISelectorSettingsItemViewModel.Items => Items.Cast<object>().ToList();

    object? ISelectorSettingsItemViewModel.SelectedItem
    {
        get => SelectedItem;
        set => SelectedItem = (T?)value;
    }
}

public sealed partial class NumberBoxSettingItemViewModel : SettingItemViewModelBase
{
    readonly Action<double> _changedAction;
    private readonly Func<double, string> _headerTextConverter;

    public NumberBoxSettingItemViewModel(
        string label,
        double firstValue,
        double minValue,
        double maxValue,
        double valueStep,
        Action<double> changedAction,
        Func<double, string> headerTextConverter)
    {
        Label = label;
        FirstValue = firstValue;
        MinValue = minValue;
        MaxValue = maxValue;
        ValueStep = valueStep;
        _changedAction = changedAction;
        _headerTextConverter = headerTextConverter;
        _headerText = _headerTextConverter(firstValue);
    }

    public NumberBoxSettingItemViewModel(
        string label,
        string desc,
        double firstValue,
        double minValue,
        double maxValue,
        double valueStep,
        Action<double> changedAction,
        Func<double, string> headerTextConverter)
    {
        Label = label;
        Desc = desc;
        FirstValue = firstValue;
        MinValue = minValue;
        MaxValue = maxValue;
        ValueStep = valueStep;
        _changedAction = changedAction;
        _headerTextConverter = headerTextConverter;
        _headerText = _headerTextConverter(firstValue);
    }

    public string Label { get; }
    public string? Desc { get; }
    public double FirstValue { get; }
    public double MinValue { get; }
    public double MaxValue { get; }
    public double ValueStep { get; }

    [ObservableProperty]
    string _headerText = "";

    public void OnValueChanged(object sender, NumberBoxValueChangedEventArgs args)
    {
        _changedAction(args.NewValue);
        HeaderText = _headerTextConverter(args.NewValue);
    }
}

public class SliderSettingItemViewModel : SettingItemViewModelBase
{
    readonly Action<double> _changedAction;

    public SliderSettingItemViewModel(string label, double firstValue, double minValue, double maxValue, double valueStep, Action<double> changedAction)
    {
        Label = label;
        FirstValue = firstValue;
        MinValue = minValue;
        MaxValue = maxValue;
        ValueStep = valueStep;
        _changedAction = changedAction;
    }

    public SliderSettingItemViewModel(string label, string desc, double firstValue, double minValue, double maxValue, double valueStep, Action<double> changedAction)
    {
        Label = label;
        Desc = desc;
        FirstValue = firstValue;
        MinValue = minValue;
        MaxValue = maxValue;
        ValueStep = valueStep;
        _changedAction = changedAction;
    }

    public string Label { get; }
    public string? Desc { get; }
    public double FirstValue { get; }
    public double MinValue { get; }
    public double MaxValue { get; }
    public double ValueStep { get; }

    public void OnValueChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        _changedAction(args.NewValue);
    }
}

public class UpdatableTextSettingItemViewModel : SettingItemViewModelBase, IDisposable 
{
    public string Label { get; }
    public IReadOnlyBindableReactiveProperty<string?> Text { get; }
    public UpdatableTextSettingItemViewModel(string label, Observable<string?> textObservable)
    {
        Label = label;
        Text = textObservable.ToReadOnlyBindableReactiveProperty();
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
        ActionCommand = new ReactiveCommand();
        ActionCommand.SubscribeAwait(async (b, ct) => await buttonAction());
    }

    public ButtonSettingItemViewModel(string buttonLabel, string label, string description, Func<Task> buttonAction, Observable<bool> canExecuteObservable)
    {
        ButtonLabel = buttonLabel;
        Label = label;
        Description = description;
        ActionCommand = canExecuteObservable.ToReactiveCommand();
        ActionCommand.SubscribeAwait(async (b, ct) => await buttonAction());
    }

    public ButtonSettingItemViewModel(string buttonLabel, string label, string description, Action buttonAction, Observable<bool> canExecuteObservable)
    {
        ButtonLabel = buttonLabel;
        Label = label;
        Description = description;
        ActionCommand = canExecuteObservable.ToReactiveCommand();
        ActionCommand.Subscribe(_ => 
        {
            buttonAction();
        });
    }

    public string ButtonLabel { get; }
    public string Label { get; }


    [ObservableProperty]
    string? _description;

    public R3.ReactiveCommand ActionCommand { get; }

    public void Dispose()
    {
        ((IDisposable)ActionCommand).Dispose();
    }
}

public sealed partial class ThemeSelectSettingItemViewModel : SettingItemViewModelBase
{
    private readonly ApplicationSettings _applicationSettings;
    readonly IMessenger _messenger;

    public ThemeSelectSettingItemViewModel(string label, ApplicationSettings applicationSettings, IMessenger messenger)
    {
        Label = label;
        _applicationSettings = applicationSettings;
        _messenger = messenger;
        _selectedTheme = applicationSettings.Theme;
    }

    [ObservableProperty]
    ApplicationTheme _selectedTheme;

    partial void OnSelectedThemeChanged(ApplicationTheme value)
    {
        _applicationSettings.Theme = value;
        _messenger.Send<ThemeChangeRequestMessage>(new(value));
    }

    public IReadOnlyList<ApplicationTheme> ThemeItems { get; } = new[] { ApplicationTheme.Default, ApplicationTheme.Light, ApplicationTheme.Dark };

    public string Label { get; }
}

public sealed partial class LocaleSelectSettingItemViewModel : SettingItemViewModelBase
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

    readonly ApplicationSettings _applicationSettings;

    [RelayCommand]
    void RestartApplication()
    {
        _ = Windows.ApplicationModel.Core.CoreApplication.RequestRestartAsync("");
    }
}
