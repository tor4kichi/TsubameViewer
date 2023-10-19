using I18NPortable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views;
using Windows.Storage;
using Windows.UI.Xaml.Navigation;
using CommunityToolkit.Diagnostics;
using TsubameViewer.Contracts.Notification;

namespace TsubameViewer.ViewModels;


public sealed class SelectionContext : ObservableObject
{
    private bool _isSelectionModeEnabled;
    public bool IsSelectionModeEnabled
    {
        get => _isSelectionModeEnabled;
        private set => SetProperty(ref _isSelectionModeEnabled, value);
    }

    public ObservableCollection<StorageItemViewModel> SelectedItems { get; } = new ();

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

public sealed class ImageListupPageViewModel : NavigationAwareViewModelBase
{

    private readonly IMessenger _messenger;
    private readonly IScheduler _scheduler;
    private readonly LocalBookmarkRepository _bookmarkManager;
    private readonly ImageCollectionManager _imageCollectionManager;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly AlbamRepository _albamRepository;
    private readonly ThumbnailImageManager _thumbnailManager;
    private readonly LastIntractItemRepository _folderLastIntractItemManager;
    private readonly FolderListingSettings _folderListingSettings;
    private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    private bool _NowProcessing;
    public bool NowProcessing
    {
        get { return _NowProcessing; }
        set { SetProperty(ref _NowProcessing, value); }
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
    public AlbamItemEditCommand AlbamItemEditCommand { get; }
    public FavoriteAddCommand FavoriteAddCommand { get; }
    public FavoriteRemoveCommand FavoriteRemoveCommand { get; }
    public AlbamItemRemoveCommand AlbamItemRemoveCommand { get; }
    public ObservableCollection<StorageItemViewModel> ImageFileItems { get; }



    private AdvancedCollectionView _FileItemsView;
    public AdvancedCollectionView FileItemsView
    {
        get { return _FileItemsView; }
        set { SetProperty(ref _FileItemsView, value); }
    }

    private bool _HasFileItem;
    public bool HasFileItem
    {
        get { return _HasFileItem; }
        set { SetProperty(ref _HasFileItem, value); }
    }

    private bool _HasFolderOrBookItem;
    public bool HasFolderOrBookItem
    {
        get { return _HasFolderOrBookItem; }
        set { SetProperty(ref _HasFolderOrBookItem, value); }
    }


    public SelectionContext Selection { get; } = new SelectionContext();

    public ReactivePropertySlim<FileSortType> SelectedFileSortType { get; }

    private readonly FileSortType DefaultFileSortType = FileSortType.TitleAscending;

    private string _DisplaySortTypeInheritancePath;
    public string DisplaySortTypeInheritancePath
    {
        get { return _DisplaySortTypeInheritancePath; }
        private set { SetProperty(ref _DisplaySortTypeInheritancePath, value); }
    }


    public ReactivePropertySlim<int> ImageLastIntractItem { get; }

    private static readonly Core.AsyncLock _NavigationLock = new ();

    private IImageSource? _currentImageSource;
    IImageCollectionContext _imageCollectionContext;
    private static readonly Core.AsyncLock _RefreshLock = new();

    private CancellationTokenSource _navigationCts;

    private string _DisplayCurrentPath;
    public string DisplayCurrentPath
    {
        get { return _DisplayCurrentPath; }
        set { SetProperty(ref _DisplayCurrentPath, value); }
    }

    private StorageItemViewModel _CurrentFolderItem;
    public StorageItemViewModel CurrentFolderItem
    {
        get { return _CurrentFolderItem; }
        set { SetProperty(ref _CurrentFolderItem, value); }
    }

    private bool _IsFavoriteAlbam;
    public bool IsFavoriteAlbam
    {
        get => _IsFavoriteAlbam;
        set => SetProperty(ref _IsFavoriteAlbam, value);
    }

    private string _DisplayCurrentArchiveFolderName;
    public string DisplayCurrentArchiveFolderName
    {
        get { return _DisplayCurrentArchiveFolderName; }
        private set { SetProperty(ref _DisplayCurrentArchiveFolderName, value); }
    }
    
    public ReactiveProperty<FileDisplayMode> FileDisplayMode { get; }
    public FileDisplayMode[] FileDisplayModeItems { get; } = new FileDisplayMode[]
    {
        Core.Models.FolderItemListing.FileDisplayMode.Large,
        Core.Models.FolderItemListing.FileDisplayMode.Midium,
        Core.Models.FolderItemListing.FileDisplayMode.Small,
        Core.Models.FolderItemListing.FileDisplayMode.Line,
    };


    public string FoldersManagementPageName => PrimaryWindowCoreLayout.HomePageName;

    CompositeDisposable _disposables = new CompositeDisposable();
    CompositeDisposable _navigationDisposables;

    public ImageListupPageViewModel(
        IMessenger messenger,
        IScheduler scheduler,
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
        AlbamItemEditCommand albamItemEditCommand,
        AlbamItemRemoveCommand albamItemRemoveCommand,
        FavoriteAddCommand favoriteAddCommand,
        FavoriteRemoveCommand favoriteRemoveCommand
        )
    {
        _messenger = messenger;
        _scheduler = scheduler;
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
        AlbamItemEditCommand = albamItemEditCommand;
        AlbamItemRemoveCommand = albamItemRemoveCommand;
        FavoriteAddCommand = favoriteAddCommand;
        FavoriteRemoveCommand = favoriteRemoveCommand;
        ImageFileItems = new ObservableCollection<StorageItemViewModel>();

        FileItemsView = new AdvancedCollectionView(ImageFileItems);
        SelectedFileSortType = new ReactivePropertySlim<FileSortType>(FileSortType.TitleAscending)
            .AddTo(_disposables);

        FileDisplayMode = _folderListingSettings.ToReactivePropertyAsSynchronized(x => x.FileDisplayMode)
            .AddTo(_disposables);
        ImageLastIntractItem = new ReactivePropertySlim<int>()
            .AddTo(_disposables);

               
    }

    public StorageItemViewModel GetLastIntractItem()
    {
        string lastIntaractItem = null;
        if (_currentImageSource.StorageItem is IStorageItem storageItem)
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

    public void SetLastIntractItem(StorageItemViewModel itemVM)
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

        _navigationDisposables?.Dispose();

        foreach (var itemVM in ImageFileItems.Reverse())
        {
            itemVM.StopImageLoading();
        }

        base.OnNavigatedFrom(parameters);
    }
    

    private async ValueTask<bool> IsRequireUpdateAsync(string newPath, string pageName, CancellationToken ct)
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


        return false;
    }

    public override async Task OnNavigatedToAsync(INavigationParameters parameters)
    {
        var mode = parameters.GetNavigationMode();
        _navigationDisposables = new CompositeDisposable();
        _navigationCts = new CancellationTokenSource();
        _navigationDisposables.Add(_navigationCts);

        var ct = _navigationCts.Token;

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

                    foreach (var itemVM in ImageFileItems)
                    {
                        itemVM.RestoreThumbnailLoadingTask(ct);
                    }
                }
            }
            else if (parameters.TryGetValue(PageNavigationConstants.AlbamPathKey, out string albamPath))
            {
                (var albamIdString, _) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(albamPath));

                await ResetContentWithAlbam(albamIdString, ct);
            }
            if (mode != NavigationMode.New)
            {
                string lastIntaractItem = null;
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
                    ImageLastIntractItem.Value = ImageFileItems.IndexOf(item);
                }
                else
                {
                    ImageLastIntractItem.Value = 0;
                }
            }
        }
        finally
        {
            NowProcessing = false;
        }

        SelectedFileSortType
            .Subscribe(async _ =>
            {
                await SetSort(SelectedFileSortType.Value, ct);
            })
            .AddTo(_navigationDisposables);

        if (_imageCollectionContext?.IsSupportedFolderContentsChanged ?? false)
        {
            // アプリ内部操作も含めて変更を検知する
            _imageCollectionContext.CreateImageFileChangedObserver()
                .Subscribe(_ =>
                {
                    _scheduler.Schedule(async () => 
                    {
                        await ReloadItemsAsync(_imageCollectionContext, ct);
                        Debug.WriteLine("Images Update required. " + _currentImageSource);
                    });
                })
                .AddTo(_navigationDisposables);
        }

        _messenger.Register<AlbamItemAddedMessage>(this, (r, m) => 
        {
            var (albamId, path, itemType) = m.Value;
            if (albamId == FavoriteAlbam.FavoriteAlbamId)
            {
                var itemVM = ImageFileItems.FirstOrDefault(x => x.Path == path);
                itemVM.IsFavorite = true;
            }
        });

        _messenger.Register<AlbamItemRemovedMessage>(this, (r, m) =>
        {
            var (albamId, path, itemType) = m.Value;
            if (albamId == FavoriteAlbam.FavoriteAlbamId)
            {
                var itemVM = ImageFileItems.FirstOrDefault(x => x.Path == path);
                itemVM.IsFavorite = false;
            }
        });

        await base.OnNavigatedToAsync(parameters);
    }

    #region Refresh Item

    private void ClearContent()
    {
        foreach (var item in ImageFileItems)
        {
            item.Dispose();
        }
        ImageFileItems.Clear();

        IsFavoriteAlbam = false;
        (_imageCollectionContext as IDisposable)?.Dispose();
        _imageCollectionContext = null;
        (CurrentFolderItem as IDisposable)?.Dispose();
        CurrentFolderItem = null;
        DisplayCurrentArchiveFolderName = null;
    }


    async Task ResetContentWithStorageItem(string path, string pageName, CancellationToken ct)
    {
        using var lockReleaser = await _NavigationLock.LockAsync(ct);

        HasFileItem = false;

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
            SelectedFileSortType.Value = settings.Sort;
        }
        else if (_displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(_currentImageSource.Path) is not null and var parentSort
            && parentSort.ChildItemDefaultSort != null
            )
        {
            DisplaySortTypeInheritancePath = parentSort.Path;
            SelectedFileSortType.Value = parentSort.ChildItemDefaultSort.Value;
        }
        else
        {
            DisplaySortTypeInheritancePath = null;
            SelectedFileSortType.Value = DefaultFileSortType;
        }
               
        await SetSort(SelectedFileSortType.Value, ct);        
        await ReloadItemsAsync(_imageCollectionContext, ct);

        HasFileItem = ImageFileItems.Any();

        OnPropertyChanged(nameof(ImageFileItems));
    }

    async Task ResetContentWithAlbam(string albamIdString, CancellationToken ct)
    {
        using var lockReleaser = await _NavigationLock.LockAsync(ct);

        HasFileItem = false;
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
            SelectedFileSortType.Value = settings.Sort;
        }
        else
        {
            DisplaySortTypeInheritancePath = null;
            SelectedFileSortType.Value = DefaultFileSortType;
        }

        await SetSort(SelectedFileSortType.Value, ct);
        await ReloadItemsAsync(_imageCollectionContext, ct);

        OnPropertyChanged(nameof(ImageFileItems));
    }

    private async Task ReloadItemsAsync(IImageCollectionContext imageCollectionContext, CancellationToken ct)
    {
        var existItemsHashSet = ImageFileItems.Select(x => x.Path).ToHashSet();
        using (FileItemsView.DeferRefresh())
        {
            // 削除アイテム
            Debug.WriteLine($"items count : {ImageFileItems.Count}");
            
            // 新規アイテム
            await foreach (var item in imageCollectionContext.GetImageFilesAsync(ct))
            {
                if (existItemsHashSet.Contains(item.Path) is false)
                {
                    ImageFileItems.Add(new StorageItemViewModel(item, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository, Selection));
                }
                else
                {
                    existItemsHashSet.Remove(item.Path);
                }
            }

            Debug.WriteLine($"after added : {ImageFileItems.Count}");
            for (int i = ImageFileItems.Count -  1; i >= 0; i--)
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

        ct.ThrowIfCancellationRequested();

        HasFileItem = ImageFileItems.Any();
        _ = Task.Run(async () =>
        {
            bool exist = await imageCollectionContext.IsExistFolderOrArchiveFileAsync(ct);
            _scheduler.Schedule(() => HasFolderOrBookItem = exist);
        }, ct);
    }

#endregion

#region FileSortType


    public IEnumerable<SortDescription> ToSortDescription(FileSortType fileSortType)
    {
        IComparer comparer = null;
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
            _ => throw new NotSupportedException(),
        };
    }

    private RelayCommand<object> _ChangeFileSortCommand;
    public RelayCommand<object> ChangeFileSortCommand =>
        _ChangeFileSortCommand ??= new RelayCommand<object>(sort =>
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
                SelectedFileSortType.Value = sortType.Value;
                if (_currentImageSource.StorageItem is IStorageItem)
                {
                    _displaySettingsByPathRepository.SetFolderAndArchiveSettings(_currentImageSource.Path, SelectedFileSortType.Value);
                }
                else if (_currentImageSource is AlbamImageSource albamImageSource)
                {
                    _displaySettingsByPathRepository.SetAlbamSettings(albamImageSource.AlbamId, SelectedFileSortType.Value);
                }
                else if (_currentImageSource is AlbamItemImageSource albamItemImageSource)
                {
                    _displaySettingsByPathRepository.SetAlbamSettings(albamItemImageSource.AlbamId, SelectedFileSortType.Value);
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
                        SelectedFileSortType.Value = parentSort.ChildItemDefaultSort.Value;
                    }
                    else
                    {
                        DisplaySortTypeInheritancePath = null;
                        SelectedFileSortType.Value = DefaultFileSortType;
                    }
                }
                else if (_currentImageSource is AlbamImageSource albamImageSource)
                {
                    _displaySettingsByPathRepository.ClearAlbamSettings(albamImageSource.AlbamId);
                    SelectedFileSortType.Value = DefaultFileSortType;
                }
            }
        });

    private async Task SetSort(FileSortType fileSort, CancellationToken ct)
    {
        using (await _RefreshLock.LockAsync(ct))
        {
            SetSortAsyncUnsafe(fileSort);
        }
    }

    private void SetSortAsyncUnsafe(FileSortType fileSort)
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

    private RelayCommand _SetParentFileSortWithCurrentSettingCommand;
    public RelayCommand SetParentFileSortWithCurrentSettingCommand =>
        _SetParentFileSortWithCurrentSettingCommand ??= new RelayCommand(() =>
        {
            Guard.IsNotNull(_currentImageSource);

            _displaySettingsByPathRepository.SetFileParentSettings(Path.GetDirectoryName(_currentImageSource.Path), SelectedFileSortType.Value);
        });



#endregion
}
