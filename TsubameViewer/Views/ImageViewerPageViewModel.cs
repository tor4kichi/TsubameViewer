using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI;
using I18NPortable;
using LiteDB;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using R3;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Helpers;
using TsubameViewer.Services;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.ViewModels.SourceFolders;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.ViewModels.ViewManagement.Commands;
using TsubameViewer.Views;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using StorageItemTypes = TsubameViewer.Core.Models.StorageItemTypes;

namespace TsubameViewer.ViewModels;

public sealed class ImageLoadedMessage : AsyncRequestMessage<Unit>
{
    public ImageLoadedMessage() : base()
    {
    }
}


public sealed partial class ImageViewerPageViewModel : NavigationAwareViewModelBase
{
    private IImageSource _currentImageSource;
    private IImageCollectionContext _imageCollectionContext;

    CancellationTokenSource? _imageLoadingCts;

    // View側の画像読み込みと共有する
    // アーカイブ画像のデータ読み込みとEntriesの列挙を同時に行えないため。
    internal Core.AsyncLock _imageLoadingLock = new();

    public int ImageCount => Images?.Length ?? 0;

    private IImageSource[] _Images;
    public IImageSource[] Images
    {
        get { return _Images; }
        private set 
        { 
            if (SetProperty(ref _Images, value))
            {
                OnPropertyChanged(nameof(ImageCount));
            }
        }
    }

    private bool _nowImagesChanging = false;

    int _CurrentImageIndex;
    public int CurrentImageIndex
    {
        get => _CurrentImageIndex;
        set
        {
            if (_nowImagesChanging) { return; }
            
            SetProperty(ref _CurrentImageIndex, value);
            DisplayCurrentImageIndex = value + 1;
        }
    }

    string _pathForSettings = null;

    string _ParentFolderOrArchiveName;
    public string ParentFolderOrArchiveName
    {
        get { return _ParentFolderOrArchiveName; }
        private set { SetProperty(ref _ParentFolderOrArchiveName, value); }
    }

    [ObservableProperty]
    int _displayCurrentImageIndex;

    [ObservableProperty]
    FileSortType _selectedFileSortType;

    readonly FileSortType DefaultFileSortType = FileSortType.TitleAscending;

    string _DisplaySortTypeInheritancePath;
    public string DisplaySortTypeInheritancePath
    {
        get { return _DisplaySortTypeInheritancePath; }
        private set { SetProperty(ref _DisplaySortTypeInheritancePath, value); }
    }

    [ObservableProperty]
    double _canvasWidth;

    [ObservableProperty]
    double _canvasHeight;




    string _title;
    public string Title
    {
        get { return _title; }
        private set { SetProperty(ref _title, value); }
    }


    string _page1Name;
    public string Page1Name
    {
        get { return _page1Name; }
        private set { SetProperty(ref _page1Name, value); }
    }

    string _page2Name;
    public string Page2Name
    {
        get { return _page2Name; }
        private set { SetProperty(ref _page2Name, value); }
    }


    private bool _page1Favorite;
    public bool Page1Favorite
    {
        get { return _page1Favorite; }
        private set { SetProperty(ref _page1Favorite, value); }
    }

    private bool _page2Favorite;
    public bool Page2Favorite
    {
        get { return _page2Favorite; }
        private set { SetProperty(ref _page2Favorite, value); }
    }

    private IImageSource[] _currentDisplayImageSources;
    public IImageSource[] CurrentDisplayImageSources
    {
        get => _currentDisplayImageSources;
        set => SetProperty(ref _currentDisplayImageSources, value);
    }

    string _pageFolderName;
    public string PageFolderName
    {
        get { return _pageFolderName; }
        set { SetProperty(ref _pageFolderName, value); }
    }

    private string[] _pageFolderNames;
    public string[] PageFolderNames
    {
        get { return _pageFolderNames; }
        set { SetProperty(ref _pageFolderNames, value); }
    }


    private StorageItemTypes _ItemType;
    public StorageItemTypes ItemType
    {
        get { return _ItemType; }
        private set { SetProperty(ref _ItemType, value); }
    }

    private bool _nowImageLoadingLongRunning;
    public bool NowImageLoadingLongRunning
    {
        get { return _nowImageLoadingLongRunning; }
        set { SetProperty(ref _nowImageLoadingLongRunning, value); }
    }

    readonly static char[] SeparateChars = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

    public ApplicationSettings ApplicationSettings { get; }
    public ViewerSettings ViewerSettings { get; }
    public ImageViewerSettings ImageViewerSettings { get; }

    [ObservableProperty]
    bool _isLeftBindingEnabled;

    [ObservableProperty]
    public bool _isDoubleViewEnabled;

    [ObservableProperty]
    public double _defaultZoom;

    [ObservableProperty]
    private bool _requireRefresh;

    [ObservableProperty]
    bool _isFavoriteCurrentFolderOrArchive;

    [ObservableProperty]
    bool _isFavoriteAlbamDisplay;

    [ObservableProperty]
    IImageSource? _prevImageSource;

    [ObservableProperty]
    IImageSource? _nextImageSource;

    [ObservableProperty]
    bool _nowEditTransformMode;

    [ObservableProperty]
    double _transformScale = 1;

    [RelayCommand]
    async Task OpenMangaFileAsync(IImageSource? imageSource)
    {
        if (imageSource == null) { return; }
        if (_windowContext.IsPrimary)
        {
            var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
            _ = _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
        }
        else
        {
            _ = _secondaryWindowService.OpenViewerAsync(imageSource, false);
        }
    }

    readonly IMessenger _messenger;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    readonly AlbamRepository _albamRepository;
    readonly FavoriteAlbam _favoriteAlbam;
    readonly ImageCollectionManager _imageCollectionManager;
    readonly LocalBookmarkRepository _bookmarkManager;
    readonly StorageItemSettings _storageItemSettings;
    readonly RecentlyAccessRepository _recentlyAccessRepository;
    internal readonly ThumbnailImageManager _thumbnailManager;
    readonly FolderListingSettings _folderListingSettings;
    readonly LastIntractItemRepository _folderLastIntractItemManager;
    readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    readonly SecondaryWindowService _secondaryWindowService;
    readonly IWindowManagementAware _windowContext;

    public ImageViewerPageViewModel(
        IMessenger messenger,
        ApplicationSettings applicationSettings,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        AlbamRepository albamRepository,
        FavoriteAlbam favoriteAlbam,
        ImageCollectionManager imageCollectionManager,
        ViewerSettings viewerSettings,
        ImageViewerSettings imageCollectionSettings,
        LocalBookmarkRepository bookmarkManager,
        StorageItemSettings storageItemSettings,
        RecentlyAccessRepository recentlyAccessRepository,
        ThumbnailImageManager thumbnailManager,
        FolderListingSettings folderListingSettings,
        LastIntractItemRepository folderLastIntractItemManager,
        DisplaySettingsByPathRepository displaySettingsByPathRepository,
        ToggleFullScreenCommand toggleFullScreenCommand,
        BackNavigationCommand backNavigationCommand,
        FavoriteToggleCommand favoriteToggleCommand,
        RefreshNavigationCommand refreshNavigationCommand,
        ChangeStorageItemThumbnailImageCommand changeStorageItemThumbnailImageCommand,
        OpenWithExplorerCommand openWithExplorerCommand,
        OpenWithExternalApplicationCommand openWithExternalApplicationCommand,
        SecondaryWindowService secondaryWindowService
        )
    {
        _messenger = messenger;
        ApplicationSettings = applicationSettings;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _albamRepository = albamRepository;
        _favoriteAlbam = favoriteAlbam;
        _imageCollectionManager = imageCollectionManager;
        ViewerSettings = viewerSettings;
        ImageViewerSettings = imageCollectionSettings;
        ToggleFullScreenCommand = toggleFullScreenCommand;
        BackNavigationCommand = backNavigationCommand;
        FavoriteToggleCommand = favoriteToggleCommand;
        RefreshCommand = refreshNavigationCommand;
        ChangeStorageItemThumbnailImageCommand = changeStorageItemThumbnailImageCommand;
        ChangeStorageItemThumbnailImageCommand.IsArchiveThumbnailSetToFile = true;
        OpenWithExplorerCommand = openWithExplorerCommand;
        OpenWithExternalApplicationCommand = openWithExternalApplicationCommand;
        _bookmarkManager = bookmarkManager;
        _storageItemSettings = storageItemSettings;
        _recentlyAccessRepository = recentlyAccessRepository;
        _thumbnailManager = thumbnailManager;
        _folderListingSettings = folderListingSettings;
        _folderLastIntractItemManager = folderLastIntractItemManager;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        _secondaryWindowService = secondaryWindowService;
        _windowContext = _secondaryWindowService.GetCurentFocusWindow();

        ClearDisplayImages();
        SelectedFileSortType = DefaultFileSortType;
       
    }

    [RelayCommand]
    async Task ToggleLeftBinding()
    {
        static bool SwapIfDoubleView(BitmapImage[] images)
        {
            if (images.Any() && images.Length == 2)
            {
                (images[0], images[1]) = (images[1], images[0]);

                return true;
            }
            else
            {
                return false;
            }
        }

        IsLeftBindingEnabled = !IsLeftBindingEnabled;
        ImageViewerSettings.SetViewerSettingsPerPath(_currentImageSource.Path, IsDoubleViewEnabled, IsLeftBindingEnabled, DefaultZoom);

        var images = GetSourceImages();
        if (images.Length == 2)
        {
            (images[0], images[1]) = (images[1], images[0]);
        }
    }

    [RelayCommand]
    async Task ToggleDoubleView()
    {
        IsDoubleViewEnabled = !IsDoubleViewEnabled;
        ImageViewerSettings.SetViewerSettingsPerPath(_currentImageSource.Path, IsDoubleViewEnabled, IsLeftBindingEnabled, DefaultZoom);
        Debug.WriteLine($"window w={CanvasWidth:F0}, h={CanvasHeight:F0}");
        await ResetImageIndex(CurrentImageIndex);
    }


    public override void OnNavigatedFrom(INavigationParameters parameters)
    {
        if (_imageCollectionContext is FolderImageCollectionContext folderContext)
        {
            try
            {
                _messenger.Send(new LatestContentViewUpdateMessage(folderContext.Folder.Path));
            }
            catch { }
        }
        else if (_imageCollectionContext is ArchiveImageCollectionContext archiveContext)
        {
            try
            {
                _messenger.Send(new LatestContentViewUpdateMessage(archiveContext.File.Path));
            }
            catch { }
        }

        ClearDisplayImages();
        IsAlreadySetDisplayImages = false;

        if (Images?.Any() ?? false)
        {
            _nowCurrenImageIndexChanging = true;
            Images = null;
            _nowCurrenImageIndexChanging = false;
        }

        (_currentImageSource as IDisposable)?.Dispose();
        _currentImageSource = null;
        (_imageCollectionContext as IDisposable)?.Dispose();
        _imageCollectionContext = null;
        _imageLoadingCts?.Cancel();
        _imageLoadingCts?.Dispose();
        _imageLoadingCts = null;

        ParentFolderOrArchiveName = String.Empty;
        _pageMovedCount = 0;

        _messenger.Unregister<AlbamItemAddedMessage>(this);
        _messenger.Unregister<AlbamItemRemovedMessage>(this);

        base.OnNavigatedFrom(parameters);
    }

    CancellationToken _navigationCt;

    int _pageMovedCount = 0;
    
    public override async Task OnNavigatedToAsync(INavigationParameters parameters, CancellationToken ct)
    {
        _navigationCt = ct;
#if DEBUG
        long time = TimeProvider.System.GetTimestamp();
#endif
        var mode = parameters.GetNavigationMode();

        _imageLoadingCts = new CancellationTokenSource();
        ClearDisplayImages();

        // 一旦ボタン類を押せないように変更通知
        GoNextImageCommand.NotifyCanExecuteChanged();
        GoPrevImageCommand.NotifyCanExecuteChanged();

        string firstDisplayPageName = null;
        if (mode is NavigationMode.New or NavigationMode.Back or NavigationMode.Forward or NavigationMode.Refresh)
        {
            (_imageCollectionContext as IDisposable)?.Dispose();
            _imageCollectionContext = null;
            Page1Name = null;
            Title = null;
            PageFolderName = null;
            IfAllFilesWannaWatchThenRegistrationFolderAtApp = false;

            if (parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string escapedPath))
            {
                (string newPath, firstDisplayPageName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(escapedPath));

                _sourceStorageItemsRepository.ThrowIfPathIsUnauthorizedAccess(newPath);

#if DEBUG
                //await _messenger.WorkWithBusyWallAsync(async ct => await Task.Delay(TimeSpan.FromSeconds(5), ct), _leavePageCancellationTokenSource.Token);
#endif
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

                    Images = default;

                    _currentImageSource = imageSource;
                    _imageCollectionContext = imageCollectionContext;

                    Title = _imageCollectionContext.Name;
                    DisplaySortTypeInheritancePath = null;

                    var settings = _displaySettingsByPathRepository.GetFolderAndArchiveSettings(_pathForSettings);
                    if (settings != null)
                    {
                        SelectedFileSortType = settings.Sort;
                    }
                    else if (_displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(_pathForSettings) is not null and var parentSort && parentSort.ChildItemDefaultSort != null)
                    {
                        DisplaySortTypeInheritancePath = parentSort.Path;
                        SelectedFileSortType = parentSort.ChildItemDefaultSort.Value;
                    }
                    else
                    {
                        SelectedFileSortType = DefaultFileSortType;
                    }

                    (IsDoubleViewEnabled, IsLeftBindingEnabled, DefaultZoom)
                        = ImageViewerSettings.GetViewerSettingsPerPath(_currentImageSource.Path);

                    _CurrentImageIndex = 0;

                    if (string.IsNullOrEmpty(firstDisplayPageName)
                        && SupportedFileTypesHelper.IsSupportedImageFileExtension(newPath)
                        )
                    {
                        firstDisplayPageName = Path.GetFileName(newPath);
                    }

                    if (imageCollectionContext is OnlyOneFileImageCollectionContext)
                    {
                        IfAllFilesWannaWatchThenRegistrationFolderAtApp = true;
                    }
                    else
                    {
                        IfAllFilesWannaWatchThenRegistrationFolderAtApp = false;
                    }

                    await RefreshItems(imageSource, imageCollectionContext, ct);


                    NextImageSource = null;
                    PrevImageSource = null;
                    try
                    {
                        if (imageCollectionContext is ArchiveImageCollectionContext or EPubImageCollectionContext
                            && ViewerSettings.IsDetectSimiralyFileNameNeighborsEnabled
                            && await _sourceStorageItemsRepository.TryGetStorageItemFromPath(Path.GetDirectoryName(newPath)) is StorageFolder parentFolder)
                        {
                            var query = parentFolder.CreateFileQuery();
                            query.ApplyNewQueryOptions(new QueryOptions(CommonFileQuery.DefaultQuery, [..SupportedFileTypesHelper.SupportedArchiveFileExtensions, .. SupportedFileTypesHelper.SupportedEBookFileExtensions]));
                            var currentItemIndex = await query.FindStartIndexAsync(imageSource.Name);

                            if (currentItemIndex - 1 >= 0
                                && await query.GetFilesAsync(currentItemIndex - 1, 1) is { } prevFiles
                                && prevFiles.ElementAtOrDefault(0) is { } prevFile
                                && StringLevenshteinHelper.GetSimilarityNormalized(_currentImageSource.Name, prevFile.Name) >= ViewerSettings.ThresholdOfSimilarityFileNameNaighborsNormalized)
                            {
                                PrevImageSource = new StorageItemImageSource(prevFile);
                            }

                            if (await query.GetFilesAsync(currentItemIndex + 1, 1) is { } nextFiles
                                && nextFiles.ElementAtOrDefault(0) is { } nextFile
                                && StringLevenshteinHelper.GetSimilarityNormalized(_currentImageSource.Name, nextFile.Name) >= ViewerSettings.ThresholdOfSimilarityFileNameNaighborsNormalized)
                            {
                                NextImageSource = new StorageItemImageSource(nextFile);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                    }
                }, ct);
            }
            else if (parameters.TryGetValue(PageNavigationConstants.AlbamPathKey, out string escapedAlbamPath))
            {
                (string albamIdString, firstDisplayPageName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(escapedAlbamPath));

                if (_currentImageSource?.Path != albamIdString)
                {
                    var albam = _albamRepository.GetAlbam(Guid.Parse(albamIdString));

                    Guard.IsNotNull(albam);

                    await _messenger.WorkWithBusyWallAsync(async (ct) =>
                    {
                        AlbamImageCollectionContext albamImageCollectionContext = new(albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _messenger);
                        AlbamImageSource albamImageSource = new(albam, albamImageCollectionContext);

                        _currentImageSource = albamImageSource;
                        _imageCollectionContext = albamImageCollectionContext;
                        Images = default;
                        _CurrentImageIndex = 0;

                        Title = albam.Name;

                        DisplaySortTypeInheritancePath = null;
                        _pathForSettings = null;

                        var settings = _displaySettingsByPathRepository.GetAlbamDisplaySettings(albam._id);
                        if (settings != null)
                        {
                            SelectedFileSortType = settings.Sort;
                        }
                        else
                        {
                            SelectedFileSortType = DefaultFileSortType;
                        }

                        (IsDoubleViewEnabled, IsLeftBindingEnabled, DefaultZoom)
                            = ImageViewerSettings.GetViewerSettingsPerPath(_currentImageSource.Path);

                        await RefreshItems(albamImageSource, albamImageCollectionContext, ct);
                    }, ct);
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }

#if DEBUG
        Debug.WriteLine($"RefreshItems: {TimeProvider.System.GetElapsedTime(time)}");
        time = TimeProvider.System.GetTimestamp();
#endif

        var bkmk = _bookmarkManager.GetBookmarkFacade(_pathForSettings);
        // 以下の場合に表示内容を更新する
        //    1. 表示フォルダが変更された場合
        //    2. 前回の更新が未完了だった場合

        // 表示する画像を決める
        if (mode == NavigationMode.Forward 
            || parameters.ContainsKey(PageNavigationConstants.Restored) 
            || (mode == NavigationMode.New && string.IsNullOrEmpty(firstDisplayPageName))
            )
        {
            if (_imageCollectionContext == null) { return; }
            if (string.IsNullOrEmpty(_pathForSettings) is false)
            {
                if (!string.IsNullOrEmpty(bkmk.PageName))
                {
                    try
                    {
                        _CurrentImageIndex = await _imageCollectionContext.GetImageFileIndexFromKeyAsync(bkmk.PageName, SelectedFileSortType, ct);
                    }
                    catch
                    {
                        _CurrentImageIndex = 0;
                    }
                }
            }
        }
        else if (mode == NavigationMode.New && !string.IsNullOrEmpty(firstDisplayPageName))
        {
            try
            {
                _CurrentImageIndex = await _imageCollectionContext.GetImageFileIndexFromKeyAsync(firstDisplayPageName, SelectedFileSortType, ct);
            }
            catch
            {
                _CurrentImageIndex = 0;
            }
        }

        _nowCurrenImageIndexChanging = true;
        OnPropertyChanged(nameof(CurrentImageIndex));
        _nowCurrenImageIndexChanging = false;

        await ResetImageIndex(CurrentImageIndex);

        var db = new DisposableBuilder();

#if DEBUG

        Debug.WriteLine($"SetImages: {TimeProvider.System.GetElapsedTime(time)}");
        time = TimeProvider.System.GetTimestamp();
#endif
       
        IsAlreadySetDisplayImages = true;

        // 表示画像が揃ったら改めてボタンを有効化
        GoNextImageCommand.NotifyCanExecuteChanged();
        GoPrevImageCommand.NotifyCanExecuteChanged();

        // 画像更新
        Observable.Merge(
            this.ObservePropertyChanged(x => x.CurrentImageIndex, true).AsUnitObservable(),
            this.ObservePropertyChanged(x => x.NowDoubleImageView, false).AsUnitObservable()
            )
            .Subscribe(_ =>
            {
                {
                    if (Images == null || Images.Length == 0) { return; }
                    if (_imageCollectionContext is null) { return; }
                    int imageIndex = CurrentImageIndex;
                    var imageSources = GetSourceImages();
                    UpdateDisplayName(imageSources);

                    _currentDisplayImageSources ??= new IImageSource[2];
                    if (imageSources.Length == 1)
                    {
                        var firstImage = imageSources[0];
                        _currentDisplayImageSources[0] = firstImage;
                        _currentDisplayImageSources[1] = null;

                        Page1Favorite = firstImage != null ? _albamRepository.IsExistAlbamItem(firstImage.Path) : false;
                        Page2Favorite = false;
                    }
                    else if (imageSources.Length == 2)
                    {
                        var firstImage = imageSources[0];
                        var secondImage = imageSources[1];
                        _currentDisplayImageSources[0] = firstImage;
                        _currentDisplayImageSources[1] = secondImage;

                        Page1Favorite = firstImage != null ? _albamRepository.IsExistAlbamItem(firstImage.Path) : false;
                        Page2Favorite = secondImage != null ? _albamRepository.IsExistAlbamItem(secondImage.Path) : false;
                    }

                    OnPropertyChanged(nameof(CurrentDisplayImageSources));
                }
            }).AddTo(ref db);

        this.ObservePropertyChanged(x => x.SelectedFileSortType)
            .Pairwise()
            .SubscribeAwait(async (pair, ct) =>
            {
                if (Images == null) { return; }
                var oldImage = await _imageCollectionContext.GetImageFileAtAsync(CurrentImageIndex, pair.Previous, ct);
                var newIndex = await _imageCollectionContext.GetImageFileIndexFromKeyAsync(oldImage.Name, pair.Current, ct);
                await ResetImageIndex(newIndex);
            })
            .AddTo(ref db);

        //_SizeChangedSubject
        //    .Where(x => Images != null)
        //    .Select(x => (X: CanvasWidth, Y:CanvasHeight))
        //    .Pairwise()
        //    .Where(x => x.Current != x.Previous)
        //    .Do(_ => NowImageLoadingLongRunning = true)
        //    .ThrottleLast(TimeSpan.FromMilliseconds(50))
        //    .SubscribeAwait(async (size, ct) =>
        //    {
        //        using (await _imageLoadingLock.LockAsync(CancellationToken.None))
        //        {
        //            ClearCachedImages();
        //        }

        //        await ResetImageIndex(CurrentImageIndex);
        //    })
        //    .AddTo(ref db);

        //ImageViewerSettings.ObservePropertyChanged(x => x.IsEnablePrefetch, false)
        //    .Subscribe(async isEnabledPrefetch => 
        //    {
        //        if (isEnabledPrefetch)
        //        {
        //            await PrefetchDisplayImagesAsync(IndexMoveDirection.Refresh, CurrentImageIndex, _imageLoadingCts.Token);
        //        }
        //    })
        //    .AddTo(ref db);

        _messenger.Register<AlbamItemAddedMessage>(this, (r, m) =>
        {
            var (albamId, path, itemType) = m.Value;
            if (albamId == FavoriteAlbam.FavoriteAlbamId)
            {
                if (_currentDisplayImageSources[0] != null && _currentDisplayImageSources[0].Path == path)
                {
                    Page1Favorite = true;
                }
                else if (_currentDisplayImageSources[1] != null && _currentDisplayImageSources[1].Path == path)
                {
                    Page2Favorite = true;
                }
            }
        });

        _messenger.Register<AlbamItemRemovedMessage>(this, (r, m) => 
        {
            var (albamId, path, itemType) = m.Value;
            if (albamId == FavoriteAlbam.FavoriteAlbamId)
            {
                if (_currentDisplayImageSources[0] != null && _currentDisplayImageSources[0].Path == path)
                {
                    Page1Favorite = false;
                }
                else if (_currentDisplayImageSources[1] != null && _currentDisplayImageSources[1].Path == path)
                {
                    Page2Favorite = false;
                }
            }
        });

        if (_imageCollectionContext?.IsSupportedFolderContentsChanged ?? false)
        {
            // アプリ内部操作も含めて変更を検知する
            bool requireRefresh = false;
            _imageCollectionContext.CreateImageFileChangedObserver()
                .Subscribe(_ =>
                {
                    requireRefresh = true;
                    Debug.WriteLine("Images Update required. " + _currentImageSource.Path);
                })
                .AddTo(ref db);

            Window.Current.WindowActivationStateChanged()    
                .ThrottleLast(TimeSpan.FromSeconds(1))
                .ObserveOnCurrentSynchronizationContext()
                .SubscribeAwait(async (visible, ct) =>
                {
                    if (visible && requireRefresh && _imageCollectionContext is not null)
                    {
                        requireRefresh = false;
                        var currentItemPath = (await _imageCollectionContext.GetImageFileAtAsync(CurrentImageIndex, SelectedFileSortType, ct)).Path;
                            await ReloadItemsAsync(_imageCollectionContext, ct);

                            try
                            {
                            var index = await _imageCollectionContext.GetImageFileIndexFromKeyAsync(currentItemPath, SelectedFileSortType, ct);
                                await ResetImageIndex(index >= 0 ? index : 0);
                            }
                            catch
                            {
                                if (await _imageCollectionContext.GetImageFileCountAsync(ct) > 0)
                                {
                                    await ResetImageIndex(0);
                                }
                            }


                        Debug.WriteLine("Images Updated. " + _currentImageSource.Path);
                    }
                })
                .AddTo(ref db);
        }
        
        this.ObservePropertyChanged(x => x.CurrentImageIndex, false)
            .Pairwise()
            .Subscribe(x => 
            {
                var (prev, imageIndex) = x;
                var imageSources = GetSourceImages();
                var imageSource = imageSources[0];
                if (imageSource == null) { return; }
                if (_currentImageSource.StorageItem is IStorageItem)
                {
                    using (bkmk.GetDeferSave())
                    {
                        bkmk.SetReadPosition(imageIndex, Images.Length);
                        bkmk.PageName = imageSource.Name;
                        if (!bkmk.IsFinishedReading)
                        {
                            bkmk.IsFinishedReading = _pageMovedCount > 0 && bkmk.ReadPosition.Value > _storageItemSettings.ReadingFinishedThresholdForImageViewer;
                        }
                    }
                    _folderLastIntractItemManager.SetLastIntractItemName(_pathForSettings, imageSource.Path);
                }
                else if (_currentImageSource is AlbamImageSource albam)
                {
                    _folderLastIntractItemManager.SetLastIntractItemName(albam.AlbamId, imageSource.Path);
                }
                if (bkmk.IsFinishedReading
                    && prev >= Images.Length - 1
                    && imageIndex == 0
                    && ViewerSettings.IsAutoMoveToNextEnabled
                    && NextImageSource != null)
                {
                    _ = OpenMangaFileAsync(NextImageSource);
                    _messenger.SendShowTextNotificationMessage("AutoMoveToNext_Notice".Translate(NextImageSource.Name));
                }
            })
            .AddTo(ref db);


        if (_currentImageSource.StorageItem != null)
        {
            string folderPath = _currentImageSource.StorageItem switch
            {
                StorageFile file => Path.GetDirectoryName(file.Path),
                StorageFolder folder => folder.Path,
                _ => throw new NotSupportedException(),
            };
            Debug.WriteLine(folderPath);
            IsFavoriteCurrentFolderOrArchive = _favoriteAlbam.IsFavorite(folderPath);
            this.ObservePropertyChanged(x => x.IsFavoriteCurrentFolderOrArchive, false)
                .Subscribe((_favoriteAlbam, folderPath, Path.GetFileName(folderPath), _messenger), static (isFavorite, s) =>
                {
                    var (_favoriteAlbam, folderPath, folderName, _messenger) = s;
                    if (isFavorite)
                    {
                        _favoriteAlbam.AddFavoriteItem(folderPath, AlbamItemType.FolderOrArchive);
                        _messenger.SendShowTextNotificationMessage("Favorite_Added".Translate(folderName));
                        _messenger.Send(new ImageSourceFavoriteChanged(folderPath, true));
                    }
                    else
                    {
                        _favoriteAlbam.DeleteFavoriteItem(folderPath, AlbamItemType.FolderOrArchive);
                        _messenger.SendShowTextNotificationMessage("Favorite_Removed".Translate(folderName));
                        _messenger.Send(new ImageSourceFavoriteChanged(folderPath, false));

                    }
                })
            .AddTo(ref db);
            IsFavoriteAlbamDisplay = false;
        }
        else
        {
            IsFavoriteAlbamDisplay = true;
        }


        db.Build().RegisterTo(ct);
#if DEBUG
        Debug.WriteLine($"Complete: {TimeProvider.System.GetElapsedTime(time)}");
        time = TimeProvider.System.GetTimestamp();
#endif
        await base.OnNavigatedToAsync(parameters, ct);
    }

    #region ImageCollection 

    async Task RefreshItems(IImageSource imageSource, IImageCollectionContext imageCollectionContext, CancellationToken ct)
    {
        ParentFolderOrArchiveName = imageCollectionContext.Name;
        ItemType = SupportedFileTypesHelper.StorageItemToStorageItemTypes(imageSource);

        await ReloadItemsAsync(imageCollectionContext, ct);

        DispatcherQueue.GetForCurrentThread().EnqueueAsync(async () => 
        {
            if (await imageCollectionContext.IsExistFolderOrArchiveFileAsync(ct))
            {
                var folders = await imageCollectionContext.GetLeafFoldersAsync(ct).ToListAsync(ct);
                if (folders.Count <= 1)
                {
                    PageFolderNames = new string[0];
                }
                else
                {
                    PageFolderNames = folders.Select(x =>
                    {
                        if (x is ArchiveDirectoryImageSource archiveDirectory)
                        {
                            return archiveDirectory.Name.TrimEnd(SeparateChars);
                        }
                        else
                        {
                            return x.Name.TrimEnd(SeparateChars);
                        }
                    }).ToArray();
                }
            }
            else
            {
                PageFolderNames = new string[0];
            }
        }, DispatcherQueuePriority.Low).FireAndForgetSafe();

        GoNextImageCommand.NotifyCanExecuteChanged();
        GoPrevImageCommand.NotifyCanExecuteChanged();
    }

    async Task ReloadItemsAsync(IImageCollectionContext imageCollectionContext, CancellationToken ct)
    {
        if (imageCollectionContext == null) { return; }

        int imageCount = await imageCollectionContext.GetImageFileCountAsync(ct);
        _nowCurrenImageIndexChanging = true;
        _nowImagesChanging = true;
        Images = new IImageSource[imageCount];
        _nowImagesChanging = false;
        _nowCurrenImageIndexChanging = false;
    }

    #endregion


    #region Image Display with Cache

    bool _nowPageFolderNameChanging = false;
    void UpdateDisplayName(IImageSource[] imageSources)
    {
        _nowPageFolderNameChanging = true;
        try
        {
            if (imageSources.Length >= 1)
            {
                var imageSource = imageSources[0];
                if (imageSource == null) { return; }
                if (imageSource is ArchiveEntryImageSource archiveEntryImageSource)
                {
                    Page1Name = Path.GetFileName(imageSource.Name);
                    if (PageFolderNames.Any())
                    {
                        PageFolderName = archiveEntryImageSource.ArchiveDirectoryName.Split(SeparateChars, options: StringSplitOptions.RemoveEmptyEntries).Last();
                    }
                }
                else
                {
                    Page1Name = imageSource.Name;
                }
            }

            if (imageSources.Length >= 2)
            {
                var imageSource = imageSources[1];
                if (imageSource == null) { return; }
                if (imageSource is ArchiveEntryImageSource)
                {
                    Page2Name = Path.GetFileName(imageSource.Name);
                }
                else
                {
                    Page2Name = imageSource.Name;
                }
            }
        }
        finally
        {
            _nowPageFolderNameChanging = false;
        }
    }

    private bool _NowDoubleImageView;
    public bool NowDoubleImageView
    {
        get { return _NowDoubleImageView; }
        set { SetProperty(ref _NowDoubleImageView, value); }
    }



    async Task ResetImageIndex(int requestIndex)
    {
        await MoveImageIndex(IndexMoveDirection.Refresh, requestIndex);
    }





    async Task MoveImageIndex(IndexMoveDirection direction, int? request = null)
    {
        if (Images == null || Images.Length == 0) { return; }
        
        _imageLoadingCts?.Cancel();
        _imageLoadingCts?.Dispose();
        _imageLoadingCts = new CancellationTokenSource();

        var ct = _imageLoadingCts.Token;
        try
        {
            using (await _imageLoadingLock.LockAsync(ct))
            {
                // IndexMoveDirection.Backward 時は CurrentIndex - 1 の位置を起点に考える
                // DoubleViewの場合は CurrentIndex - 1 -> CurrentIndex - 2 と表示可能かを試す流れ

                int currentIndex = request ?? CurrentImageIndex;
                var (requestIndex, isJumpHeadTail, requestImageCount) = GetMovedIndex(direction, currentIndex);
                int movedIndex = requestIndex;
                int displayImageCount = requestImageCount;
                try
                {
                    displayImageCount = await SetDisplayImagesAsync(direction, requestIndex, requestImageCount == 2, ct);
                    movedIndex = displayImageCount == 2 && direction == IndexMoveDirection.Backward ? requestIndex - 1 : requestIndex;
                }
                catch (OperationCanceledException)
                {
                    _nowCurrenImageIndexChanging = true;
                    CurrentImageIndex = movedIndex;
                    _nowCurrenImageIndexChanging = false;
                    throw;
                }


                // 最後尾から先頭にジャンプした場合に音を鳴らす
                if (isJumpHeadTail)
                {
                    ElementSoundPlayer.State = ElementSoundPlayerState.On;
                    ElementSoundPlayer.Volume = 1.0;
                    ElementSoundPlayer.Play(ElementSoundKind.Invoke);

                    DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
                    {
                        await Task.Delay(500);
                        using (await _imageLoadingLock.LockAsync(CancellationToken.None))
                        {
                            ElementSoundPlayer.State = ElementSoundPlayerState.Auto;
                        }

                    });
                }

                NowDoubleImageView = displayImageCount == 2;

                _nowCurrenImageIndexChanging = true;
                CurrentImageIndex = movedIndex;
                _nowCurrenImageIndexChanging = false;

            }
        }
        catch (OperationCanceledException)
        {
            
        }
        catch (NotSupportedImageFormatException ex)
        {
            _messenger.Send<RequireInstallImageCodecExtensionMessage>(new(ex.FileType));
            RequireRefresh = true;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.ToString());
        }
    }






    bool _nowCurrenImageIndexChanging;


    static int GetMoveDirection(IndexMoveDirection direction)
    {
        return direction switch
        {
            IndexMoveDirection.Refresh => 0,
            IndexMoveDirection.Forward => 1,
            IndexMoveDirection.Backward => -1,
            _ => throw new NotSupportedException()
        };
    }
    static (int position, bool IsJumpHeadTail) GetMovedImageIndex(IndexMoveDirection direction, int currentIndex, int totalImagesCount)
    {
        // 読み込むべきインデックスを先に洗い出す
        int directionMoveIndex = GetMoveDirection(direction);
        int rawRequestFirstIndex = (currentIndex) + directionMoveIndex;

        int requestIndex;
        bool isHeadTailJump = false;
        // 表示位置のWrap処理
        if (rawRequestFirstIndex < 0)
        {
            requestIndex = totalImagesCount - 1;
            isHeadTailJump = true;
        }
        else if (rawRequestFirstIndex >= totalImagesCount)
        {
            requestIndex = 0;
            isHeadTailJump = true;
        }
        else
        {
            requestIndex = rawRequestFirstIndex;
        }

        return (requestIndex, isHeadTailJump);
    }


    public async ValueTask<IImageSource> GetImageSourceWithCacheAsync(int requestIndex, CancellationToken ct)
    {
        var image = Images[requestIndex] is { } cachedImage
                ? cachedImage
                : Images[requestIndex] = await _imageCollectionContext.GetImageFileAtAsync(requestIndex, SelectedFileSortType, ct);
        if (image == null)
        {
            throw new InvalidOperationException();
        }

        return image;
    }
    async ValueTask<int> SetDisplayImagesAsync(IndexMoveDirection direction, int requestIndex, bool requestDoubleView, CancellationToken ct)
    {
        if (requestDoubleView)
        {
            // RightToLeftを基準に
            var indexies = direction == IndexMoveDirection.Backward
                ? new[] { 0, -1 }
                : new[] { 0, 1 }
                ;

            // 表示用のインデックスを生成
            // 後ろ方向にページ移動していた場合は1 -> 0のように逆順の並びにすることで
            // 見開きページかつ横長ページを表示しようとしたときに後ろ方向の一個前だけを選択して表示できるようにしている
            List<IImageSource> candidateImages = new List<IImageSource>();
            foreach (var index in indexies)
            {
                var candidateIndex = requestIndex + index;
                if (0 <= candidateIndex && candidateIndex < _Images.Length)
                {
                    candidateImages.Add(await Task.Run(async () => await _imageCollectionContext.GetImageFileAtAsync(candidateIndex, SelectedFileSortType, ct), ct));
                }
            }

            if (candidateImages.Any() is false)
            {
                throw new InvalidOperationException();
            }

            var sizeCheckResult = await CheckImagesCanDoubleViewInCurrentCanvasSizeAsync(candidateImages, ct);
            if (sizeCheckResult.CanDoubleView)
            {
                var (imageSource1, imageSource2) = direction switch
                {
                    IndexMoveDirection.Backward => (sizeCheckResult.Slot1Image, sizeCheckResult.Slot2Image),
                    _ => (sizeCheckResult.Slot2Image, sizeCheckResult.Slot1Image)
                };

                SetDisplayImages_Internal(imageSource1, imageSource2);
                //if (direction == IndexMoveDirection.Refresh)
                //{
                //    var flattenImageSource1 = imageSource1.FlattenAlbamItemInnerImageSource();
                //    var flattenImageSource2 = imageSource2.FlattenAlbamItemInnerImageSource();
                //    bool isEnabledThumbnailOut = _folderListingSettings.ThumbnailImageCacheMode != ThumbnailImageCacheMode.NeverGenerateCache;
                //    if (isEnabledThumbnailOut)
                //    {
                //        async Task<BitmapImage> LoadThumbnailAsync(IImageSource imageSource, CancellationToken ct)
                //        {
                //            var thumbImage = new BitmapImage();
                //            try
                //            {
                //                using var imageStream = await Task.Run(async () => await _thumbnailManager.EnsureGetImageStreamAsync(imageSource, ct: ct));
                //                await thumbImage.SetSourceAsync(imageStream.AsRandomAccessStream()).AsTask(ct);
                //            }
                //            catch { }
                //            return thumbImage;
                //        }

                //        var thumbnailLoadTask1 = LoadThumbnailAsync(imageSource1, ct);
                //        var thumbnailLoadTask2 = LoadThumbnailAsync(imageSource2, ct);

                //        SetDisplayImages(imageSource1, imageSource2);

                //        try
                //        {
                //            await _messenger.Send(new ImageLoadedMessage());
                //        }
                //        catch { }
                //    }
                //}

                return 2;
            }
            else
            {
                SetDisplayImages_Internal(sizeCheckResult.Slot1Image);
                return 1;
            }
        }
        else
        {
            var image = await GetImageSourceWithCacheAsync(requestIndex, ct);
            SetDisplayImages_Internal(image);
            return 1;
        }
    }


    (int requestIndex, bool isJumpHeadTail, int requestImageCount) GetMovedIndex(IndexMoveDirection direction, int currentIndex)
    {
        int requestImageCount = IsDoubleViewEnabled ? 2 : 1;
        int lastRequestImageCount = GetCurrentDisplayImageCount();

        var (requestIndex, isJumpHeadTail) = GetMovedImageIndex(direction, currentIndex, Images.Length);
        if (lastRequestImageCount == 2)
        {
            if (direction is IndexMoveDirection.Forward && !isJumpHeadTail)
            {
                (requestIndex, isJumpHeadTail) = GetMovedImageIndex(direction, requestIndex, Images.Length);
            }                
            else if (direction == IndexMoveDirection.Backward && requestIndex == 0)
            {
                if (currentIndex == 1)
                {
                    requestImageCount = 1;
                }
                else
                {
                    requestIndex = 1;
                }
            }
        }

        if (ImageViewerSettings.IsKeepSingleViewOnFirstPage)
        {
            if (requestIndex == 0)
            {
                requestImageCount = 1;
            }
            else if (requestIndex == 1 && requestImageCount == 2 && direction == IndexMoveDirection.Backward)
            {
                requestImageCount = 1;                    
            }
        }

        return (requestIndex, isJumpHeadTail, requestImageCount);
    }

    async ValueTask<(int movedIndex, int DisplayImageCount, bool IsJumpHeadTail)> LoadImagesAsync(IndexMoveDirection direction, int currentIndex, CancellationToken ct)
    {
        var (requestIndex, isJumpHeadTail, requestImageCount) = GetMovedIndex(direction, currentIndex);

        var displayImageCount = await SetDisplayImagesAsync(direction, requestIndex, requestImageCount == 2, ct);

        return (displayImageCount == 2 && direction == IndexMoveDirection.Backward ? requestIndex - 1 : requestIndex, displayImageCount, isJumpHeadTail);
    }



    public enum IndexMoveDirection
    {
        Refresh,
        Forward,
        Backward,
    }

    struct ImageDoubleViewCulcResult
    {
        public bool CanDoubleView;
        public IImageSource Slot1Image;
        public IImageSource Slot2Image;
    }

    async ValueTask<ImageDoubleViewCulcResult> CheckImagesCanDoubleViewInCurrentCanvasSizeAsync(IEnumerable<IImageSource> candidateImages, CancellationToken ct)
    {
        if (IsDoubleViewEnabled)
        {
            if (candidateImages.Count() == 1)
            {
                return new ImageDoubleViewCulcResult() { CanDoubleView = false, Slot1Image = candidateImages.First() };
            }
            else
            {
                var canvasSize = new Vector2((float)CanvasWidth, (float)CanvasHeight);

                Debug.WriteLine(canvasSize);
                var firstImage = candidateImages.ElementAt(0);
                ThumbnailSize? firstImageSize = _thumbnailManager.GetCachedThumbnailSize(firstImage);
                var secondImage = candidateImages.ElementAt(1);
                ThumbnailSize? secondImageSize = _thumbnailManager.GetCachedThumbnailSize(secondImage);

                bool canDoubleView;
                if (firstImageSize is not null and ThumbnailSize fistImageSizeReal 
                    && secondImageSize is not null and ThumbnailSize secondImageSizeReal)
                {
                    canDoubleView = CanInsideSameHeightAsLarger(in canvasSize, in fistImageSizeReal, in secondImageSizeReal);
                }
                else if (firstImageSize is not null and ThumbnailSize firstImageSizeReal2)
                {
                    canDoubleView = CanInsideSameHeightAsLarger(in canvasSize, in firstImageSizeReal2, await GetThumbnailSizeAsync(secondImage, ct));
                }
                else if (secondImageSize is not null and ThumbnailSize secondImageSizeReal2)
                {
                    canDoubleView = CanInsideSameHeightAsLarger(in canvasSize, await GetThumbnailSizeAsync(firstImage, ct), in secondImageSizeReal2);
                }
                else
                {
                    canDoubleView = CanInsideSameHeightAsLarger(in canvasSize, await GetThumbnailSizeAsync(firstImage, ct), await GetThumbnailSizeAsync(secondImage, ct));
                }

                return canDoubleView
                    ? new ImageDoubleViewCulcResult() { CanDoubleView = canDoubleView, Slot1Image = firstImage, Slot2Image = secondImage }
                    : new ImageDoubleViewCulcResult() { CanDoubleView = canDoubleView, Slot1Image = firstImage }
                    ;
            }
        }
        else
        {
            return new ImageDoubleViewCulcResult() { CanDoubleView = false, Slot1Image = candidateImages.First() };
        }
    }

    static bool CanInsideSameHeightAsLarger(in Vector2 canvasSize, in ThumbnailSize firstImageSize, in ThumbnailSize secondImageSize)
    {
        float firstImageScaledWidth = (canvasSize.Y / firstImageSize.Height) * firstImageSize.Width;
        float secondImageScaledWidth = (canvasSize.Y / secondImageSize.Height) * secondImageSize.Width;
        return canvasSize.X > (firstImageScaledWidth + secondImageScaledWidth);
    }


    async ValueTask<ThumbnailSize> GetThumbnailSizeAsync(IImageSource source, CancellationToken ct)
    {
        if (_thumbnailManager.GetCachedThumbnailSize(source) is not null and ThumbnailSize thumbSize) { return thumbSize; }
        
        if (source.IsStorageItemNotFound())
        {
            return default;
        }
        else
        {
            using (var stream = await source.GetImageStreamAsync())
            {
                var bounds = SKBitmap.DecodeBounds(stream);
                return _thumbnailManager.SetThumbnailSize(source, (uint)bounds.Width, (uint)bounds.Height);
            }
        }                
    }
 
    //async ValueTask<BitmapImage> GetBitmapImageWithCacheAsync(IImageSource source, CancellationToken ct)
    //{
    //    var image = _CachedImages.FirstOrDefault(x => x.ImageSource == source);
    //    if (image != null)
    //    {
    //        _CachedImages.Remove(image);
    //        _CachedImages.Insert(0, image);
    //    }
    //    else
    //    {
    //        image = new PrefetchImageInfo(source, (int)CanvasWidth);
    //        _CachedImages.Insert(0, image);

    //        if (_CachedImages.Count > 8)
    //        {
    //            var last = _CachedImages.Last();
    //            last.Dispose();
    //            _CachedImages.Remove(last);
    //            if (last.Image != null)
    //            {
    //                //RemoveFromDisplayImages(last.Image);
    //            }

    //            Debug.WriteLine($"remove from display cache: {last.ImageSource.Name}");
    //        }
    //    }

    //    return await image.GetBitmapImageAsync(ct);
    //}


    //readonly List<PrefetchImageInfo> _CachedImages = new ();

    //void ClearCachedImages()
    //{
    //    _CachedImages.ForEach(x => x.Cancel());
    //    _CachedImages.Clear();
    //}

    //int _currentDisplayImageIndex = 0;

    //public int CurrentDisplayImageIndex => _currentDisplayImageIndex;
    //public int PrevDisplayImageIndex => _currentDisplayImageIndex - 1 < 0 ? 2 : _currentDisplayImageIndex - 1;
    //public int NextDisplayImageIndex => _currentDisplayImageIndex + 1 > 2 ? 0 : _currentDisplayImageIndex + 1;

    //void SetCurrentDisplayImageIndex(int index)
    //{
    //    _currentDisplayImageIndex = index;
    //    OnPropertyChanged(nameof(CurrentDisplayImageIndex));
    //    OnPropertyChanged(nameof(PrevDisplayImageIndex));
    //    OnPropertyChanged(nameof(NextDisplayImageIndex));
    //}

    public IImageSource[] SourceImages => GetSourceImages();

    void NotifySourceImagesChanged()
    {
        OnPropertyChanged(nameof(SourceImages));
    }
    
    IImageSource[] _sourceImagesSingle = new IImageSource[1];
    IImageSource[] _sourceImagesDouble = new IImageSource[2];

    IImageSource[] GetSourceImages()
    {
        return NowDoubleImageView ? _sourceImagesDouble : _sourceImagesSingle;
    }

    //public async Task DisableImageDecodeWhenImageSmallerCanvasSize()
    //{
    //    var ct = _imageLoadingCts?.Token ?? default;
    //    try
    //    {
    //        using (await _imageLoadingLock.LockAsync(ct))
    //        {
    //            // 現在表示中の画像がデコード済みだった場合だけ、デコードしていない画像として読み込む

    //            var images = GetDisplayImages(PrefetchIndexType.Current);
    //            if (images.Any(x => x == null) || images.All(x => x.DecodePixelHeight == 0 && x.DecodePixelWidth == 0))
    //            {
    //                return;
    //            }

    //            var indexType = GetDisplayImageIndex(PrefetchIndexType.Current);
    //            if (NowDoubleImageView)
    //            {
    //                BitmapImage image1 = images[0];
    //                BitmapImage image2 = images[1];

    //                var imageSource1 = _sourceImagesDouble[indexType][0];
    //                var imageSource2 = _sourceImagesDouble[indexType][1];

    //                if (image1.DecodePixelHeight != 0)
    //                {
    //                    using var loader1 = new PrefetchImageInfo(imageSource1, (int)CanvasWidth);
    //                    image1 = await loader1.GetBitmapImageAsync(ct);
    //                    Debug.WriteLine($"Reload with no decode pixel : {imageSource1.Name}");
    //                }
    //                if (image2.DecodePixelHeight != 0)
    //                {
    //                    using var loader2 = new PrefetchImageInfo(imageSource2, (int)CanvasWidth);
    //                    image2 = await loader2.GetBitmapImageAsync(ct);
    //                    Debug.WriteLine($"Reload with no decode pixel : {imageSource2.Name}");
    //                }

    //                SetDisplayImages_Internal(PrefetchIndexType.Current,
    //                    imageSource1, image1,
    //                    imageSource2, image2
    //                    );
    //            }
    //            else
    //            {
    //                var imageSource1 = _sourceImagesSingle[indexType][0];
    //                using var loader1 = new PrefetchImageInfo(imageSource1, (int)CanvasWidth);
    //                SetDisplayImages_Internal(PrefetchIndexType.Current,
    //                    imageSource1, await loader1.GetBitmapImageAsync(ct)
    //                    );
    //            }
    //        }
    //    }
    //    catch (OperationCanceledException) { }
    //}

    
    //void SetDisplayImages(PrefetchIndexType type, IImageSource firstSource, BitmapImage firstImage)
    //{
    //    static void SetDecodePixelSize(BitmapImage image, float canvasWidth, float canvasHeight)
    //    {                
    //        if (image is not null && image.DecodePixelWidth == 0 && image.DecodePixelHeight == 0)
    //        {
    //            if (image.PixelWidth > canvasWidth || image.PixelHeight > canvasHeight)
    //            {
    //                // 最適化を期待してVector2で計算
    //                //var imageRatioWH = image.PixelWidth / (float)image.PixelHeight;
    //                //var canvasRatioWH = CanvasWidth.Value / (float)CanvasHeight.Value;
    //                //if (imageRatioWH > canvasRatioWH)
    //                Vector2 vector = new Vector2((float)image.PixelWidth, (float)canvasWidth) / new Vector2((float)image.PixelHeight, (float)canvasHeight);
    //                if (vector.X > vector.Y)
    //                {
    //                    image.DecodePixelWidth = (int)canvasWidth;
    //                    Debug.WriteLine($"decode to width {image.PixelWidth} -> {image.DecodePixelWidth}");
    //                }
    //                else
    //                {
    //                    image.DecodePixelHeight = (int)canvasHeight;
    //                    Debug.WriteLine($"decode to height {image.PixelHeight} -> {image.DecodePixelHeight}");
    //                }
    //            }
    //        }
    //    }

    //    if (TransformScale <= 1)
    //    {
    //        SetDecodePixelSize(firstImage, (float)CanvasWidth, (float)CanvasHeight);
    //    }

    //    SetDisplayImages_Internal(type, firstSource, firstImage);
    //}


    void SetDisplayImages(IImageSource firstSource, IImageSource secondSource)
    {
        Guard.IsNotNull(firstSource);
        if (secondSource == null)
        {
            SetDisplayImages_Internal(firstSource);

        }
        else
        {
            SetDisplayImages_Internal(firstSource, secondSource);
        }
    }

    void SetDecodePixelHeightWhenLargerThenCanvasHeight(BitmapImage image)
    {
        if (TransformScale <= 1 && image.PixelHeight > CanvasHeight)
        {
            image.DecodePixelHeight = (int)CanvasHeight;
        }
    }


    void SetDisplayImages_Internal(IImageSource firstSource)
    {
        NowDoubleImageView = false;
        var images = GetSourceImages();
        images[0] = firstSource;
        NotifySourceImagesChanged();
    }

    void SetDisplayImages_Internal(IImageSource firstSource, IImageSource secondSource)
    {
        NowDoubleImageView = true;
        var images = GetSourceImages();
        images[0] = firstSource;
        images[1] = secondSource;
        NotifySourceImagesChanged();
    }

    void ClearDisplayImages()
    {
        _sourceImagesSingle[0] = null;
        _sourceImagesDouble[0] = null;
        _sourceImagesDouble[1] = null;
    }



    private bool _IsAlreadySetDisplayImages = false;
    public bool IsAlreadySetDisplayImages
    {
        get => _IsAlreadySetDisplayImages;
        private set => SetProperty(ref _IsAlreadySetDisplayImages, value);
    }

    int GetCurrentDisplayImageCount()
    {
        if (IsAlreadySetDisplayImages is false) { return 0; }

        return GetSourceImages().Length;
    }


    #endregion

    #region Commands

    public ToggleFullScreenCommand ToggleFullScreenCommand { get; }
    public BackNavigationCommand BackNavigationCommand { get; }
    public ChangeStorageItemThumbnailImageCommand ChangeStorageItemThumbnailImageCommand { get; }
    public OpenWithExplorerCommand OpenWithExplorerCommand { get; }
    public OpenWithExternalApplicationCommand OpenWithExternalApplicationCommand { get; }
    public FavoriteToggleCommand FavoriteToggleCommand { get; }
    public RefreshNavigationCommand RefreshCommand { get; }

    private RelayCommand _GoNextImageCommand;
    public RelayCommand GoNextImageCommand =>
        _GoNextImageCommand ??= new RelayCommand(ExecuteGoNextImageCommand, CanGoNextCommand);

    void ExecuteGoNextImageCommand()
    {
        MoveImageIndex(IndexMoveDirection.Forward).FireAndForgetSafe();
        _pageMovedCount++;
    }

    private bool CanGoNextCommand()
    {
        //return CurrentImageIndex + 1 < Images?.Length;
        return true;
    }

    private RelayCommand _GoPrevImageCommand;
    public RelayCommand GoPrevImageCommand =>
        _GoPrevImageCommand ??= new RelayCommand(ExecuteGoPrevImageCommand, CanGoPrevCommand);

    void ExecuteGoPrevImageCommand()
    {
        MoveImageIndex(IndexMoveDirection.Backward).FireAndForgetSafe();
        _pageMovedCount--;
    }

    private bool CanGoPrevCommand()
    {
        //return CurrentImageIndex >= 1 && Images?.Length > 0;
        return true;
    }


    ReactiveProperty<int> _SizeChangedSubject = new ReactiveProperty<int>(-1);

    [RelayCommand]
    void SizeChanged()
    {
        if (!(Images?.Any() ?? false)) { return; }

        _SizeChangedSubject.OnNext(CurrentImageIndex);
    }


    [RelayCommand]
    async Task ChangePageFolder(string pageName)
    {
        if (string.IsNullOrEmpty(pageName)) { return; }
        if (_nowPageFolderNameChanging) { return; }

        var ct = _navigationCt;
        //using (_imageLoadingLock.LockAsync(ct))
        {
            var folders = await _imageCollectionContext.GetLeafFoldersAsync(ct).ToListAsync(ct);
            var folder = folders
                .FirstOrDefault(x => x.Name.TrimEnd(SeparateChars) == pageName);
            if (string.IsNullOrEmpty(folder?.Path) is false)
            {
                string key = folder is IArchiveEntryImageSource entry ? entry.EntryKey : folder.Path;
                var index = await _imageCollectionContext.GetImageFileIndexFromKeyAsync(key, SelectedFileSortType, ct);
                if (index >= 0)
                {
                    await ResetImageIndex(index);
                }
            }
        }
    }

    [RelayCommand]
    async Task ChangePage(double? parameter)
    {
        if (_nowCurrenImageIndexChanging) { return; }

        var index = (int)parameter.Value;
        await ResetImageIndex(index);
        //if (index == CurrentImageIndex)
        //{
        //    OnPropertyChanged(nameof(CurrentImageIndex));
        //}
    }

    [RelayCommand]
    async Task DoubleViewCorrect()
    {
        _ = ResetImageIndex(Math.Max(CurrentImageIndex - 1, 0));
    }



    [RelayCommand]
    void ChangeFileSort(object sort)
    {
        FileSortType? sortType = null;
        if (sort is int num)
        {
            sortType = (FileSortType)num;
        }
        else if (sort is FileSortType sortTypeExact)
        {
            sortType = sortTypeExact;
        }

        if (sortType.HasValue)
        {
            DisplaySortTypeInheritancePath = null;
            SelectedFileSortType = sortType.Value;
            if (_currentImageSource.StorageItem is IStorageItem)
            {
                _displaySettingsByPathRepository.SetFolderAndArchiveSettings(_pathForSettings, SelectedFileSortType);
            }
            else if (_currentImageSource is AlbamImageSource albam)
            {
                _displaySettingsByPathRepository.SetAlbamSettings(albam.AlbamId, SelectedFileSortType);
            }
        }
        else
        {
            if (_currentImageSource.StorageItem is IStorageItem)
            {
                _displaySettingsByPathRepository.ClearFolderAndArchiveSettings(_pathForSettings);
                if (_displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(_pathForSettings) is not null and var parentSort
                && parentSort.ChildItemDefaultSort != null
                )
                {
                    DisplaySortTypeInheritancePath = parentSort.Path;
                    SelectedFileSortType = parentSort.ChildItemDefaultSort.Value;
                }
                else
                {
                    DisplaySortTypeInheritancePath = null;
                    SelectedFileSortType = DefaultFileSortType;
                }
            }
            else if (_currentImageSource is AlbamImageSource albam)
            {
                _displaySettingsByPathRepository.ClearAlbamSettings(albam.AlbamId);
                DisplaySortTypeInheritancePath = null;
                SelectedFileSortType = DefaultFileSortType;
            }
        }
    }

    #endregion


    [ObservableProperty]
    private bool _ifAllFilesWannaWatchThenRegistrationFolderAtApp;

}

public class PrefetchImageInfo : IDisposable
{
    public PrefetchImageInfo(IImageSource imageSource, int canvasWidth)
    {
        ImageSource = imageSource;
        _canvasWidth = canvasWidth;
    }

    CancellationTokenSource _PrefetchCts = new CancellationTokenSource();

    public BitmapImage Image { get; set; }

    public IImageSource ImageSource { get; set; }

    public bool IsCompleted { get; set; }

    public bool IsCanceled { get; set; }

    static Core.AsyncLock _prefetchProcessLock = new ();
    readonly int _canvasWidth;

    public void Cancel()
    {
        IsCanceled = true;
        _PrefetchCts.Cancel();
    }


    public async ValueTask<BitmapImage> GetBitmapImageAsync(CancellationToken ct)
    {
        if (Image != null) { return Image; }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_PrefetchCts.Token, ct);
        var linkedCt = linkedCts.Token;
        using (await _prefetchProcessLock.LockAsync(linkedCt))
        {
            if (Image != null) { return Image; }

            if (ImageSource.IsStorageItemNotFound() is false)
            {
                var image = new BitmapImage();                    
                if (ImageSource is PdfPageImageSource)
                {
                    using var stream = new MemoryStream();
                    var size = await ImageSource.TryGetSizedImageStreamAsync(_canvasWidth, stream, linkedCt);
                    if (size != null)
                    {
                        try
                        {
                            await image.SetSourceAsync(stream.AsRandomAccessStream()).AsTask(linkedCt);
                        }
                        catch (Exception ex) when (ex.HResult == -1072868846)
                        {
                            throw new NotSupportedImageFormatException(Path.GetExtension(ImageSource.Name));
                        }
                    }
                }
                else
                {
                    using (var stream = await ImageSource.GetImageStreamAsync(linkedCt))
                    {
                        try
                        {
                            if (stream.CanSeek)
                            {
                                await image.SetSourceAsync(stream.AsRandomAccessStream()).AsTask(linkedCt);
                            }
                            else
                            {
                                using var memoryStream = new MemoryStream();
                                stream.CopyTo(memoryStream);
                                memoryStream.Seek(0, SeekOrigin.Begin);
                                await image.SetSourceAsync(memoryStream.AsRandomAccessStream()).AsTask(linkedCt);
                            }
                        }
                        catch (Exception ex) when (ex.HResult == -1072868846)
                        {
                            throw new NotSupportedImageFormatException(Path.GetExtension(ImageSource.Name));
                        }
                    }
                }

                Image = image;

                Debug.WriteLine("image load to memory : " + ImageSource.Name);
            }
            else
            {                            
                Debug.WriteLine("image load failed : " + ImageSource.Name);
            }

            IsCompleted = true;

            return Image;
        }
        
    }

    public void Dispose()
    {
        ((IDisposable)_PrefetchCts).Dispose();
    }
}
