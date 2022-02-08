using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
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
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Domain.ReadingFeature;
using TsubameViewer.Models.Domain.RestoreNavigation;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Models.UseCase;
using TsubameViewer.Presentation.Navigations;
using TsubameViewer.Presentation.Services.UWP;
using TsubameViewer.Presentation.ViewModels.Albam.Commands;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.ViewModels.SourceFolders.Commands;
using TsubameViewer.Presentation.Views;
using Windows.Storage;
using Windows.UI.Xaml.Navigation;
using static TsubameViewer.Models.Domain.ImageViewer.ImageCollectionManager;
using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

namespace TsubameViewer.Presentation.ViewModels
{

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
        private readonly BookmarkManager _bookmarkManager;
        private readonly ImageCollectionManager _imageCollectionManager;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly AlbamRepository _albamRepository;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderLastIntractItemManager _folderLastIntractItemManager;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
        private bool _NowProcessing;
        public bool NowProcessing
        {
            get { return _NowProcessing; }
            set { SetProperty(ref _NowProcessing, value); }
        }

        public SecondaryTileManager SecondaryTileManager { get; }
        public OpenPageCommand OpenPageCommand { get; }
        public OpenFolderItemCommand OpenFolderItemCommand { get; }
        public OpenImageViewerCommand OpenImageViewerCommand { get; }
        public OpenFolderListupCommand OpenFolderListupCommand { get; }
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

        private static readonly Models.Infrastructure.AsyncLock _NavigationLock = new ();

        private string _currentPath;
        private object _currentItem;

        private CancellationTokenSource _leavePageCancellationTokenSource;

        bool _isCompleteEnumeration = false;

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

        private string _currentArchiveFolderName;

        private string _DisplayCurrentArchiveFolderName;
        public string DisplayCurrentArchiveFolderName
        {
            get { return _DisplayCurrentArchiveFolderName; }
            private set { SetProperty(ref _DisplayCurrentArchiveFolderName, value); }
        }
        
        public ReactiveProperty<FileDisplayMode> FileDisplayMode { get; }
        public FileDisplayMode[] FileDisplayModeItems { get; } = new FileDisplayMode[]
        {
            Models.Domain.FolderItemListing.FileDisplayMode.Large,
            Models.Domain.FolderItemListing.FileDisplayMode.Midium,
            Models.Domain.FolderItemListing.FileDisplayMode.Small,
            Models.Domain.FolderItemListing.FileDisplayMode.Line,
        };

        static bool _LastIsImageFileGenerateThumbnailEnabled;


        public string FoldersManagementPageName => PrimaryWindowCoreLayout.HomePageName;

        IDisposable _ImageCollectionDisposer;

        CompositeDisposable _disposables = new CompositeDisposable();
        CompositeDisposable _navigationDisposables;

        public ImageListupPageViewModel(
            IMessenger messenger,
            IScheduler scheduler,
            BookmarkManager bookmarkManager,
            ImageCollectionManager imageCollectionManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            AlbamRepository albamRepository,
            ThumbnailManager thumbnailManager,
            SecondaryTileManager secondaryTileManager,
            FolderLastIntractItemManager folderLastIntractItemManager,
            FolderListingSettings folderListingSettings,
            DisplaySettingsByPathRepository displaySettingsByPathRepository,
            OpenPageCommand openPageCommand,
            OpenFolderItemCommand openFolderItemCommand,
            OpenImageViewerCommand openImageViewerCommand,
            OpenFolderListupCommand openFolderListupCommand,
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
            if (_currentItem is IStorageItem storageItem)
            {
                lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(storageItem.Path);
            }
            else if (_currentItem is AlbamEntry albamEntry)
            {
                lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(albamEntry._id);
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

            foreach (var itemVM in ImageFileItems)
            {
                itemVM.StopImageLoading();
            }

            base.OnNavigatedFrom(parameters);
        }

        


        void ClearCurrentContent()
        {
            _ImageCollectionDisposer?.Dispose();
            _ImageCollectionDisposer = null;

            foreach (var itemVM in ImageFileItems)
            {
                itemVM.Dispose();
            }
            ImageFileItems.Clear();

            CurrentFolderItem?.Dispose();
            CurrentFolderItem = null;
        }

        async Task ResetContentWithStorageItem(string path, CancellationToken ct)
        {
            using var lockReleaser = await _NavigationLock.LockAsync(ct);

            HasFileItem = false;
           
            _currentPath = path;
            _currentItem = null;

            // SourceStorageItemsRepositoryへの登録が遅延する可能性がある
            IStorageItem currentItem = null;
            foreach (var _ in Enumerable.Repeat(0, 10))
            {
                currentItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(_currentPath);
                if (currentItem != null)
                {
                    break;
                }
                await Task.Delay(100);
            }

            if (currentItem == null)
            {
                throw new Exception();
            }

            DisplayCurrentPath = currentItem.Path;


            var settings = _displaySettingsByPathRepository.GetFolderAndArchiveSettings(_currentPath);
            if (settings != null)
            {
                DisplaySortTypeInheritancePath = null;
                SelectedFileSortType.Value = settings.Sort;
            }
            else if (_displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(_currentPath) is not null and var parentSort
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
            await RefreshFolderItems(currentItem, _leavePageCancellationTokenSource.Token);

            HasFileItem = ImageFileItems.Any();

            OnPropertyChanged(nameof(ImageFileItems));
        }

        async Task ResetContentWithAlbam(Guid albamId, CancellationToken ct)
        {
            using var lockReleaser = await _NavigationLock.LockAsync(ct);

            HasFileItem = false;

            _currentPath = null;
            _currentItem = null;

            // SourceStorageItemsRepositoryへの登録が遅延する可能性がある
            var albam = _albamRepository.GetAlbam(albamId);

            if (albam == null)
            {
                throw new Exception();
            }

            DisplayCurrentPath = albam.Name;

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
            await RefreshFolderItems(albam, _leavePageCancellationTokenSource.Token);

            HasFileItem = ImageFileItems.Any();

            OnPropertyChanged(nameof(ImageFileItems));
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationDisposables = new CompositeDisposable();
            _leavePageCancellationTokenSource = new CancellationTokenSource();
            _navigationDisposables.Add(_leavePageCancellationTokenSource);

            var ct = _leavePageCancellationTokenSource.Token;

            NowProcessing = true;
            try
            {
                var mode = parameters.GetNavigationMode();

                _currentArchiveFolderName = null;

                if (parameters.TryGetValueSafe(PageNavigationConstants.GeneralPathKey, out string path))
                {                    
                    (var itemPath, _, _currentArchiveFolderName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(path));
                    var unescapedPath = itemPath;                    
                    if (unescapedPath != _currentPath
                        || (string.IsNullOrEmpty(_currentArchiveFolderName) is false
                            && _imageCollectionContext is ArchiveImageCollectionContext archiveImageCollectionContext
                            && archiveImageCollectionContext.ArchiveDirectoryToken?.Key != _currentArchiveFolderName
                            )
                        )
                    {
                        await ResetContentWithStorageItem(unescapedPath, ct);
                    }
                    else
                    {
                        foreach (var itemVM in ImageFileItems)
                        {
                            itemVM.RestoreThumbnailLoadingTask(ct);
                        }
                    }
                }
                else if (parameters.TryGetValueSafe(PageNavigationConstants.AlbamPathKey, out string albamPath))
                {
                    (var albamIdString, _, _currentArchiveFolderName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(albamPath));

                    if (Guid.TryParse(albamIdString, out var albamId) is true)
                    {
                        await ResetContentWithAlbam(albamId, ct);
                    }
                }
                if (mode != NavigationMode.New)
                {
                    string lastIntaractItem = null;
                    if (_currentItem is IStorageItem storageItem)
                    {
                        lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(storageItem.Path);                        
                    }    
                    else if (_currentItem is AlbamEntry albam)
                    {
                        lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(albam._id);
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
                    .Subscribe(async _ =>
                    {
                        await ReloadItemsAsync(_imageCollectionContext, ct);
                        Debug.WriteLine("Images Update required. " + _currentPath);
                    })
                    .AddTo(_navigationDisposables);
            }

            _messenger.Register<AlbamItemAddedMessage>(this, (r, m) => 
            {
                var (albamId, path) = m.Value;
                if (albamId == FavoriteAlbam.FavoriteAlbamId)
                {
                    var itemVM = ImageFileItems.FirstOrDefault(x => x.Path == path);
                    itemVM.IsFavorite = true;
                }
            });

            _messenger.Register<AlbamItemRemovedMessage>(this, (r, m) =>
            {
                var (albamId, path) = m.Value;
                if (albamId == FavoriteAlbam.FavoriteAlbamId)
                {
                    var itemVM = ImageFileItems.FirstOrDefault(x => x.Path == path);
                    itemVM.IsFavorite = false;
                }
            });

            await base.OnNavigatedToAsync(parameters);
        }

        #region Refresh Item

        IImageCollectionContext _imageCollectionContext;
        private static readonly Models.Infrastructure.AsyncLock _RefreshLock = new ();
        private async Task RefreshFolderItems(object currentItem, CancellationToken ct)
        {
            using var lockObject = await _RefreshLock.LockAsync(ct);

            _currentItem = currentItem;
            _ImageCollectionDisposer?.Dispose();
            _ImageCollectionDisposer = null;
            ImageFileItems.Clear();

            _IsFavoriteAlbam = false;
            _imageCollectionContext = null;
            _isCompleteEnumeration = false;
            IImageCollectionContext imageCollectionContext = null;
            if (currentItem is StorageFolder folder)
            {
                Debug.WriteLine(folder.Path);
                imageCollectionContext = _imageCollectionManager.GetFolderImageCollectionContext(folder, ct);
                CurrentFolderItem = new StorageItemViewModel(new StorageItemImageSource(folder, _folderListingSettings, _thumbnailManager), _sourceStorageItemsRepository, _bookmarkManager, _albamRepository);
            }
            else if (currentItem is StorageFile file)
            {
                Debug.WriteLine(file.Path);
                if (file.IsSupportedImageFile())
                {
                    try
                    {
                        var parentFolder = await file.GetParentAsync();
                        imageCollectionContext = _imageCollectionManager.GetFolderImageCollectionContext(parentFolder, ct);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        var parentItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(Path.GetDirectoryName(_currentPath));
                        if (parentItem is StorageFolder parentFolder)
                        {
                            imageCollectionContext = _imageCollectionManager.GetFolderImageCollectionContext(parentFolder, ct);
                        }
                    }

                    CurrentFolderItem = new StorageItemViewModel(new StorageItemImageSource(file, _folderListingSettings, _thumbnailManager), _sourceStorageItemsRepository, _bookmarkManager, _albamRepository);
                }
                else if (file.IsSupportedMangaFile())
                {
                    imageCollectionContext = await _imageCollectionManager.GetArchiveImageCollectionContextAsync(file, _currentArchiveFolderName ?? String.Empty, ct);
                    DisplayCurrentArchiveFolderName = _currentArchiveFolderName;
                    if (_currentArchiveFolderName == null)
                    {
                        CurrentFolderItem = new StorageItemViewModel(new StorageItemImageSource(file, _folderListingSettings, _thumbnailManager), _sourceStorageItemsRepository, _bookmarkManager, _albamRepository);
                    }
                    else if (imageCollectionContext is ArchiveImageCollectionContext aic)
                    {
                        CurrentFolderItem = new StorageItemViewModel(new ArchiveDirectoryImageSource(aic.ArchiveImageCollection, aic.ArchiveDirectoryToken, _folderListingSettings, _thumbnailManager), _sourceStorageItemsRepository, _bookmarkManager, _albamRepository);
                    }
                }
            }
            else if (currentItem is AlbamEntry albam)
            {
                imageCollectionContext = new AlbamImageCollectionContext(albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _folderListingSettings, _thumbnailManager, _messenger);
                CurrentFolderItem = new StorageItemViewModel(new AlbamImageSource(albam, imageCollectionContext as AlbamImageCollectionContext), _sourceStorageItemsRepository, _bookmarkManager, _albamRepository);
                _IsFavoriteAlbam = albam._id == FavoriteAlbam.FavoriteAlbamId;
            }
            else
            {
                throw new NotSupportedException();
            }

            OnPropertyChanged(nameof(IsFavoriteAlbam));

            _isCompleteEnumeration = true;

            if (imageCollectionContext == null) { return; }

            _imageCollectionContext = imageCollectionContext;
            _ImageCollectionDisposer = imageCollectionContext as IDisposable;

            await ReloadItemsAsync(_imageCollectionContext, ct);
        }

        private async Task ReloadItemsAsync(IImageCollectionContext imageCollectionContext, CancellationToken ct)
        {
            var oldItemPathMap = ImageFileItems.Select(x => x.Path).ToHashSet();
            var newItems = await imageCollectionContext.GetImageFilesAsync(ct).ToListAsync(ct);
            var deletedItems = Enumerable.Except(oldItemPathMap, newItems.Select(x => x.Path))
                .Where(x => oldItemPathMap.Contains(x))
                .ToHashSet();

            using (FileItemsView.DeferRefresh())
            {
                // 削除アイテム
                Debug.WriteLine($"items count : {ImageFileItems.Count}");
                foreach (var itemVM in ImageFileItems.Where(x => deletedItems.Contains(x.Path)).ToArray())
                {
                    itemVM.Dispose();
                    ImageFileItems.Remove(itemVM);
                }
                Debug.WriteLine($"after deleted : {ImageFileItems.Count}");
                // 新規アイテム
                foreach (var item in newItems.Where(x => oldItemPathMap.Contains(x.Path) is false))
                {
                    ImageFileItems.Add(new StorageItemViewModel(item, _sourceStorageItemsRepository, _bookmarkManager, _albamRepository, Selection));
                }
                Debug.WriteLine($"after added : {ImageFileItems.Count}");
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
            if (_currentItem is StorageFile file
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
                    if (_currentItem is IStorageItem)
                    {
                        _displaySettingsByPathRepository.SetFolderAndArchiveSettings(_currentPath, SelectedFileSortType.Value);
                    }
                    else if (_currentItem is AlbamEntry albam)
                    {
                        _displaySettingsByPathRepository.SetAlbamSettings(albam._id, SelectedFileSortType.Value);
                    }
                }
                else
                {
                    if (_currentItem is IStorageItem)
                    {
                        _displaySettingsByPathRepository.ClearFolderAndArchiveSettings(_currentPath);
                        if (_displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(_currentPath) is not null and var parentSort
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
                    else if (_currentItem is AlbamEntry albam)
                    {
                        _displaySettingsByPathRepository.ClearAlbamSettings(albam._id);
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
                _displaySettingsByPathRepository.SetFileParentSettings(Path.GetDirectoryName(_currentPath), SelectedFileSortType.Value);
            });



#endregion
    }
}
