using Microsoft.Toolkit.Uwp.UI;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.ReadingFeature;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.RestoreNavigation;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.Services;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;
using Windows.UI.Xaml.Media.Animation;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Presentation.ViewModels.SourceFolders.Commands;
using System.Collections;
using System.Reactive.Concurrency;
using Microsoft.Toolkit.Mvvm.Messaging;
using System.Windows.Input;
using TsubameViewer.Models.Domain.Albam;
using Windows.UI.Xaml;
using I18NPortable;
using TsubameViewer.Presentation.Navigations;
using System.Reactive.Disposables;
using Windows.UI.Xaml.Navigation;
using Microsoft.Toolkit.Mvvm.Input;
using TsubameViewer.Models.Domain.Navigation;
using TsubameViewer.Presentation.Services.UWP;

namespace TsubameViewer.Presentation.ViewModels
{
    using static TsubameViewer.Models.Domain.ImageViewer.ImageCollectionManager;
    using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;


    public class CachedFolderListupItems
    {
        public ObservableCollection<StorageItemViewModel> FolderItems { get; set; }
        public int GetTotalCount()
        {
            return FolderItems.Count;
        }

        public void DisposeItems()
        {
            foreach (var itemVM in FolderItems)
            {
                itemVM.Dispose();
            }
        }
    }


    public sealed class FolderListupPageViewModel : NavigationAwareViewModelBase
    {
        private bool _NowProcessing;
        public bool NowProcessing
        {
            get { return _NowProcessing; }
            set { SetProperty(ref _NowProcessing, value); }
        }

        private readonly IScheduler _scheduler;
        private readonly IMessenger _messenger;
        private readonly BookmarkManager _bookmarkManager;
        private readonly AlbamRepository _albamRepository;
        private readonly ImageCollectionManager _imageCollectionManager;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly FolderLastIntractItemManager _folderLastIntractItemManager;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
        private readonly BackNavigationCommand _backNavigationCommand;

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
        public ObservableCollection<StorageItemViewModel> FolderItems { get; private set; }

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


        public ReactivePropertySlim<FileSortType> SelectedFileSortType { get; }
        public ReactivePropertySlim<FileSortType?> SelectedChildFileSortType { get; }


        public ReactivePropertySlim<StorageItemViewModel> FolderLastIntractItem { get; }

        private static readonly Models.Infrastructure.AsyncLock _NavigationLock = new ();

        private string _currentPath;
        private IStorageItem _currentItem;

        private CancellationTokenSource _leavePageCancellationTokenSource;

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
        
        public SelectionContext Selection { get; } = new SelectionContext();

        private string _selectedCountDisplayText;
        public string SelectedCountDisplayText
        {
            get => _selectedCountDisplayText;
            set => SetProperty(ref _selectedCountDisplayText, value);
        }


        IDisposable _ImageCollectionDisposer;

        public string FoldersManagementPageName => PrimaryWindowCoreLayout.HomePageName;

        private string _currentArchiveFolderName;

        private string _DisplayCurrentArchiveFolderName;
        public string DisplayCurrentArchiveFolderName
        {
            get { return _DisplayCurrentArchiveFolderName; }
            private set { SetProperty(ref _DisplayCurrentArchiveFolderName, value); }
        }

        CompositeDisposable _disposables = new CompositeDisposable();
        CompositeDisposable _navigationDisposables;


        public FolderListupPageViewModel(
            IScheduler scheduler,
            IMessenger messenger,
            BookmarkManager bookmarkManager,
            AlbamRepository albamRepository,
            ImageCollectionManager imageCollectionManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            ISecondaryTileManager secondaryTileManager,
            FolderLastIntractItemManager folderLastIntractItemManager,
            ThumbnailManager thumbnailManager,
            FolderListingSettings folderListingSettings,
            DisplaySettingsByPathRepository displaySettingsByPathRepository,
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
            FileDeleteCommand fileDeleteCommand
            )
        {
            _scheduler = scheduler;
            _messenger = messenger;
            _bookmarkManager = bookmarkManager;
            _albamRepository = albamRepository;
            _imageCollectionManager = imageCollectionManager;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            SecondaryTileManager = secondaryTileManager;
            _folderLastIntractItemManager = folderLastIntractItemManager;
            _thumbnailManager = thumbnailManager;
            _folderListingSettings = folderListingSettings;
            _displaySettingsByPathRepository = displaySettingsByPathRepository;
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
            FolderItems = new ObservableCollection<StorageItemViewModel>();
            FileItemsView = new AdvancedCollectionView(FolderItems);
            FolderLastIntractItem = new ReactivePropertySlim<StorageItemViewModel>()
                .AddTo(_disposables);

            SelectedFileSortType = new ReactivePropertySlim<FileSortType>(FileSortType.TitleAscending, ReactivePropertyMode.DistinctUntilChanged)
                .AddTo(_disposables);

            SelectedChildFileSortType = new ReactivePropertySlim<FileSortType?>(null)
                .AddTo(_disposables);
        }


        public override async void OnNavigatedFrom(INavigationParameters parameters)
        {
            Selection.EndSelection();
            using (await _NavigationLock.LockAsync(default))
            {
                _leavePageCancellationTokenSource?.Cancel();
                _leavePageCancellationTokenSource?.Dispose();
                _leavePageCancellationTokenSource = null;

                _navigationDisposables?.Dispose();
                _navigationDisposables = null;

                foreach (var itemVM in FolderItems)
                {
                    itemVM.StopImageLoading();
                }

                if (_currentPath != null && 
                    parameters.ContainsKey(PageNavigationConstants.GeneralPathKey) &&  parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string path))
                {
                    _folderLastIntractItemManager.SetLastIntractItemName(_currentPath, Uri.UnescapeDataString(path));
                }
                
                _messenger.Unregister<BackNavigationRequestingMessage>(this);
                _messenger.Unregister<StartMultiSelectionMessage>(this);

                base.OnNavigatedFrom(parameters);
            }
        }

        void ClearContent()
        {
            foreach (var itemVM in FolderItems)
            {
                itemVM.Dispose();
            }
            FolderItems.Clear();

            _ImageCollectionDisposer?.Dispose();
            _ImageCollectionDisposer = null;

            CurrentFolderItem?.Dispose();
            CurrentFolderItem = null;

        }

        public StorageItemViewModel GetLastIntractItem()
        {
            if (_currentItem == null) { return null; }

            var lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(_currentItem.Path);
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

        

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            // ナビゲーション全体をカバーしてロックしていないと_leavePageCancellationTokenSourceが先にキャンセルされているケースがある
            using var lockReleaser = await _NavigationLock.LockAsync(default);

            var mode = parameters.GetNavigationMode();
            if (mode == NavigationMode.Refresh)
            {
                await ReloadItemsAsync(_imageCollectionContext, _leavePageCancellationTokenSource.Token);
                return;
            }

            _navigationDisposables = new CompositeDisposable();
            _leavePageCancellationTokenSource = new CancellationTokenSource();
            var ct = _leavePageCancellationTokenSource.Token;

            NowProcessing = true;
            try
            {
                _currentArchiveFolderName = null;

                if (parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string path))
                {
                    (var itemPath, _currentArchiveFolderName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(path));

                    var unescapedPath = itemPath;
                    if (_sourceStorageItemsRepository.IsIgnoredPath(unescapedPath))
                    {
                        throw new InvalidOperationException();
                    }
                    else if (_currentPath != unescapedPath)                        
                    {        
                        await ResetContent(unescapedPath, ct);
                    }
                    else if (string.IsNullOrEmpty(_currentArchiveFolderName) is false
                            && _imageCollectionContext is ArchiveImageCollectionContext archiveImageCollectionContext
                            && archiveImageCollectionContext.ArchiveDirectoryToken?.Key != _currentArchiveFolderName
                            )
                    {
                        await ResetContent(unescapedPath, ct);
                    }
                    else
                    {
                        if (FolderItems != null)
                        {
                            foreach (var itemVM in FolderItems)
                            {
                                itemVM.RestoreThumbnailLoadingTask(ct);
                            }
                        }
                    }

                    _currentPath = unescapedPath;
                }
                else if (parameters.TryGetValue(PageNavigationConstants.AlbamPathKey, out string albamPath))
                {
                    _currentPath = null;

                    (var albamIdString, _currentArchiveFolderName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(albamPath));

                    if (Guid.TryParse(albamIdString, out Guid albamId))
                    {
                        var albam = _albamRepository.GetAlbam(albamId);
                        await ResetContentWithAlbam(albam, ct);
                    }
                }


                if (mode != NavigationMode.New)
                {
                    StorageItemViewModel lastIntractItemVM = GetLastIntractItem();
                    if (lastIntractItemVM != null)
                    {
                        lastIntractItemVM.ThumbnailChanged();
                        lastIntractItemVM.Initialize(ct);

                        FolderLastIntractItem.Value = lastIntractItemVM;
                    }
                    else
                    {
                        FolderLastIntractItem.Value = null;
                    }
                }
            }
            finally
            {
                NowProcessing = false;
            }

            SelectedFileSortType
                .Subscribe(x => _ = SetSort(x, _leavePageCancellationTokenSource?.Token ?? CancellationToken.None))
                .AddTo(_navigationDisposables);

            if (_imageCollectionContext?.IsSupportedFolderContentsChanged ?? false)
            {
                // アプリ内部操作も含めて変更を検知する
                bool requireRefresh = false;
                _imageCollectionContext.CreateFolderAndArchiveFileChangedObserver()
                    .Subscribe(_ =>
                    {
                        _scheduler.Schedule(async () => 
                        {
                            if (Window.Current.Visible)
                            {
                                requireRefresh = false;
                                await ReloadItemsAsync(_imageCollectionContext, _leavePageCancellationTokenSource?.Token ?? CancellationToken.None);
                            }
                            else
                            {
                                requireRefresh = true;
                            }
                        });

                        Debug.WriteLine("Folder andor Archive Update required. " + _currentPath);
                    })
                    .AddTo(_navigationDisposables);

                ApplicationLifecycleObservable.WindowActivationStateChanged()
                    .Subscribe(async visible =>
                    {
                        if (visible && requireRefresh && _imageCollectionContext is not null)
                        {
                            requireRefresh = false;
                            await ReloadItemsAsync(_imageCollectionContext, _leavePageCancellationTokenSource?.Token ?? CancellationToken.None);
                            Debug.WriteLine("Folder andor Archive Updated. " + _currentPath);
                        }
                    })
                    .AddTo(_navigationDisposables);
            }

            _messenger.Register<StartMultiSelectionMessage>(this, (r, m) => 
            {
                Selection.StartSelection();
                FileDeleteCommand.NotifyCanExecuteChanged();
                OpenWithExplorerCommand.NotifyCanExecuteChanged();
            });

            Selection.ObserveProperty(x => x.IsSelectionModeEnabled)
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
                .AddTo(_navigationDisposables);

            Selection.SelectedItems.ObserveProperty(x => x.Count)
                .Subscribe(count =>
                {
                    SelectedCountDisplayText = "ImageSelection_SelectedCount".Translate(count);
                    FileDeleteCommand.NotifyCanExecuteChanged();
                    OpenWithExplorerCommand.NotifyCanExecuteChanged();
                })
                .AddTo(_navigationDisposables);

            await base.OnNavigatedToAsync(parameters);
        }

        IImageCollectionContext _imageCollectionContext;

        async Task ResetContent(string path, CancellationToken ct)
        {
            HasFileItem = false;

            // PathReferenceCountManagerへの登録が遅延する可能性がある
            foreach (var _ in Enumerable.Repeat(0, 10))
            {
                _currentItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(path);
                if (_currentItem != null)
                {
                    break;
                }
                await Task.Delay(100, ct);
            }

            if (_currentItem == null)
            {
                throw new Exception();
            }

            DisplayCurrentPath = _currentItem.Path;

            var settingPath = path;
            var settings = _displaySettingsByPathRepository.GetFolderAndArchiveSettings(settingPath);
            if (settings != null)
            {
                SelectedFileSortType.Value = settings.Sort;
                SetSortAsyncUnsafe(SelectedFileSortType.Value, path);
            }
            else
            {
                if (_currentItem is StorageFolder)
                {
                    SelectedFileSortType.Value = FileSortType.TitleAscending;
                    SetSortAsyncUnsafe(SelectedFileSortType.Value, path);
                }
                else if (_currentItem is StorageFile file && file.IsSupportedMangaFile())
                {
                    SelectedFileSortType.Value = FileSortType.TitleAscending;
                    SetSortAsyncUnsafe(SelectedFileSortType.Value, path);
                }
            }

            SelectedChildFileSortType.Value = _displaySettingsByPathRepository.GetFileParentSettings(path);

            await RefreshFolderItems(path, ct);
        }

        async Task ResetContentWithAlbam(AlbamEntry albam, CancellationToken ct)
        {
            HasFileItem = false;
            
            DisplayCurrentPath = "Albam".Translate();

            string path = albam._id.ToString();
            /*
            var settings = _displaySettingsByPathRepository.GetFolderAndArchiveSettings(path);
            if (settings != null)
            {
                SelectedFileSortType.Value = settings.Sort;
                SetSortAsyncUnsafe(SelectedFileSortType.Value, path);
            }
            else
            {
                if (_currentItem is StorageFolder)
                {
                    SelectedFileSortType.Value = FileSortType.TitleAscending;
                    SetSortAsyncUnsafe(SelectedFileSortType.Value, path);
                }
                else if (_currentItem is StorageFile file && file.IsSupportedMangaFile())
                {
                    SelectedFileSortType.Value = FileSortType.TitleAscending;
                    SetSortAsyncUnsafe(SelectedFileSortType.Value, path);
                }
            }
            */

            //SelectedChildFileSortType.Value = _displaySettingsByPathRepository.GetFileParentSettings(path);
            SelectedChildFileSortType.Value = FileSortType.None;
            SelectedFileSortType.Value = FileSortType.UpdateTimeDecending;
            SetSortAsyncUnsafe(SelectedFileSortType.Value, path);

            await RefreshFolderItems(albam, ct);
        }

        #region Refresh Item

        private static readonly Models.Infrastructure.AsyncLock _RefreshLock = new ();
        private async Task RefreshFolderItems(string path, CancellationToken ct)
        {
            using var lockObject = await _RefreshLock.LockAsync(ct);

            ClearContent();
            DisplayCurrentArchiveFolderName = null;
            CurrentFolderItem = null;

            _imageCollectionContext = null;
            IImageCollectionContext imageCollectionContext = null;
            try
            {
                if (_currentItem is StorageFolder folder)
                {
                    Debug.WriteLine(folder.Path);
                    imageCollectionContext = _imageCollectionManager.GetFolderImageCollectionContext(folder, ct);
                    CurrentFolderItem = new StorageItemViewModel(new StorageItemImageSource(_currentItem, _folderListingSettings, _thumbnailManager), _messenger, _sourceStorageItemsRepository, _bookmarkManager, _albamRepository);
                }
                else if (_currentItem is StorageFile file)
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
                            var parentItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(Path.GetDirectoryName(path));
                            if (parentItem is StorageFolder parentFolder)
                            {
                                imageCollectionContext = _imageCollectionManager.GetFolderImageCollectionContext(parentFolder, ct);
                            }
                        }

                        CurrentFolderItem = new StorageItemViewModel(new StorageItemImageSource(_currentItem, _folderListingSettings, _thumbnailManager), _messenger, _sourceStorageItemsRepository, _bookmarkManager, _albamRepository);
                    }
                    else if (file.IsSupportedMangaFile())
                    {
                        // string.Emptyを渡すことでルートフォルダのフォルダ取得を行える
                        imageCollectionContext = await _imageCollectionManager.GetArchiveImageCollectionContextAsync(file, _currentArchiveFolderName ?? string.Empty, ct);
                        DisplayCurrentArchiveFolderName = _currentArchiveFolderName;
                        if (_currentArchiveFolderName == null)
                        {
                            CurrentFolderItem = new StorageItemViewModel(new StorageItemImageSource(_currentItem, _folderListingSettings, _thumbnailManager), _messenger, _sourceStorageItemsRepository, _bookmarkManager, _albamRepository);
                        }
                        else if (imageCollectionContext is ArchiveImageCollectionContext aic)
                        {
                            CurrentFolderItem = new StorageItemViewModel(new ArchiveDirectoryImageSource(aic.ArchiveImageCollection, aic.ArchiveDirectoryToken, _folderListingSettings, _thumbnailManager), _messenger, _sourceStorageItemsRepository, _bookmarkManager, _albamRepository);
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            catch (OperationCanceledException)
            {
                CurrentFolderItem = null;
                DisplayCurrentArchiveFolderName = _currentArchiveFolderName;
                return;
            }
            catch
            {
                CurrentFolderItem = null;
                DisplayCurrentArchiveFolderName = _currentArchiveFolderName;
                throw;
            }

            if (imageCollectionContext == null) { return; }

            _imageCollectionContext = imageCollectionContext;
            _ImageCollectionDisposer = imageCollectionContext as IDisposable;

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

        private async Task RefreshFolderItems(AlbamEntry albam, CancellationToken ct)
        {
            using var lockObject = await _RefreshLock.LockAsync(ct);

            ClearContent();
            DisplayCurrentArchiveFolderName = null;
            CurrentFolderItem = null;

            _imageCollectionContext = null;
            AlbamImageCollectionContext imageCollectionContext = new AlbamImageCollectionContext(albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _folderListingSettings, _thumbnailManager, _messenger);
            CurrentFolderItem = new StorageItemViewModel(new AlbamImageSource(albam, imageCollectionContext), _messenger, _sourceStorageItemsRepository, _bookmarkManager, _albamRepository);
            DisplayCurrentArchiveFolderName = imageCollectionContext.Name;

            _imageCollectionContext = imageCollectionContext;
            _ImageCollectionDisposer = imageCollectionContext as IDisposable;

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

        private async Task ReloadItemsAsync(IImageCollectionContext imageCollectionContext, CancellationToken ct)
        {
            var oldItemPathMap = FolderItems.Select(x => x.Path).ToHashSet();
            var newItems = await _messenger.WorkWithBusyWallAsync((ct) => imageCollectionContext.GetFolderOrArchiveFilesAsync(ct), ct).ToListAsync(ct);
            var deletedItems = Enumerable.Except(oldItemPathMap, newItems.Select(x => x.Path))
                .Where(x => oldItemPathMap.Contains(x))
                .ToHashSet();

            using (FileItemsView.DeferRefresh())
            {
                // 削除アイテム
                Debug.WriteLine($"items count : {FolderItems.Count}");
                foreach (var itemVM in FolderItems.Where(x => deletedItems.Contains(x.Path)).ToArray())
                {
                    itemVM.Dispose();
                    FolderItems.Remove(itemVM);
                }

                Debug.WriteLine($"after deleted : {FolderItems.Count}");
                // 新規アイテム
                foreach (var item in newItems.Where(x => oldItemPathMap.Contains(x.Path) is false))
                {
                    FolderItems.Add(new StorageItemViewModel(item, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _albamRepository));
                    ct.ThrowIfCancellationRequested();
                }
                Debug.WriteLine($"after added : {FolderItems.Count}");
            }

            ct.ThrowIfCancellationRequested();

            _ = Task.Run(async () =>
            {
                bool exist = await imageCollectionContext.IsExistImageFileAsync(ct);
                _scheduler.Schedule(() => HasFileItem = exist);
            }, ct);
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
                    SelectedFileSortType.Value = sortType.Value;
                }
            });

        private async Task SetSort(FileSortType fileSort, CancellationToken ct)
        {
            using (await _RefreshLock.LockAsync(ct))
            {
                SetSortAsyncUnsafe(fileSort, _currentPath);
            }
        }

        private void SetSortAsyncUnsafe(FileSortType fileSort, string path)
        {
            var sortDescriptions = ToSortDescription(fileSort);
            using (FileItemsView.DeferRefresh())
            {
                FileItemsView.SortDescriptions.Clear();
                FileItemsView.SortDescriptions.Add(new SortDescription(nameof(StorageItemViewModel.Type), SortDirection.Ascending));
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


        private RelayCommand<object> _ChangeChildFileSortCommand;
        public RelayCommand<object> ChangeChildFileSortCommand =>
            _ChangeChildFileSortCommand ??= new RelayCommand<object>(sort =>
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
                                
                SelectedChildFileSortType.Value = sortType;
                _displaySettingsByPathRepository.SetFileParentSettings(_currentPath, sortType);
            });

#endregion
    }

    public abstract class FolderItemsGroupBase
    {
        public ObservableCollection<StorageItemViewModel> Items { get; set; }
    }
    public sealed class FolderFolderItemsGroup : FolderItemsGroupBase
    {

    }

    public sealed class FileFolderItemsGroup : FolderItemsGroupBase
    {

    }

    class FolderListupPageParameter
    {
        public string Token { get; set; }
        public string Path { get; set; }
    }
    
}
