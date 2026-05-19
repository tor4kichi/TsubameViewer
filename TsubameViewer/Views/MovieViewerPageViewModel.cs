using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Infrastructure;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.PageNavigation;
using Windows.Media.Core;
using Windows.Storage;
using Windows.UI.ViewManagement;
#nullable enable
namespace TsubameViewer.ViewModels;


public sealed class MovieViewerPageSettings : FlagsRepositoryBase
{
    public MovieViewerPageSettings()
    {
        _isRepeat = Read(false, nameof(IsRepeat));
        _isMuted = Read(false, nameof(IsMuted));
        _playbackRate = Read(1d, nameof(PlaybackRate));
        _isHorizontalMirror = Read(false, nameof(IsHorizontalMirror));
        _soundVolume = Read(0.5d, nameof(SoundVolume));
    }

    bool _isRepeat;
    public bool IsRepeat
    {
        get => _isRepeat;
        set => SetProperty(ref _isRepeat, value);
    }

    bool _isMuted;
    public bool IsMuted
    {
        get => _isMuted;
        set => SetProperty(ref _isMuted, value);
    }


    double _soundVolume;
    public double SoundVolume
    {
        get => _soundVolume;
        set => SetProperty(ref _soundVolume, value);
    }

    double _playbackRate;
    public double PlaybackRate
    {
        get => _playbackRate;
        set => SetProperty(ref _playbackRate, value);
    }

    bool _isHorizontalMirror;
    public bool IsHorizontalMirror
    {
        get => _isHorizontalMirror;
        set => SetProperty(ref _isHorizontalMirror, value);
    }
}

public sealed partial class MovieViewerPageViewModel : NavigationAwareViewModelBase
{
    public MovieViewerPageViewModel(
        IMessenger messenger,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        AlbamRepository albamRepository,
        ImageCollectionManager imageCollectionManager,
        ImageViewerSettings imageCollectionSettings,
        LocalBookmarkRepository bookmarkManager,
        RecentlyAccessRepository recentlyAccessRepository,
        ThumbnailImageManager thumbnailManager,
        LastIntractItemRepository folderLastIntractItemManager,
        DisplaySettingsByPathRepository displaySettingsByPathRepository,
        RecyclableMemoryStreamManager recyclableMemoryStreamManager,
        MovieViewerPageSettings pageSettings)
    {
        _messenger = messenger;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _albamRepository = albamRepository;
        _imageCollectionManager = imageCollectionManager;
        _imageCollectionSettings = imageCollectionSettings;
        BookmarkManager = bookmarkManager;
        _recentlyAccessRepository = recentlyAccessRepository;
        _thumbnailManager = thumbnailManager;
        _folderLastIntractItemManager = folderLastIntractItemManager;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        _recyclableMemoryStreamManager = recyclableMemoryStreamManager;
        PageSettings = pageSettings;
    }

    CancellationToken _navigationCt;
    private readonly IMessenger _messenger;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly AlbamRepository _albamRepository;
    private readonly ImageCollectionManager _imageCollectionManager;
    private readonly ImageViewerSettings _imageCollectionSettings;
    public LocalBookmarkRepository BookmarkManager { get; }
    public MovieViewerPageSettings PageSettings { get; }

    private readonly RecentlyAccessRepository _recentlyAccessRepository;
    private readonly ThumbnailImageManager _thumbnailManager;
    private readonly LastIntractItemRepository _folderLastIntractItemManager;
    private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;
    


    [ObservableProperty]
    StorageFile? _movieFile;

    string? _pathForSettings = null;

    [ObservableProperty]
    string _parentFolderOrArchiveName = "";

    [ObservableProperty]
    string _title = "";

    public override async Task OnNavigatedToAsync(INavigationParameters parameters, CancellationToken ct)
    {
        _navigationCt = ct;
#if DEBUG
        long time = TimeProvider.System.GetTimestamp();
#endif
        var mode = parameters.GetNavigationMode();
        string? firstDisplayPageName = null;
        if (parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string escapedPath))
        {
            (string newPath, firstDisplayPageName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(escapedPath));

            _sourceStorageItemsRepository.ThrowIfPathIsUnauthorizedAccess(newPath);

            await _messenger.WorkWithBusyWallAsync(async (ct) =>
            {
                var (imageSource, imageCollectionContext) = await _imageCollectionManager.GetImageSourceAndContextAsync(newPath, string.Empty, ct);

                _pathForSettings = imageCollectionContext switch
                {
                    FolderImageCollectionContext folderICC when folderICC.Folder is not null => folderICC.Folder.Path,
                    ArchiveImageCollectionContext ArchiveICC => ArchiveICC.File.Path,
                    _ => imageSource.Path,
                };

                _recentlyAccessRepository.AddWatched(_pathForSettings);

                if (imageSource.StorageItem is StorageFile file)
                {
                    MovieFile = file;
                    ApplicationView.GetForCurrentView().Title = Title = file.Name;
                }
                else
                {
                    
                }

                //_currentImageSource = imageSource;
                //_imageCollectionContext = imageCollectionContext;

                //DisplaySortTypeInheritancePath = null;

                //var settings = _displaySettingsByPathRepository.GetFolderAndArchiveSettings(_pathForSettings);
                //if (settings != null)
                //{
                //    SelectedFileSortType = settings.Sort;
                //}
                //else if (_displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(_pathForSettings) is not null and var parentSort && parentSort.ChildItemDefaultSort != null)
                //{
                //    DisplaySortTypeInheritancePath = parentSort.Path;
                //    SelectedFileSortType = parentSort.ChildItemDefaultSort.Value;
                //}
                //else
                //{
                //    SelectedFileSortType = DefaultFileSortType;
                //}

                //_CurrentImageIndex = 0;

                //if (string.IsNullOrEmpty(firstDisplayPageName)
                //    && SupportedFileTypesHelper.IsSupportedImageFileExtension(newPath)
                //    )
                //{
                //    firstDisplayPageName = Path.GetFileName(newPath);
                //}

                //if (imageCollectionContext is OnlyOneFileImageCollectionContext)
                //{
                //    IfAllFilesWannaWatchThenRegistrationFolderAtApp = true;
                //}
                //else
                //{
                //    IfAllFilesWannaWatchThenRegistrationFolderAtApp = false;
                //}

                //await RefreshItems(imageSource, imageCollectionContext, ct);

            }, ct);
        }
        else if (parameters.TryGetValue(PageNavigationConstants.AlbamPathKey, out string escapedAlbamPath))
        {
            (string albamIdString, firstDisplayPageName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(escapedAlbamPath));
        }
    }

    public override void OnNavigatedFrom(INavigationParameters parameters)
    {        
        base.OnNavigatedFrom(parameters);
    }


    [RelayCommand]
    public void ToggleFullScreen()
    {
        var appView = ApplicationView.GetForCurrentView();
        if (appView.IsFullScreenMode)
        {
            appView.ExitFullScreenMode();            
        }
        else
        {
            appView.TryEnterFullScreenMode();
        }
    }

    [RelayCommand]
    void ToggleHorizontalMirrorDisplay()
    {
        IsHorizontalMirrorModeEnabled = !IsHorizontalMirrorModeEnabled;
    }

    [ObservableProperty]
    bool _isHorizontalMirrorModeEnabled;


    [ObservableProperty]
    bool _isTransformModeEnabled;

    [RelayCommand]
    void ToggleTransformMode()
    {
        IsTransformModeEnabled = !IsTransformModeEnabled;
    }
}