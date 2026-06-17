using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI;
using I18NPortable;
using Microsoft.Toolkit.Uwp.UI;
using R3;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Contracts.Services;
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
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views;
using TsubameViewer.Views.Helpers;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using ZLinq;
#nullable enable
namespace TsubameViewer.ViewModels;

public sealed class StorageItemNotFoundMessage : ValueChangedMessage<string>
{
    public StorageItemNotFoundMessage(string value) : base(value)
    {
    }
}

public sealed partial class FolderListupPageViewModel 
    : NavigationAwareViewModelBase
    , IRecipient<InPageSearchRequestMessage>
    , IRecipient<StorageItemNotFoundMessage>
    , IRecipient<ThumbnailImageUpdateRequestMessage>
    , IRecipient<SendToOtherFolderMessage>
    , IRecipient<ImageSourceFavoriteChanged>
{

    public void Receive(SendToOtherFolderMessage message)
    {
        var (destSourceFolderEntry, sourceItemPath) = message.Value;
        if (CurrentFolderItem != null
            && CurrentFolderItem.Path == destSourceFolderEntry.Path
            && _imageCollectionContext != null)
        {
            // このフォルダーにアイテムが追加される？
            ReloadItemsAsync(_imageCollectionContext, _navigationCt).FireAndForgetSafe();
        }
        else
        {
            for (int i = FolderItems.Count - 1; i >= 0; i--)
            {
                var itemVM = FolderItems[i];
                if (itemVM?.Path?.Equals(sourceItemPath) ?? false)
                {
                    FolderItems.RemoveAt(i);
                    break;
                }
            }
        }
    }
    public void Receive(InPageSearchRequestMessage message)
    {
        //_filterText = message.Value;
        //OnPropertyChanged(nameof(FilterText));
    }

    [ObservableProperty]
    string _filterText = "";

    public Visibility NotEmptyToVisible(string s)
    {
        return string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;
    }


    public void Receive(StorageItemNotFoundMessage message)
    {
        var item = FolderItems.FirstOrDefault(x => x.Path != null && x.Path.Equals(message.Value, StringComparison.Ordinal));
        if (item != null)
        {
            FolderItems.Remove(item);
        }
    }




    public void Receive(ThumbnailImageUpdateRequestMessage message)
    {
        foreach (var item in FolderItems)
        {
            if (item.Path?.Equals(message.Value, StringComparison.Ordinal) ?? false)
            {
                item.ThumbnailChanged();
                item.InitializeAsync(default);
                break;
            }
        }
    }

    public void Receive(ImageSourceFavoriteChanged message)
    {
        var (imageSourcePath, isFav) = message.Value;
        foreach (var item in FolderItems)
        {
            if (item.Path?.Equals(imageSourcePath, StringComparison.Ordinal) ?? false)
            {
                item.IsFavorite = isFav;
                break;
            }
        }
    }

    [ObservableProperty]
    bool _nowProcessing;

    readonly IMessenger _messenger;
    readonly LocalBookmarkRepository _bookmarkManager;
    readonly AlbamRepository _albamRepository;
    readonly ImageCollectionManager _imageCollectionManager;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    readonly LastIntractItemRepository _folderLastIntractItemManager;
    readonly ThumbnailImageManager _thumbnailManager;        
    readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    readonly FolderListingSettings _folderListingSettings;
    readonly BackNavigationCommand _backNavigationCommand;

    public ISecondaryTileManager SecondaryTileManager { get; }
    public OpenPageCommand OpenPageCommand { get; }
    public OpenListupCommand OpenListupCommand { get; }
    public OpenFolderItemCommand OpenFolderItemCommand { get; }
    public OpenFolderItemSecondaryCommand OpenFolderItemSecondaryCommand { get; }
    public OpenImageViewerCommand OpenImageViewerCommand { get; }
    public OpenFolderListupCommand OpenFolderListupCommand { get; }
    public OpenImageListupCommand OpenImageListupCommand { get; }

    public OpenWithExplorerCommand OpenWithExplorerCommand { get; }
    public SecondaryTileAddCommand SecondaryTileAddCommand { get; }
    public SecondaryTileRemoveCommand SecondaryTileRemoveCommand { get; }
    public ChangeStorageItemThumbnailImageCommand ChangeStorageItemThumbnailImageCommand { get; }
    public OpenWithExternalApplicationCommand OpenWithExternalApplicationCommand { get; }
    public FileDeleteCommand FileDeleteCommand { get; }
    public FavoriteToggleCommand FavoriteToggleCommand { get; }
    public RangeObservableCollection<IStorageItemViewModel> FolderItems { get; private set; }

    public AdvancedCollectionView FileItemsView { get; }
    [ObservableProperty]
    bool _hasFileItem;

    [ObservableProperty]
    FileSortType _selectedFileSortType;
    [ObservableProperty]
    FileSortType? _selectedChildFileSortType;
    [ObservableProperty]
    DefaultFolderOrArchiveOpenMode _selectedChildFolderOrArchiveOpenMode;
    [ObservableProperty]
    IStorageItemViewModel? _folderLastIntractItem;

    public Visibility IsFolderItemIsRawFolderAsVisible(IStorageItemViewModel? itemVM)
    {
        return (itemVM?.Type == Core.Models.StorageItemTypes.Folder).TrueToVisible();
    }

    static readonly Core.AsyncLock _navigationLock = new();
    private IImageSource? _currentImageSource;

    [ObservableProperty]
    string? _displayCurrentPath;

    [ObservableProperty]
    StorageItemViewModel? _currentFolderItem;
    
    public SelectionContext Selection { get; } = new SelectionContext();

    [ObservableProperty]
    string? _selectedCountDisplayText;
  
    public string FoldersManagementPageName => AppShell.HomePageName;

    [ObservableProperty]
    string? _displayCurrentArchiveFolderName;

    DateTimeOffset _sourceItemLastUpdatedTime;
    CancellationToken _navigationCt;

    public FolderListupPageViewModel(
        IMessenger messenger,        
        LocalBookmarkRepository bookmarkManager,
        AlbamRepository albamRepository,
        ImageCollectionManager imageCollectionManager,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        ISecondaryTileManager secondaryTileManager,
        LastIntractItemRepository folderLastIntractItemManager,
        ThumbnailImageManager thumbnailManager,            
        DisplaySettingsByPathRepository displaySettingsByPathRepository,
        FolderListingSettings folderListingSettings,
        BackNavigationCommand backNavigationCommand,
        OpenPageCommand openPageCommand,
        OpenListupCommand openListupCommand,
        OpenFolderItemCommand openFolderItemCommand,
        OpenFolderItemSecondaryCommand openFolderItemSecondaryCommand,
        OpenImageViewerCommand openImageViewerCommand,
        OpenFolderListupCommand openFolderListupCommand,
        OpenImageListupCommand openImageListupCommand,
        OpenWithExplorerCommand openWithExplorerCommand,
        SecondaryTileAddCommand secondaryTileAddCommand,
        SecondaryTileRemoveCommand secondaryTileRemoveCommand,
        ChangeStorageItemThumbnailImageCommand changeStorageItemThumbnailImageCommand,
        OpenWithExternalApplicationCommand openWithExternalApplicationCommand,
        FileDeleteCommand fileDeleteCommand,
        FavoriteToggleCommand favoriteToggleCommand
        )
    {
        _messenger = messenger;
        _bookmarkManager = bookmarkManager;
        _albamRepository = albamRepository;
        _imageCollectionManager = imageCollectionManager;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        SecondaryTileManager = secondaryTileManager;
        _folderLastIntractItemManager = folderLastIntractItemManager;
        _thumbnailManager = thumbnailManager;            
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        _folderListingSettings = folderListingSettings;
        _backNavigationCommand = backNavigationCommand;
        OpenPageCommand = openPageCommand;
        OpenListupCommand = openListupCommand;
        OpenFolderItemCommand = openFolderItemCommand;
        OpenFolderItemSecondaryCommand = openFolderItemSecondaryCommand;
        OpenImageViewerCommand = openImageViewerCommand;
        OpenFolderListupCommand = openFolderListupCommand;
        OpenImageListupCommand = openImageListupCommand;
        OpenWithExplorerCommand = openWithExplorerCommand;
        SecondaryTileAddCommand = secondaryTileAddCommand;
        SecondaryTileRemoveCommand = secondaryTileRemoveCommand;
        ChangeStorageItemThumbnailImageCommand = changeStorageItemThumbnailImageCommand;
        OpenWithExternalApplicationCommand = openWithExternalApplicationCommand;
        FileDeleteCommand = fileDeleteCommand;
        FavoriteToggleCommand = favoriteToggleCommand;
        FolderItems = new RangeObservableCollection<IStorageItemViewModel>();
        FileItemsView = new AdvancedCollectionView(FolderItems);
        FileItemsView.Filter = s =>
        {
            if (s is not IStorageItemViewModel itemVM) { return true; }
            if (IsFavoriteFilteredDisplayEnabled
                && !itemVM.IsFavorite)
            {
                return false;
            }
            return string.IsNullOrWhiteSpace(_filterText) ? true : (itemVM?.Name?.Contains(_filterText, StringComparison.Ordinal) ?? false);
        };

        SelectedChildFolderOrArchiveOpenMode = DefaultFolderOrArchiveOpenMode.Viewer;
    }

    [ObservableProperty]
    bool _isFavoriteAlbam;

    [ObservableProperty]
    bool _isFavoriteFilteredDisplayEnabled;

    partial void OnIsFavoriteFilteredDisplayEnabledChanged(bool value)
    {   
        using (FileItemsView.DeferRefresh())
        {
            FileItemsView.RefreshFilter();
        }
    }

    [ObservableProperty]
    bool _isReadyToFavoriteFilterDisplay;

    public override void OnNavigatedFrom(INavigationParameters parameters)
    {
        d().FireAndForgetSafe();
        async Task d()
        {
            Selection.EndSelection();
            using (await _navigationLock.LockAsync(default))
            {
                foreach (var itemVM in FolderItems)
                {
                    itemVM.StopImageLoading();
                }

                if (_currentImageSource != null
                    && parameters.ContainsKey(PageNavigationConstants.GeneralPathKey) && parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string path))
                {
                    _folderLastIntractItemManager.SetLastIntractItemName(_currentImageSource.Path, Uri.UnescapeDataString(path));
                }

                _messenger.Unregister<RefreshNavigationRequestMessage>(this);
                _messenger.Unregister<BackNavigationRequestingMessage>(this);
                _messenger.Unregister<StartMultiSelectionMessage>(this);
                _messenger.Unregister<InPageSearchRequestMessage>(this);
                _messenger.Unregister<StorageItemNotFoundMessage>(this);
                _messenger.Unregister<ThumbnailImageUpdateRequestMessage>(this);
                _messenger.Unregister<SendToOtherFolderMessage>(this);
                _messenger.Unregister<ImageSourceFavoriteChanged>(this);

                base.OnNavigatedFrom(parameters);
            }
        }
    }

    void ClearContent()
    {
        using (FileItemsView.DeferRefresh())
        {
            FolderItems.Clear();
        }

        (_currentImageSource as IDisposable)?.Dispose();
        _currentImageSource = null;
        (_imageCollectionContext as IDisposable)?.Dispose();
        _imageCollectionContext = null;

        CurrentFolderItem?.Dispose();
        CurrentFolderItem = null;
        _itemsDisposable?.Dispose();
        _itemsDisposable = null;

        DisplayCurrentArchiveFolderName = null;
    }

    public IStorageItemViewModel? GetLastIntractItem()
    {
        if (_currentImageSource == null) { return null; }

        var lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(_currentImageSource.Path);
        if (lastIntaractItem == null) { return null; }

        foreach (var item in FolderItems)
        {
            if (item.Name == lastIntaractItem)
            {
                return item;
            }
        }

        return null;
    }

    async ValueTask<bool> IsRequireUpdateAsync(string newPath, string pageName, CancellationToken ct)
    {
        Guard.IsNotNullOrWhiteSpace(newPath);

        if (_currentImageSource?.Path != newPath) { return true; }

        if (string.IsNullOrEmpty(pageName) is false
            && _imageCollectionContext is ArchiveImageCollectionContext archiveImageCollectionContext
            && archiveImageCollectionContext.ArchiveDirectoryToken?.Key != pageName
            )
        {
            return true;
        }

        if (_currentImageSource != null
            && await _currentImageSource.StorageItem.GetBasicPropertiesAsync().AsTask(ct) is not null and var prop
            && _sourceItemLastUpdatedTime != prop.DateModified
            )
        {
            return true;
        }

        return false;
    }

    public override async Task OnNavigatedToAsync(INavigationParameters parameters, CancellationToken ct)
    {
        _navigationCt = ct;
        var mode = parameters.GetNavigationMode();

        NowProcessing = true;
        try
        {
            string? lastPath = _currentImageSource?.Path;
            if (parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string path))
            {
                (var newPath, var pageName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(path));

                if (await IsRequireUpdateAsync(newPath, pageName, ct))
                {
                    await ResetContent(newPath, pageName, ct);
                }
                else
                {
                    _sourceStorageItemsRepository.ThrowIfPathIsUnauthorizedAccess(newPath);

                    if (FolderItems != null)
                    {
                        foreach (var itemVM in FolderItems)
                        {
                            itemVM.RestoreThumbnailLoadingTask(ct);                            
                        }
                    }

                    FileItemsView.RefreshFilter();
                }
            }
            else if (parameters.TryGetValue(PageNavigationConstants.AlbamPathKey, out string albamPath))
            {
                (var albamIdString, _) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(albamPath));

                await ResetContentWithAlbam(albamIdString, ct);                
            }

            if (mode is NavigationMode.Back && 
                _imageCollectionContext is FolderImageCollectionContext folderContext
                )
            {                
                _sourceStorageItemsRepository.ThrowIfPathIsUnauthorizedAccess(folderContext.Folder.Path);
            }

            if (mode != NavigationMode.New)
            {
                IStorageItemViewModel? lastIntractItemVM = GetLastIntractItem();
                if (lastIntractItemVM != null)
                {
                    lastIntractItemVM.UpdateLastReadPosition();
                    lastIntractItemVM.ThumbnailChanged();                    
                    lastIntractItemVM.InitializeAsync(ct).FireAndForgetSafe();
                    FolderLastIntractItem  = lastIntractItemVM;
                }
                else
                {
                    FolderLastIntractItem  = null;
                }
            }
        }
        finally
        {
            NowProcessing = false;
        }

        var db = new DisposableBuilder();
        this.ObservePropertyChanged(x => x.SelectedFileSortType, false)            
            .DistinctUntilChanged()
            .SubscribeAwait(async (x, ct) => await SetSort(x, ct))
            .AddTo(ref db);

        this.ObservePropertyChanged(x => x.FilterText, false)
            .Debounce(TimeSpan.FromSeconds(0.25))
            .Subscribe(_ => FileItemsView.RefreshFilter())
            .AddTo(ref db);

        // Note: IsSupportFolderOrArchiveFilesIndexAccess == trueの際、
        // 意図しないFileChangedが発生し無駄更新が掛かるため変更監視を無効にしている
        if (_imageCollectionContext != null
            && _imageCollectionContext.IsSupportedFolderContentsChanged
            && IsIndexAccessListingEnabled is false
            )
        {
            // アプリ内部操作も含めて変更を検知する
            bool requireRefresh = false;
            _imageCollectionContext.CreateFolderAndArchiveFileChangedObserver()
                .ToObservable()
                .Subscribe(async _ =>
                {
                    if (Window.Current.Visible)
                    {
                        requireRefresh = false;
                        await ReloadItemsAsync(_imageCollectionContext, ct);
                    }
                    else
                    {
                        requireRefresh = true;
                    }

                    Debug.WriteLine("Folder/Archive Update required. " + _currentImageSource?.Name ?? string.Empty);
                })
                .AddTo(ref db);

            Window.Current.WindowActivationStateChanged()
                .Subscribe(async visible =>
                {
                    if (visible && requireRefresh && _imageCollectionContext is not null)
                    {
                        requireRefresh = false;
                        await ReloadItemsAsync(_imageCollectionContext, ct);
                        Debug.WriteLine("Folder/Archive Updated. " + _currentImageSource?.Name ?? string.Empty);
                    }
                })
                .AddTo(ref db);
        }

        _messenger.Register<RefreshNavigationRequestMessage>(this, (r, m) => 
        {
            Guard.IsNotNull(_imageCollectionContext);
            // TODO: 現在のフォルダ名、ないしアーカイブ名が変わっていないかチェック
            ReloadItemsAsync(_imageCollectionContext, ct).FireAndForgetSafe();
        });

        _messenger.Register<StartMultiSelectionMessage>(this, (r, m) => 
        {
            if (Selection.IsSelectionModeEnabled)
            {
                Selection.EndSelection();
            }
            else
            {
                Selection.StartSelection();
            }
            
            FileDeleteCommand.NotifyCanExecuteChanged();
            OpenWithExplorerCommand.NotifyCanExecuteChanged();
        });

        Selection.ObservePropertyChanged(x => x.IsSelectionModeEnabled)
            .Subscribe(selectionEnabled => 
            {
                if (selectionEnabled)
                {
                    _messenger.Send(new MenuDisplayMessage(Visibility.Collapsed));
                    _messenger.Register<BackNavigationRequestingMessage>(this, (r, m) =>
                    {
                        m.Value.IsHandled = true;
                        Selection.EndSelection();
                    });
                }
                else
                {
                    _messenger.Send(new MenuDisplayMessage(Visibility.Visible));
                    _messenger.Unregister<BackNavigationRequestingMessage>(this);
                }
            })
            .AddTo(ref db);

        Selection.SelectedItems.ObservePropertyChanged(x => x.Count)
            .Subscribe(count =>
            {
                SelectedCountDisplayText = "ImageSelection_SelectedCount".Translate(count);
                FileDeleteCommand.NotifyCanExecuteChanged();
                OpenWithExplorerCommand.NotifyCanExecuteChanged();
            })
            .AddTo(ref db);

        db.Build().RegisterTo(ct);

        _messenger.Register<InPageSearchRequestMessage>(this);
        _messenger.Register<StorageItemNotFoundMessage>(this);
        _messenger.Register<ThumbnailImageUpdateRequestMessage>(this);
        _messenger.Register<SendToOtherFolderMessage>(this);
        _messenger.Register<ImageSourceFavoriteChanged>(this);

        await base.OnNavigatedToAsync(parameters, ct);
    }

    bool IsIndexAccessListingEnabled => (_imageCollectionContext?.IsSupportFolderOrArchiveFilesIndexAccess ?? false) && _folderListingSettings.ShowWithIndexedFolderItemAccess;

    IImageCollectionContext? _imageCollectionContext;

    async Task ResetContent(string path, string pageName, CancellationToken ct)
    {
        using var lockObject = await _refreshLock.LockAsync(ct);

        HasFileItem = false;
        IsFavoriteAlbam = false;

        // 表示情報の解決
        ClearContent();

        try
        {
            (_currentImageSource, _imageCollectionContext) = await _imageCollectionManager.GetImageSourceAndContextAsync(path, pageName, ct);

            Guard.IsNotNull(_currentImageSource);
            Guard.IsNotNull(_imageCollectionContext);
            
            CurrentFolderItem = new StorageItemViewModel(_currentImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository);
        }
        catch
        {
            ClearContent();
            throw;
        }
        
        DisplayCurrentPath = _currentImageSource.Path;

        var settingPath = path;
        var settings = _displaySettingsByPathRepository.GetFolderAndArchiveSettings(settingPath);
        if (settings != null)
        {
            SelectedFileSortType = settings.Sort;
            SetSortAsyncUnsafe(SelectedFileSortType, path);
        }
        else
        {
            if (_currentImageSource.StorageItem is StorageFolder)
            {
                SelectedFileSortType = FileSortType.UpdateTimeDecending;
                SetSortAsyncUnsafe(SelectedFileSortType, path);
            }
            else if (_currentImageSource.StorageItem is StorageFile file && file.IsSupportedMangaFile())
            {
                SelectedFileSortType = FileSortType.UpdateTimeDecending;
                SetSortAsyncUnsafe(SelectedFileSortType, path);
            }
        }

        SelectedChildFileSortType  = _displaySettingsByPathRepository.GetFileParentSettings(path);
        SelectedChildFolderOrArchiveOpenMode = _displaySettingsByPathRepository.GetFolderAndArchiveSettings(path)?.DefaultOpenMode ?? DefaultFolderOrArchiveOpenMode.Viewer;

        try
        {
            await ReloadItemsAsync(_imageCollectionContext, ct);
        }
        catch (OperationCanceledException)
        {
            ClearContent();
            (_backNavigationCommand as ICommand).Execute(null);
        }
    }

    async Task ResetContentWithAlbam(string albamIdString, CancellationToken ct)
    {
        using var lockObject = await _refreshLock.LockAsync(ct);

        if (Guid.TryParse(albamIdString, out Guid albamId) is false)
        {
            throw new InvalidOperationException();
        }

        var albam = _albamRepository.GetAlbam(FavoriteAlbam.FavoriteAlbamId);
        HasFileItem = false;
        DisplayCurrentPath = "Albam".Translate();
        IsFavoriteAlbam = true;

        ClearContent();

        string path = albam._id.ToString();
        //SelectedChildFileSortType  = _displaySettingsByPathRepository.GetFileParentSettings(path);
        SelectedChildFileSortType  = FileSortType.None;
        SelectedFileSortType = FileSortType.UpdateTimeDecending;
        SetSortAsyncUnsafe(SelectedFileSortType, path);        

        AlbamImageCollectionContext imageCollectionContext = new (albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _messenger);
        AlbamImageSource albamImageSource = new (albam, imageCollectionContext);
        CurrentFolderItem = new StorageItemViewModel(albamImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository);
        DisplayCurrentArchiveFolderName = imageCollectionContext.Name;

        _currentImageSource = albamImageSource;
        _imageCollectionContext = imageCollectionContext;        

        try
        {
            await ReloadItemsAsync(_imageCollectionContext, ct);
        }
        catch (OperationCanceledException)
        {
            ClearContent();
            (_backNavigationCommand as ICommand).Execute(null);
        }
    }

    #region Refresh Item

    static readonly Core.AsyncLock _refreshLock = new ();
    IDisposable? _itemsDisposable;
    async Task ReloadItemsAsync(IImageCollectionContext imageCollectionContext, CancellationToken ct)
    {
        _itemsDisposable?.Dispose();
        _itemsDisposable = null;

        Guard.IsNotNull(imageCollectionContext);
        if (!IsIndexAccessListingEnabled)
        {
            await _messenger.WorkWithBusyWallAsync(async (ct) =>
            {
                var existItemsHashSet = FolderItems.Select(x => x.Path).ToHashSet();
                using (FileItemsView.DeferRefresh())
                {
                    IsFavoriteFilteredDisplayEnabled = false;
                    Debug.WriteLine($"items count : {FolderItems.Count}");

                    // 新規アイテム
                    List<StorageItemViewModel> items = [];
                    await foreach (var item in imageCollectionContext.GetFolderOrArchiveFilesAsync(ct).WithCancellation(ct))
                    {
                        if (item == null) { continue; }

                        if (existItemsHashSet.Contains(item.Path) is false)
                        {
                            items.Add(new StorageItemViewModel(item, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository, Selection));
                        }
                        else
                        {
                            existItemsHashSet.Remove(item.Path);
                        }
                    }

                    FolderItems.AddRange(items);

                    Debug.WriteLine($"after added : {FolderItems.Count}");
                    for (int i = FolderItems.Count - 1; i >= 0; i--)
                    {
                        var itemVM = FolderItems[i];
                        if (existItemsHashSet.Contains(itemVM.Path))
                        {
                            FolderItems.RemoveAt(i);
                        }
                        else
                        {
                            itemVM.RestoreThumbnailLoadingTask(ct);
                        }
                    }

                    FileItemsView.RefreshFilter();
                    IsReadyToFavoriteFilterDisplay = true;
                    Debug.WriteLine($"after deleted : {FolderItems.Count}");
                }
            }, ct);
        }
        else
        {
            IsReadyToFavoriteFilterDisplay = false;
            var sortType = SelectedFileSortType;
            if (imageCollectionContext is FolderImageCollectionContext col)
            {
                R3.CompositeDisposable disposable = new R3.CompositeDisposable();
                // StorageFolderはアイテム取得に時間がかかる
                Func<FolderStructureFileEntry, IStorageItem?, LazyCacheFolderOrArchiveFileViewModel> cacheImageViewModelFactory = (entry, file) =>
                {
                    return new LazyCacheFolderOrArchiveFileViewModel(col, entry, sortType, new StorageItemImageSource(file), _messenger,
                                _sourceStorageItemsRepository,
                                _bookmarkManager,
                                _thumbnailManager,
                                _albamRepository,
                                Selection);
                };

                var d1 = imageCollectionContext.CreateFolderAndArchiveFileChangedObserver()
                    .ToObservable()
                    .SubscribeAwait((col, FileItemsView, cacheImageViewModelFactory), async (_, s, ct) =>
                    {
                        var (col, items, itemFacotry) = s;
                        //await ReloadItemsAsync(col, ct);
                        var ignore = col.Context.HandleDiffNotImages(
                            (ObservableCollection<IStorageItemViewModel>)items.Source,
                            items.DeferRefresh,
                            itemFacotry,
                            (IStorageItemViewModel itemVM) => itemVM.Path,
                            ct);
                    });

                disposable.Add(d1);
                _itemsDisposable = disposable;

                await _messenger.WorkWithBusyWallAsync(async (ct) =>
                {
                    using (FileItemsView.DeferRefresh())
                    {
                        IsFavoriteFilteredDisplayEnabled = false;
                        FolderItems.Clear();

                        FolderItems.AddRange(col.Context.GetCacheNotImages().Select(entry =>
                        {
                            return new LazyCacheFolderOrArchiveFileViewModel(col, entry, sortType, null, _messenger,
                                _sourceStorageItemsRepository,
                                _bookmarkManager,
                                _thumbnailManager,
                                _albamRepository,
                                Selection);
                        }));

                        if (await col.Context.CheckIsNotSameNotImagesCacheCountAndExactCountAsync(ct))
                        {
                            await col.Context.HandleDiffNotImages(
                                    (ObservableCollection<IStorageItemViewModel>)FileItemsView.Source,
                                    FileItemsView.DeferRefresh,
                                    cacheImageViewModelFactory,
                                    (IStorageItemViewModel itemVM) => itemVM.Path,
                                    ct);
                        }
                        FileItemsView.RefreshFilter();

                        IsReadyToFavoriteFilterDisplay = true;
                    }
                }, ct);
            }
            else // pdfやzipなどは構造が固定でIndexアクセスしても安定する
            {
                Guard.IsNotNull(_imageCollectionContext);
                await _messenger.WorkWithBusyWallAsync(async (ct) =>
                {
                    using (FileItemsView.DeferRefresh())
                    {
                        IsFavoriteFilteredDisplayEnabled = false;
                        FolderItems.Clear();
                        var count = await imageCollectionContext.GetImageFileCountAsync(ct);
                        FolderItems.AddRange(Enumerable.Range(0, count)
                            .Select(index =>
                            {
                                return new LazyFolderOrArchiveFileViewModel(
                                    _imageCollectionContext,
                                    index,
                                    SelectedFileSortType,
                                    _messenger,
                                    _sourceStorageItemsRepository,
                                    _bookmarkManager,
                                    _thumbnailManager,
                                    _albamRepository,
                                    Selection);
                            }));
                        FileItemsView.RefreshFilter();

                        DispatcherQueue.GetForCurrentThread().EnqueueAsync(async () =>
                        {
                            var items = FolderItems.AsValueEnumerable().Cast<LazyFolderOrArchiveFileViewModel>().ToArrayPool();
                            foreach (var itemVM in items.ArraySegment)
                            {
                                await itemVM.EnsureStorageItemAsync(ct);
                            }
                            IsReadyToFavoriteFilterDisplay = true;
                        }).FireAndForgetSafe();
                    }
                }, ct);
            }
        }

        ct.ThrowIfCancellationRequested();

        DispatcherQueue.GetForCurrentThread().EnqueueAsync(async () =>
        {
            if (_currentImageSource?.StorageItem != null)
            {
                var prop = await _currentImageSource.StorageItem.GetBasicPropertiesAsync();
                _sourceItemLastUpdatedTime = prop.DateModified;
            }
            bool exist = await imageCollectionContext.IsExistImageFileAsync(ct);
            HasFileItem = exist;
        }).FireAndForgetSafe();
    }

#endregion



#region FileSortType


    public static IEnumerable<SortDescription> ToSortDescription(FileSortType fileSortType)
    {
        return fileSortType switch
        {
            FileSortType.TitleAscending => new[] { new SortDescription(nameof(StorageItemViewModel.Name), SortDirection.Ascending) },
            FileSortType.TitleDecending => new[] { new SortDescription(nameof(StorageItemViewModel.Name), SortDirection.Descending) },
            FileSortType.UpdateTimeAscending => new[] { new SortDescription(nameof(StorageItemViewModel.DateCreated), SortDirection.Ascending) },
            FileSortType.UpdateTimeDecending => new[] { new SortDescription(nameof(StorageItemViewModel.DateCreated), SortDirection.Descending) },
            _ => throw new NotSupportedException(),
        };
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
            SelectedFileSortType = sortType.Value;
        }
    }

    async Task SetSort(FileSortType fileSort, CancellationToken ct)
    {
        if (_currentImageSource == null) { throw new NullReferenceException(nameof(_currentImageSource.Path)); }
        Guard.IsNotNull(_imageCollectionContext);

        using (await _refreshLock.LockAsync(ct))
        {
            SetSortAsyncUnsafe(fileSort, _currentImageSource.Path);
        }
    }

    void SetSortAsyncUnsafe(FileSortType fileSort, string path)
    {
        var sortDescriptions = ToSortDescription(fileSort);
        using (FileItemsView.DeferRefresh())
        {
            FileItemsView.SortDescriptions.Clear();
            foreach (var sort in sortDescriptions)
            {
                FileItemsView.SortDescriptions.Add(sort);
            }
        }

        _displaySettingsByPathRepository.SetFolderAndArchiveSettings(
            path,
            fileSort
            );
    }

    [RelayCommand]
    void ChangeChildFolderOrArchiveOpenMode(object mode)
    {
        if (mode is DefaultFolderOrArchiveOpenMode openMode)
        {
            Guard.IsNotNull(_currentImageSource);

            SelectedChildFolderOrArchiveOpenMode = openMode;
            _displaySettingsByPathRepository.SetChildFolderOrArchiveOpenModeParentSettings(_currentImageSource.Path, openMode);
        }
    }

    [RelayCommand]
    void ChangeChildFileSort(object sort)
    {
        if (_currentImageSource == null) { throw new NullReferenceException(nameof(_currentImageSource.Path)); }

        FileSortType? sortType = null;
        if (sort is int num)
        {
            sortType = (FileSortType)num;
        }
        else if (sort is FileSortType sortTypeExact)
        {
            sortType = sortTypeExact;
        }

        SelectedChildFileSortType = sortType;
        _displaySettingsByPathRepository.SetFileParentSettings(_currentImageSource.Path, sortType);
    }

    #endregion


    public StorageItemViewModel ToStorageItemVM(IStorageItem item)
    {
        return new StorageItemViewModel(new StorageItemImageSource(item), _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository, Selection);
    }
}

public abstract class FolderItemsGroupBase
{
    public ObservableCollection<StorageItemViewModel>? Items { get; set; }
}
public sealed class FolderFolderItemsGroup : FolderItemsGroupBase
{

}

public sealed class FileFolderItemsGroup : FolderItemsGroupBase
{

}

class FolderListupPageParameter
{
    public string? Token { get; set; }
    public string? Path { get; set; }
}

