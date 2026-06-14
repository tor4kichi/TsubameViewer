using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using I18NPortable;
using LiteDB;
using Microsoft.Toolkit.Uwp.UI;
using R3;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Infrastructure;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views;
using TsubameViewer.Views.Helpers;
using Windows.Storage;
using Windows.System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ZLinq;
#nullable enable
namespace TsubameViewer.ViewModels;

public sealed class SelectionContext : ObservableObject
{
    private bool _isSelectionModeEnabled;
    public bool IsSelectionModeEnabled
    {
        get => _isSelectionModeEnabled;
        private set => SetProperty(ref _isSelectionModeEnabled, value);
    }

    public ObservableCollection<IStorageItemViewModel> SelectedItems { get; } = new ();

    public void ForceNotifySelectedItems()
    {
        OnPropertyChanged(nameof(SelectedItems));
    }

    public void StartSelection()
    {
        IsSelectionModeEnabled = true;
    }

    public void EndSelection()
    {
        IsSelectionModeEnabled = false;
        SelectedItems.Clear();
    }
}

public sealed partial class ImageListupPageViewModel 
    : NavigationAwareViewModelBase
    , IRecipient<InPageSearchRequestMessage>
    , IRecipient<StorageItemNotFoundMessage>
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
            for (int i = ImageFileItems.Count - 1; i >= 0; i--)
            {
                var itemVM = ImageFileItems[i];
                if (itemVM?.Path?.Equals(sourceItemPath) ?? false)
                {
                    ImageFileItems.RemoveAt(i);
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

    public Visibility NotEmptyToVisible(string s)
    {
        return string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;
    }

    [ObservableProperty]
    string _filterText = "";


    public void Receive(StorageItemNotFoundMessage message)
    {
        var item = ImageFileItems.FirstOrDefault(x => x.Path.Equals(message.Value, StringComparison.Ordinal));
        if (item != null)
        {
            ImageFileItems.Remove(item);

        }
    }

    public void Receive(ImageSourceFavoriteChanged message)
    {
        var (imageSourcePath, isFav) = message.Value;
        foreach (var item in ImageFileItems)
        {
            if (item.Path?.Equals(imageSourcePath, StringComparison.Ordinal) ?? false)
            {
                item.IsFavorite = isFav;
                break;
            }
        }
    }

    readonly IMessenger _messenger;
    readonly LocalBookmarkRepository _bookmarkManager;
    readonly ImageCollectionManager _imageCollectionManager;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    readonly AlbamRepository _albamRepository;
    readonly ThumbnailImageManager _thumbnailManager;
    readonly LastIntractItemRepository _folderLastIntractItemManager;
    readonly FolderListingSettings _folderListingSettings;
    readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    private bool _nowProcessing;
    public bool NowProcessing
    {
        get { return _nowProcessing; }
        set { SetProperty(ref _nowProcessing, value); }
    }

    public ISecondaryTileManager SecondaryTileManager { get; }
    public OpenPageCommand OpenPageCommand { get; }
    public OpenFolderItemCommand OpenFolderItemCommand { get; }
    public OpenImageViewerCommand OpenImageViewerCommand { get; }
    public OpenFolderListupCommand OpenFolderListupCommand { get; }
    public FileDeleteCommand FileDeleteCommand { get; }
    public OpenWithExplorerCommand OpenWithExplorerCommand { get; }
    public SecondaryTileAddCommand SecondaryTileAddCommand { get; }
    public SecondaryTileRemoveCommand SecondaryTileRemoveCommand { get; }
    public ChangeStorageItemThumbnailImageCommand ChangeStorageItemThumbnailImageCommand { get; }
    public OpenWithExternalApplicationCommand OpenWithExternalApplicationCommand { get; }
    public FavoriteToggleCommand FavoriteToggleCommand { get; }
    public RangeObservableCollection<IStorageItemViewModel> ImageFileItems { get; }

    public AdvancedCollectionView FileItemsView { get; }

    [ObservableProperty]
    bool _hasFileItem;

    [ObservableProperty]
    bool _hasFolderOrBookItem;

    public SelectionContext Selection { get; } = new SelectionContext();

    [ObservableProperty]
    FileSortType _selectedFileSortType;
   
    readonly FileSortType _defaultFileSortType = FileSortType.TitleAscending;

    [ObservableProperty]
    string? _displaySortTypeInheritancePath;

    [ObservableProperty]
    int _imageLastIntractItem;

    static readonly Core.AsyncLock _navigationLock = new ();

    private IImageSource? _currentImageSource;
    IImageCollectionContext? _imageCollectionContext;
    static readonly Core.AsyncLock _refreshLock = new();

    [ObservableProperty]
    string? _displayCurrentPath;

    [ObservableProperty]
    StorageItemViewModel? _currentFolderItem;

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
    string? _displayCurrentArchiveFolderName;

    [ObservableProperty]
    FileDisplayMode _fileDisplayMode;

    partial void OnFileDisplayModeChanged(FileDisplayMode value)
    {
        _folderListingSettings.FileDisplayMode = value;
    }

    public FileDisplayMode[] FileDisplayModeItems { get; } = new FileDisplayMode[]
    {
        Core.Models.FolderItemListing.FileDisplayMode.Large,
        Core.Models.FolderItemListing.FileDisplayMode.Midium,
        Core.Models.FolderItemListing.FileDisplayMode.Small,
        Core.Models.FolderItemListing.FileDisplayMode.Line,
    };


    public string FoldersManagementPageName => AppShell.HomePageName;
    private CancellationToken _navigationCt;

    public ImageListupPageViewModel(
        IMessenger messenger,
        LocalBookmarkRepository bookmarkManager,
        ImageCollectionManager imageCollectionManager,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        AlbamRepository albamRepository,
        ThumbnailImageManager thumbnailManager,
        ISecondaryTileManager secondaryTileManager,
        LastIntractItemRepository folderLastIntractItemManager,
        FolderListingSettings folderListingSettings,
        DisplaySettingsByPathRepository displaySettingsByPathRepository,
        OpenPageCommand openPageCommand,
        OpenFolderItemCommand openFolderItemCommand,
        OpenImageViewerCommand openImageViewerCommand,
        OpenFolderListupCommand openFolderListupCommand,
        FileDeleteCommand fileDeleteCommand,
        OpenWithExplorerCommand openWithExplorerCommand,
        SecondaryTileAddCommand secondaryTileAddCommand,
        SecondaryTileRemoveCommand secondaryTileRemoveCommand,
        ChangeStorageItemThumbnailImageCommand changeStorageItemThumbnailImageCommand,
        OpenWithExternalApplicationCommand openWithExternalApplicationCommand,
        FavoriteToggleCommand favoriteToggleCommand
        )
    {
        _messenger = messenger;
        _bookmarkManager = bookmarkManager;
        _imageCollectionManager = imageCollectionManager;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _albamRepository = albamRepository;
        _thumbnailManager = thumbnailManager;
        SecondaryTileManager = secondaryTileManager;
        _folderLastIntractItemManager = folderLastIntractItemManager;
        _folderListingSettings = folderListingSettings;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        OpenPageCommand = openPageCommand;
        OpenFolderItemCommand = openFolderItemCommand;
        OpenImageViewerCommand = openImageViewerCommand;
        OpenFolderListupCommand = openFolderListupCommand;
        FileDeleteCommand = fileDeleteCommand;
        OpenWithExplorerCommand = openWithExplorerCommand;
        SecondaryTileAddCommand = secondaryTileAddCommand;
        SecondaryTileRemoveCommand = secondaryTileRemoveCommand;
        ChangeStorageItemThumbnailImageCommand = changeStorageItemThumbnailImageCommand;
        OpenWithExternalApplicationCommand = openWithExternalApplicationCommand;
        FavoriteToggleCommand = favoriteToggleCommand;
        ImageFileItems = new RangeObservableCollection<IStorageItemViewModel>();
        FileItemsView = new KeyIndexMappedAdvancedCollectionView<IStorageItemViewModel>(ImageFileItems, itemVM => itemVM.Path);
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
        SelectedFileSortType = FileSortType.UpdateTimeDecending;
        FileDisplayMode = _folderListingSettings.FileDisplayMode;        
    }

    public IStorageItemViewModel? GetLastIntractItem()
    {
        string? lastIntaractItem = null;
        if (_currentImageSource?.StorageItem is IStorageItem storageItem)
        {
            lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(storageItem.Path);
        }
        else if (_currentImageSource is AlbamImageSource albamImageSource)
        {
            lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(albamImageSource.AlbamId);
        }        

        if (lastIntaractItem == null) { return null; }

        //Debug.WriteLine($"last intaraction item restore, folderPath: {storageItem.Path}, itemPath: {lastIntaractItem}");

        foreach (var item in ImageFileItems)
        {
            if (item.Name == lastIntaractItem)
            {
                return item;
            }
        }

        return null;
    }

    public void SetLastIntractItem(IStorageItemViewModel itemVM)
    {
        Debug.WriteLine($"last intaraction item saved, folderPath: {DisplayCurrentPath}, itemPath: {itemVM.Path}");

        _folderLastIntractItemManager.SetLastIntractItemName(DisplayCurrentPath, itemVM.Path);
    }

    public void ClearLastIntractItem()
    {
        _folderLastIntractItemManager.Remove(DisplayCurrentPath);
    }

    public override void OnNavigatedFrom(INavigationParameters parameters)
    {
        _messenger.Unregister<AlbamItemAddedMessage>(this);
        _messenger.Unregister<AlbamItemRemovedMessage>(this);
        _messenger.Unregister<InPageSearchRequestMessage>(this);
        _messenger.Unregister<StorageItemNotFoundMessage>(this);
        _messenger.Unregister<SendToOtherFolderMessage>(this);
        _messenger.Unregister<BackNavigationRequestingMessage>(this);
        _messenger.Unregister<ImageSourceFavoriteChanged>(this);

        foreach (var itemVM in ImageFileItems.Reverse())
        {
            itemVM.StopImageLoading();
        }

        base.OnNavigatedFrom(parameters);
    }
    

    async ValueTask<bool> IsRequireUpdateAsync(string newPath, string pageName, CancellationToken ct)
    {
        if (newPath != _currentImageSource?.Path)
        {
            return true;
        }

        if(string.IsNullOrEmpty(pageName) is false
            && _imageCollectionContext is ArchiveImageCollectionContext archiveImageCollectionContext
            && archiveImageCollectionContext.ArchiveDirectoryToken?.Key != pageName
            )
        {
            return true;
        }

        if (IsIndexAccessListingEnabled 
            && _imageCollectionContext != null
            && ImageFileItems.Count != await _imageCollectionContext.GetImageFileCountAsync(ct))
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
            if (parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string path))
            {                    
                (var newPath, var pageName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(path));
                if (await IsRequireUpdateAsync(newPath, pageName, ct))
                {
                    await ResetContentWithStorageItem(newPath, pageName, ct);
                }
                else
                {
                    _sourceStorageItemsRepository.ThrowIfPathIsUnauthorizedAccess(newPath);
                }
            }
            else if (parameters.TryGetValue(PageNavigationConstants.AlbamPathKey, out string albamPath))
            {
                (var albamIdString, _) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(albamPath));

                await ResetContentWithAlbam(albamIdString, ct);
            }
            if (mode != NavigationMode.New)
            {
                string? lastIntaractItem = null;
                if (_currentImageSource?.StorageItem is IStorageItem storageItem)
                {
                    lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(storageItem.Path);                        
                }    
                else if (_currentImageSource is AlbamImageSource albamImageSource)
                {
                    lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(albamImageSource.AlbamId);
                }

                if (lastIntaractItem != null)
                {
                    var item = ImageFileItems.FirstOrDefault(x => x.Name == lastIntaractItem);
                    ImageLastIntractItem = ImageFileItems.IndexOf(item);
                }
                else
                {
                    ImageLastIntractItem = 0;
                }
            }
        }
        finally
        {
            NowProcessing = false;
        }

       
        var db = new DisposableBuilder();
        try
        {
            _messenger.Register<AlbamItemAddedMessage>(this, (r, m) =>
            {
                var (albamId, path, itemType) = m.Value;
                if (albamId == FavoriteAlbam.FavoriteAlbamId)
                {
                    var itemVM = ImageFileItems.FirstOrDefault(x => x.Path.Equals(path, StringComparison.Ordinal));
                    itemVM?.IsFavorite = true;
                }
            });

            _messenger.Register<AlbamItemRemovedMessage>(this, (r, m) =>
            {
                var (albamId, path, itemType) = m.Value;
                if (albamId == FavoriteAlbam.FavoriteAlbamId)
                {
                    var itemVM = ImageFileItems.FirstOrDefault(x => x.Path.Equals(path, StringComparison.Ordinal));
                    itemVM?.IsFavorite = false;
                }
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

            _messenger.Register<InPageSearchRequestMessage>(this);
            _messenger.Register<StorageItemNotFoundMessage>(this);
            _messenger.Register<SendToOtherFolderMessage>(this);
            _messenger.Register<ImageSourceFavoriteChanged>(this);

            this.ObservePropertyChanged(x => x.SelectedFileSortType)
                .SubscribeAwait(async (sort, ct) =>
                {
                    await SetSort(sort, ct);
                })
                .AddTo(ref db);

            this.ObservePropertyChanged(x => x.FilterText)
                .Debounce(TimeSpan.FromSeconds(1))
                .Subscribe(_ => FileItemsView.RefreshFilter())
                .AddTo(ref db);

            db.Build().RegisterTo(ct);
        }
        catch
        {
            db.Dispose();
            _messenger.Unregister<AlbamItemAddedMessage>(this); 
            _messenger.Unregister<AlbamItemRemovedMessage>(this);
            _messenger.Unregister<InPageSearchRequestMessage>(this);
            _messenger.Unregister<StorageItemNotFoundMessage>(this);
            _messenger.Unregister<SendToOtherFolderMessage>(this);
            _messenger.Unregister<ImageSourceFavoriteChanged>(this);
            throw;
        }

        await base.OnNavigatedToAsync(parameters, ct);
    }

    #region Refresh Item

    void ClearContent()
    {
        using (FileItemsView.DeferRefresh())
        {
            ImageFileItems.Clear();
        }

        IsFavoriteAlbam = false;
        (_imageCollectionContext as IDisposable)?.Dispose();
        _imageCollectionContext = null;
        (CurrentFolderItem as IDisposable)?.Dispose();
        CurrentFolderItem = null;
        DisplayCurrentArchiveFolderName = null;
        _itemsDisposable?.Dispose();
        _itemsDisposable = null;
    }


    async Task ResetContentWithStorageItem(string path, string pageName, CancellationToken ct)
    {
        using var lockReleaser = await _navigationLock.LockAsync(ct);

        HasFileItem = false;
        DisplayCurrentPath = ""; 

        // 表示情報の解決
        ClearContent();
        
        try
        {
            (_currentImageSource, _imageCollectionContext) = await _imageCollectionManager.GetImageSourceAndContextAsync(path, pageName, ct);

            Guard.IsNotNull(_currentImageSource);
            Guard.IsNotNull(_imageCollectionContext);
        }
        catch
        {
            ClearContent();
            throw;
        }

        CurrentFolderItem = new StorageItemViewModel(_currentImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository);        
        DisplayCurrentPath = _currentImageSource.Path;
        if (_imageCollectionContext is ArchiveImageCollectionContext archiveImageCollectionContext)
        {
            if (archiveImageCollectionContext.ArchiveDirectoryToken.IsRoot)
            {
                DisplayCurrentPath = archiveImageCollectionContext.File.Path;
            }
            else
            {
                DisplayCurrentPath = Path.Combine(archiveImageCollectionContext.File.Path, archiveImageCollectionContext.ArchiveDirectoryToken.DirectoryPath);
            }
        }        

        var settings = _displaySettingsByPathRepository.GetFolderAndArchiveSettings(_currentImageSource.Path);
        if (settings != null)
        {
            DisplaySortTypeInheritancePath = null;
            SelectedFileSortType = settings.Sort;
        }
        else if (_displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(_currentImageSource.Path) is not null and var parentSort
            && parentSort.ChildItemDefaultSort != null
            )
        {
            DisplaySortTypeInheritancePath = parentSort.Path;
            SelectedFileSortType = parentSort.ChildItemDefaultSort.Value;
        }
        else
        {
            DisplaySortTypeInheritancePath = null;
            SelectedFileSortType = _defaultFileSortType;
        }

        FilterText = "";

        await SetSort(SelectedFileSortType, ct);        
        await ReloadItemsAsync(_imageCollectionContext, ct);

        HasFileItem = ImageFileItems.Any();

        OnPropertyChanged(nameof(ImageFileItems));
    }

    async Task ResetContentWithAlbam(string albamIdString, CancellationToken ct)
    {
        using var lockReleaser = await _navigationLock.LockAsync(ct);

        HasFileItem = false;
        DisplayCurrentPath = "";
        ClearContent();
        if (Guid.TryParse(albamIdString, out Guid albamId) is false)
        {
            throw new InvalidOperationException();
        }
        
        // SourceStorageItemsRepositoryへの登録が遅延する可能性がある
        var albam = _albamRepository.GetAlbam(albamId);
        if (albam == null)
        {
            throw new InvalidOperationException();
        }

        AlbamImageCollectionContext imageCollectionContext = new (albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _messenger);
        AlbamImageSource albamImageSource = new (albam, imageCollectionContext);
        CurrentFolderItem = new StorageItemViewModel(albamImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository);

        _imageCollectionContext = imageCollectionContext;
        _currentImageSource = albamImageSource;
        DisplayCurrentPath = "Albam".Translate();
        IsFavoriteAlbam = albamId == FavoriteAlbam.FavoriteAlbamId;
        DisplayCurrentArchiveFolderName = albam.Name;
        HasFileItem = ImageFileItems.Any();

        var settings = _displaySettingsByPathRepository.GetAlbamDisplaySettings(albamId);
        if (settings != null)
        {
            DisplaySortTypeInheritancePath = null;
            SelectedFileSortType = settings.Sort;
        }
        else
        {
            DisplaySortTypeInheritancePath = null;
            SelectedFileSortType = _defaultFileSortType;
        }

        await SetSort(SelectedFileSortType, ct);
        await ReloadItemsAsync(_imageCollectionContext, ct);

        OnPropertyChanged(nameof(ImageFileItems));
    }
    bool IsIndexAccessListingEnabled => (_imageCollectionContext?.IsSupportFolderOrArchiveFilesIndexAccess ?? false) && _folderListingSettings.ShowWithIndexedFolderItemAccess;
    
    IDisposable? _itemsDisposable;
    async Task ReloadItemsAsync(IImageCollectionContext imageCollectionContext, CancellationToken ct)
    {
        _itemsDisposable?.Dispose();
        _itemsDisposable = null;
        if (!IsIndexAccessListingEnabled)
        {
            var existItemsHashSet = ImageFileItems.Select(x => x.Path).ToHashSet();
            using (FileItemsView.DeferRefresh())
            {
                IsFavoriteFilteredDisplayEnabled = false;
                ImageFileItems.Clear();
                // 削除アイテム
                Debug.WriteLine($"items count : {ImageFileItems.Count}");

                // 新規アイテム
                List<IStorageItemViewModel> items = [];
                await foreach (var item in imageCollectionContext.GetImageFilesAsync(ct).WithCancellation(ct))
                {
                    if (existItemsHashSet.Contains(item.Path) is false)
                    {
                        items.Add(new StorageItemViewModel(item, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository, Selection));
                    }
                    else
                    {
                        existItemsHashSet.Remove(item.Path);
                    }
                }

                ImageFileItems.AddRange(items);

                Debug.WriteLine($"after added : {ImageFileItems.Count}");
                for (int i = ImageFileItems.Count - 1; i >= 0; i--)
                {
                    var itemVM = ImageFileItems[i];
                    if (existItemsHashSet.Contains(itemVM.Path))
                    {
                        ImageFileItems.RemoveAt(i);
                    }
                    else
                    {
                        itemVM.RestoreThumbnailLoadingTask(ct);
                    }
                }

                Debug.WriteLine($"after deleted : {ImageFileItems.Count}");
            }

            if (_imageCollectionContext?.IsSupportedFolderContentsChanged ?? false)
            {
                R3.CompositeDisposable disposable = new R3.CompositeDisposable();
                // アプリ内部操作も含めて変更を検知する
                var d2 = _imageCollectionContext.CreateImageFileChangedObserver()
                    .ToObservable()
                    .SubscribeAwait(async (_, ct) =>
                    {
                        await ReloadItemsAsync(_imageCollectionContext, ct);
                        Debug.WriteLine("Images Update required. " + _currentImageSource);
                    });
                disposable.Add(d2);
                _itemsDisposable = disposable;
            }
        }
        else
        {
            var sortType = SelectedFileSortType;
            if (imageCollectionContext is FolderImageCollectionContext col)
            {
                R3.CompositeDisposable disposable = new R3.CompositeDisposable();
                // StorageFolderはアイテム取得に時間がかかる
                Func<FolderStructureFileEntry, StorageFile?, LazyCacheImageFileViewModel> cacheImageViewModelFactory = (entry, file) => 
                {
                    return new LazyCacheImageFileViewModel(col, sortType, entry, new StorageItemImageSource(file), _messenger,
                                _sourceStorageItemsRepository,
                                _bookmarkManager,
                                _thumbnailManager,
                                _albamRepository,
                                Selection);
                };

                var d1 = imageCollectionContext.CreateImageFileChangedObserver()
                    .ToObservable()
                    .SubscribeAwait((col, FileItemsView, cacheImageViewModelFactory), async (_, s, ct) =>
                    {
                        var (col, items, itemFacotry) = s;
                        //await ReloadItemsAsync(col, ct);
                        var ignore = col.Context.HandleDiffItems(
                            (ObservableCollection<IStorageItemViewModel>)items.Source, 
                            items.DeferRefresh,
                            itemFacotry,
                            (IStorageItemViewModel itemVM) => itemVM.Path,
                            ct);
                    });

                disposable.Add(d1);
                _itemsDisposable = disposable;

                using (FileItemsView.DeferRefresh())
                {
                    IsFavoriteFilteredDisplayEnabled = false;
                    ImageFileItems.Clear();
                    ImageFileItems.AddRange(col.Context.GetCacheItems().Select(entry =>
                    {
                        return new LazyCacheImageFileViewModel(col, sortType, entry, null, _messenger,
                            _sourceStorageItemsRepository,
                            _bookmarkManager,
                            _thumbnailManager,
                            _albamRepository,
                            Selection);
                    }));                    

                    if (await col.Context.CheckIsNotSameCacheCountAndExactCountAsync(ct))
                    {
                        await col.Context.HandleDiffItems(
                                (ObservableCollection<IStorageItemViewModel>)FileItemsView.Source,
                                FileItemsView.DeferRefresh,
                                cacheImageViewModelFactory,
                                (IStorageItemViewModel itemVM) => itemVM.Path,
                                ct);
                    }
                }
            }
            else // pdfやzipなどは構造が固定でIndexアクセスしても安定する
            {
                Guard.IsNotNull(_imageCollectionContext);
                using (FileItemsView.DeferRefresh())
                {
                    IsFavoriteFilteredDisplayEnabled = false;
                    ImageFileItems.Clear();
                    var count = await imageCollectionContext.GetImageFileCountAsync(ct);                    
                    ImageFileItems.AddRange(Enumerable.Range(0, count)
                        .Select(index =>
                        {
                            return new LazyImageFileViewModel(
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
                }                
            }
        }

        ct.ThrowIfCancellationRequested();

        HasFileItem = ImageFileItems.Any();
        DispatcherQueue.GetForCurrentThread().EnqueueAsync(async () =>
        {
            bool exist = await imageCollectionContext.IsExistFolderOrArchiveFileAsync(ct);
            HasFolderOrBookItem = exist;
        }, priority: DispatcherQueuePriority.Low).FireAndForgetSafe();
    }

    #endregion

    #region FileSortType


    public IEnumerable<SortDescription> ToSortDescription(FileSortType fileSortType)
    {
        IComparer? comparer = null;
        if (_currentImageSource?.StorageItem is StorageFile file
            && SupportedFileTypesHelper.PdfFileType == file.FileType
            )
        {
            comparer = TitleDigitCompletionComparer.Default;
        }

        return fileSortType switch
        {
            FileSortType.TitleAscending => new[] { new SortDescription(nameof(StorageItemViewModel.Name), SortDirection.Ascending, comparer) },
            FileSortType.TitleDecending => new[] { new SortDescription(nameof(StorageItemViewModel.Name), SortDirection.Descending, comparer) },
            FileSortType.UpdateTimeAscending => new[] { new SortDescription(nameof(StorageItemViewModel.DateCreated), SortDirection.Ascending) },
            FileSortType.UpdateTimeDecending => new[] { new SortDescription(nameof(StorageItemViewModel.DateCreated), SortDirection.Descending) },
            _ => Array.Empty<SortDescription>(),
        };
    }

    [RelayCommand]
    void ChangeFileSort(object sort)
    {
        Guard.IsNotNull(_currentImageSource);

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
                _displaySettingsByPathRepository.SetFolderAndArchiveSettings(_currentImageSource.Path, SelectedFileSortType);
            }
            else if (_currentImageSource is AlbamImageSource albamImageSource)
            {
                _displaySettingsByPathRepository.SetAlbamSettings(albamImageSource.AlbamId, SelectedFileSortType);
            }
            else if (_currentImageSource is AlbamItemImageSource albamItemImageSource)
            {
                _displaySettingsByPathRepository.SetAlbamSettings(albamItemImageSource.AlbamId, SelectedFileSortType);
            }
        }
        else
        {
            if (_currentImageSource.StorageItem is IStorageItem)
            {
                _displaySettingsByPathRepository.ClearFolderAndArchiveSettings(_currentImageSource.Path);
                if (_displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(_currentImageSource.Path) is not null and var parentSort
                && parentSort.ChildItemDefaultSort != null
                )
                {
                    DisplaySortTypeInheritancePath = parentSort.Path;
                    SelectedFileSortType = parentSort.ChildItemDefaultSort.Value;
                }
                else
                {
                    DisplaySortTypeInheritancePath = null;
                    SelectedFileSortType = _defaultFileSortType;
                }
            }
            else if (_currentImageSource is AlbamImageSource albamImageSource)
            {
                _displaySettingsByPathRepository.ClearAlbamSettings(albamImageSource.AlbamId);
                SelectedFileSortType = _defaultFileSortType;
            }
        }
    }

    async Task SetSort(FileSortType fileSort, CancellationToken ct)
    {
        using (await _refreshLock.LockAsync(ct))
        {
            SetSortAsyncUnsafe(fileSort);
        }
    }

    void SetSortAsyncUnsafe(FileSortType fileSort)
    {
        try
        {
            var sortDescriptions = ToSortDescription(fileSort);
            if (FileItemsView.SortDescriptions.Any())
            {
                FileItemsView.SortDescriptions.Clear();
            }
            foreach (var sort in sortDescriptions)
            {
                FileItemsView.SortDescriptions.Add(sort);
            }

            FileItemsView.RefreshSorting();
        }
        catch (COMException) { }
    }

    [RelayCommand]
    void SetParentFileSortWithCurrentSetting()
    {
        Guard.IsNotNull(_currentImageSource);

        _displaySettingsByPathRepository.SetFileParentSettings(Path.GetDirectoryName(_currentImageSource.Path), SelectedFileSortType);
    }



#endregion
}

