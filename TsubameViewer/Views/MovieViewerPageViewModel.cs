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
using System.Windows.Input;
using TsubameViewer.Core.Infrastructure;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.PageNavigation;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Media;
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
        _isPlayerRotateEnabled = Read(false, nameof(IsPlayerRotateEnabled));
        _mediaRotate = (MediaRotation)Read((int)MediaRotation.Clockwise90Degrees, nameof(PlayerRotate));
        _isPlayerStretchEnabled = Read(false, nameof(IsPlayerStretchEnabled));
        _playerStretch = (Stretch)Read((int)Stretch.UniformToFill, nameof(PlayerStretch));
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

    bool _isPlayerRotateEnabled;
    public bool IsPlayerRotateEnabled
    {
        get => _isPlayerRotateEnabled;
        set => SetProperty(ref _isPlayerRotateEnabled, value);
    }

    MediaRotation _mediaRotate;
    public MediaRotation PlayerRotate
    {
        get => _mediaRotate;
        set => SetProperty(_mediaRotate, value, this, (m, v) => m.Save((int)(m._mediaRotate = v), nameof(PlayerRotate)));
    }

    bool _isPlayerStretchEnabled;
    public bool IsPlayerStretchEnabled
    {
        get => _isPlayerStretchEnabled;
        set => SetProperty(ref _isPlayerStretchEnabled, value);
    }

    Stretch _playerStretch;
    public Stretch PlayerStretch
    {
        get => _playerStretch;
        set => SetProperty(_playerStretch, value, this, (m, v) => m.Save((int)(m._playerStretch = v), nameof(PlayerStretch)));
    }
}

public sealed partial class MovieViewerPageViewModel : NavigationAwareViewModelBase
{
    public ICommand ToggleFullScreenCommand { get; set; }

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
        ThumbnailManager = thumbnailManager;
        _folderLastIntractItemManager = folderLastIntractItemManager;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        RecyclableMemoryStreamManager = recyclableMemoryStreamManager;
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
    public ThumbnailImageManager ThumbnailManager { get; }
    private readonly LastIntractItemRepository _folderLastIntractItemManager;
    private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    public RecyclableMemoryStreamManager RecyclableMemoryStreamManager { get; }
    
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
                    Title = MovieFile.Name;
                }
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
}