using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using Microsoft.IO;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Infrastructure;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.Albam.Commands;
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
        _isFFmpegUseFirstToMediaSourceFactory = Read(false, nameof(IsFFmpegUseFirstToMediaSourceFactory));
        _videoFrameThumbnailSize = Read(200, nameof(VideoFrameThumbnailSize));
        _subtitleEnabledByLanguage = Read(new Dictionary<string, bool>(), "SubtitleEnabledByLanguage");
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

    bool _isFFmpegUseFirstToMediaSourceFactory;
    public bool IsFFmpegUseFirstToMediaSourceFactory
    {
        get => _isFFmpegUseFirstToMediaSourceFactory;
        set => SetProperty(ref _isFFmpegUseFirstToMediaSourceFactory, value);
    }


    int _videoFrameThumbnailSize;
    public int VideoFrameThumbnailSize
    {
        get => _videoFrameThumbnailSize;
        set => SetProperty(ref _videoFrameThumbnailSize, value);
    }

    Dictionary<string, bool> _subtitleEnabledByLanguage;
    public bool GetSubtitleLanguageEnabled(string language)
    {
        var lower = language.ToLowerInvariant();
        return _subtitleEnabledByLanguage.TryGetValue(language, out bool isEnabeld) ? isEnabeld : false;
    }

    public void SetSubtitleLanguageEnabled(string language, bool isEnabled)
    {
        _subtitleEnabledByLanguage[language] = isEnabled;
        Save(_subtitleEnabledByLanguage, "SubtitleEnabledByLanguage");
    }

    public void ClearSubtitleLanguageEnabled()
    {
        _subtitleEnabledByLanguage.Clear();
        Save(_subtitleEnabledByLanguage, "SubtitleEnabledByLanguage");
    }
}

public sealed partial class MovieViewerPageViewModel : NavigationAwareViewModelBase
{
    public ICommand? ToggleFullScreenCommand { get; set; }

    public MovieViewerPageViewModel(
        IMessenger messenger,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        AlbamRepository albamRepository,
        FavoriteAlbam favoriteAlbam,
        ImageCollectionManager imageCollectionManager,
        ImageViewerSettings imageCollectionSettings,
        LocalBookmarkRepository bookmarkManager,
        StorageItemSettings storageItemSettings,
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
        _favoriteAlbam = favoriteAlbam;
        _imageCollectionManager = imageCollectionManager;
        _imageCollectionSettings = imageCollectionSettings;
        BookmarkManager = bookmarkManager;
        StorageItemSettings = storageItemSettings;
        _recentlyAccessRepository = recentlyAccessRepository;
        ThumbnailManager = thumbnailManager;
        _folderLastIntractItemManager = folderLastIntractItemManager;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        RecyclableMemoryStreamManager = recyclableMemoryStreamManager;
        PageSettings = pageSettings;
    }

    CancellationToken _navigationCt;
    readonly IMessenger _messenger;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    readonly AlbamRepository _albamRepository;
    private readonly FavoriteAlbam _favoriteAlbam;
    readonly ImageCollectionManager _imageCollectionManager;
    readonly ImageViewerSettings _imageCollectionSettings;
    public LocalBookmarkRepository BookmarkManager { get; }
    public StorageItemSettings StorageItemSettings { get; }
    public MovieViewerPageSettings PageSettings { get; }

    readonly RecentlyAccessRepository _recentlyAccessRepository;
    public ThumbnailImageManager ThumbnailManager { get; }
    readonly LastIntractItemRepository _folderLastIntractItemManager;
    readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    public RecyclableMemoryStreamManager RecyclableMemoryStreamManager { get; }
    
    [ObservableProperty]
    StorageFile? _movieFile;

    string? _pathForSettings = null;

    [ObservableProperty]
    string _parentFolderOrArchiveName = "";

    [ObservableProperty]
    string _title = "";

    [ObservableProperty]
    bool _isFavoriteCurrentFolderOrArchive;

    [ObservableProperty]
    IImageSource? _currentImageSource;

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

                CurrentImageSource = imageSource;
            }, ct);

            Guard.IsNotNull(CurrentImageSource);
            IsFavoriteCurrentFolderOrArchive = _favoriteAlbam.IsFavorite(CurrentImageSource.Path);
            this.ObservePropertyChanged(x => x.IsFavoriteCurrentFolderOrArchive, false)
                .Subscribe((_favoriteAlbam, CurrentImageSource, _messenger), static (isFavorite, s) =>
                {                    
                    var (_favoriteAlbam, imageSource, _messenger) = s;
                    if (isFavorite)
                    {
                        _favoriteAlbam.AddFavoriteItem(imageSource);
                        _messenger.SendShowTextNotificationMessage("Favorite_Added".Translate(imageSource.Name));
                        _messenger.Send(new ImageSourceFavoriteChanged(imageSource.Path, true));
                    }
                    else
                    {
                        _favoriteAlbam.DeleteFavoriteItem(imageSource);
                        _messenger.SendShowTextNotificationMessage("Favorite_Removed".Translate(imageSource.Name));
                        _messenger.Send(new ImageSourceFavoriteChanged(imageSource.Path, false));

                    }
                })
                .RegisterTo(_navigationCt);
        }
    }

    public override void OnNavigatedFrom(INavigationParameters parameters)
    {        
        if (MovieFile?.Path is { } path)
        {
            _messenger.Send(new LatestContentViewUpdateMessage(path));
        }

        base.OnNavigatedFrom(parameters);
    }
}