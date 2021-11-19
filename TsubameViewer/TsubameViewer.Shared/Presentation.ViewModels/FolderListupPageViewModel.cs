using Microsoft.Toolkit.Uwp.UI;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
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
using TsubameViewer.Presentation.Services.UWP;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views;
using Uno.Disposables;
using Uno.Extensions;
using Uno.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;
using Windows.UI.Xaml.Media.Animation;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Presentation.ViewModels.SourceFolders.Commands;
using System.Collections;
using System.Reactive.Concurrency;

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
            FolderItems.DisposeAll();
        }
    }


    public sealed class FolderListupPageViewModel : ViewModelBase
    {
        private bool _NowProcessing;
        public bool NowProcessing
        {
            get { return _NowProcessing; }
            set { SetProperty(ref _NowProcessing, value); }
        }

        private readonly IScheduler _scheduler;
        private readonly BookmarkManager _bookmarkManager;
        private readonly ImageCollectionManager _imageCollectionManager;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly PathReferenceCountManager _PathReferenceCountManager;
        private readonly FolderLastIntractItemManager _folderLastIntractItemManager;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;

        public SecondaryTileManager SecondaryTileManager { get; }
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

        public ReactivePropertySlim<bool> IsSortWithTitleDigitCompletion { get; }



        public ReactivePropertySlim<StorageItemViewModel> FolderLastIntractItem { get; }

        static FastAsyncLock _NavigationLock = new FastAsyncLock();

        private string _currentPath;
        private IStorageItem _currentItem;

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

        IDisposable _ImageCollectionDisposer;

        StorageItemToken _currentItemRootFolderToken;

        public string FoldersManagementPageName => nameof(Views.SourceStorageItemsPage);


        static bool _LastIsImageFileThumbnailEnabled;
        static bool _LastIsArchiveFileThumbnailEnabled;
        static bool _LastIsFolderThumbnailEnabled;

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
            BookmarkManager bookmarkManager,
            ImageCollectionManager imageCollectionManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            PathReferenceCountManager PathReferenceCountManager,
            SecondaryTileManager secondaryTileManager,
            FolderLastIntractItemManager folderLastIntractItemManager,
            ThumbnailManager thumbnailManager,
            FolderListingSettings folderListingSettings,
            DisplaySettingsByPathRepository displaySettingsByPathRepository,
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
            OpenWithExternalApplicationCommand openWithExternalApplicationCommand
            )
        {
            _scheduler = scheduler;
            _bookmarkManager = bookmarkManager;
            _imageCollectionManager = imageCollectionManager;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _PathReferenceCountManager = PathReferenceCountManager;
            SecondaryTileManager = secondaryTileManager;
            _folderLastIntractItemManager = folderLastIntractItemManager;
            _thumbnailManager = thumbnailManager;
            _folderListingSettings = folderListingSettings;
            _displaySettingsByPathRepository = displaySettingsByPathRepository;
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
            FolderItems = new ObservableCollection<StorageItemViewModel>();
            FileItemsView = new AdvancedCollectionView(FolderItems);
            FolderLastIntractItem = new ReactivePropertySlim<StorageItemViewModel>()
                .AddTo(_disposables);

            SelectedFileSortType = new ReactivePropertySlim<FileSortType>(FileSortType.UpdateTimeDescThenTitleAsc)
                .AddTo(_disposables);
            IsSortWithTitleDigitCompletion = new ReactivePropertySlim<bool>(true)
                .AddTo(_disposables);

            SelectedChildFileSortType = new ReactivePropertySlim<FileSortType?>(null)
                .AddTo(_disposables);
        }


        public override async void OnNavigatedFrom(INavigationParameters parameters)
        {
            using (await _NavigationLock.LockAsync(default))
            {
                _leavePageCancellationTokenSource?.Cancel();
                _leavePageCancellationTokenSource?.Dispose();
                _leavePageCancellationTokenSource = null;

                FolderItems.AsParallel().ForEach(x => x.Dispose());

                _LastIsImageFileThumbnailEnabled = _folderListingSettings.IsImageFileThumbnailEnabled;
                _LastIsArchiveFileThumbnailEnabled = _folderListingSettings.IsArchiveFileThumbnailEnabled;
                _LastIsFolderThumbnailEnabled = _folderListingSettings.IsFolderThumbnailEnabled;

                if (_currentPath != null && parameters.TryGetValue(PageNavigationConstants.Path, out string path))
                {
                    _folderLastIntractItemManager.SetLastIntractItemName(_currentPath, Uri.UnescapeDataString(path));
                }

                FolderItems.Clear();

                _ImageCollectionDisposer?.Dispose();
                _ImageCollectionDisposer = null;

                _navigationDisposables?.Dispose();
                _navigationDisposables = null;
                base.OnNavigatedFrom(parameters);
            }
        }

        public override void OnNavigatingTo(INavigationParameters parameters)
        {
            PrimaryWindowCoreLayout.SetCurrentNavigationParameters(parameters);

            base.OnNavigatingTo(parameters);
        }        

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            NowProcessing = true;
            try
            {
                var mode = parameters.GetNavigationMode();
                if (mode == NavigationMode.Refresh)
                {
                    parameters = PrimaryWindowCoreLayout.GetCurrentNavigationParameter();
                }

                _leavePageCancellationTokenSource = new CancellationTokenSource();

                _currentArchiveFolderName = parameters.TryGetValue(PageNavigationConstants.ArchiveFolderName, out string archiveFolderName)
                    ? Uri.UnescapeDataString(archiveFolderName)
                    : null
                    ;

                if (mode == NavigationMode.New
                    || mode == NavigationMode.Forward
                    || mode == NavigationMode.Back
                    || mode == NavigationMode.Refresh
                    )
                {
                    HasFileItem = false;

                    using (await _NavigationLock.LockAsync(default))
                    {
                        if (parameters.TryGetValue(PageNavigationConstants.Path, out string path))
                        {
                            var unescapedPath = Uri.UnescapeDataString(path);
                            _currentPath = unescapedPath;
                            _currentItem = null;
                           
                            // PathReferenceCountManagerへの登録が遅延する可能性がある
                            string token = null;
                            foreach (var _ in Enumerable.Repeat(0, 10))
                            {
                                token = _PathReferenceCountManager.GetToken(_currentPath);
                                if (token != null)
                                {
                                    break;
                                }
                                await Task.Delay(100);
                            }

                            if (token == null)
                            {
                                throw new Exception();
                            }

                            foreach (var tempToken in _PathReferenceCountManager.GetTokens(_currentPath))
                            {
                                try
                                {
                                    _currentItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(tempToken, _currentPath);
                                    token = tempToken;
                                }
                                catch
                                {
                                    _PathReferenceCountManager.Remove(tempToken);
                                }
                            }

                            _currentItemRootFolderToken = new StorageItemToken(_currentPath, token);

                            var currentPathItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(token, _currentPath);
                            _currentItem = currentPathItem;
                            DisplayCurrentPath = _currentItem.Path;

                            var settingPath = _currentPath;
                            var settings = _displaySettingsByPathRepository.GetFolderAndArchiveSettings(settingPath);
                            if (settings != null)
                            {
                                SelectedFileSortType.Value = settings.Sort;
                                IsSortWithTitleDigitCompletion.Value = settings.IsTitleDigitInterpolation;
                                SetSortAsyncUnsafe(SelectedFileSortType.Value, IsSortWithTitleDigitCompletion.Value);
                            }
                            else
                            {
                                if (_currentItem is StorageFolder)
                                {
                                    SelectedFileSortType.Value = FileSortType.TitleAscending;
                                    IsSortWithTitleDigitCompletion.Value = false;
                                    SetSortAsyncUnsafe(SelectedFileSortType.Value, IsSortWithTitleDigitCompletion.Value);
                                }
                                else if (_currentItem is StorageFile file && file.IsSupportedMangaFile())
                                {
                                    SelectedFileSortType.Value = FileSortType.UpdateTimeDescThenTitleAsc;
                                    IsSortWithTitleDigitCompletion.Value = false;
                                    SetSortAsyncUnsafe(SelectedFileSortType.Value, IsSortWithTitleDigitCompletion.Value);
                                }
                            }

                            SelectedChildFileSortType.Value = _displaySettingsByPathRepository.GetFileParentSettings(_currentPath);
                        }

                        await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                    }
                }
                else if (!_isCompleteEnumeration
                    || _LastIsImageFileThumbnailEnabled != _folderListingSettings.IsImageFileThumbnailEnabled
                    || _LastIsArchiveFileThumbnailEnabled != _folderListingSettings.IsArchiveFileThumbnailEnabled
                    || _LastIsFolderThumbnailEnabled != _folderListingSettings.IsFolderThumbnailEnabled
                    )
                {
                    await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                }
                else
                {
                    // 前回読み込みキャンセルしていたものを改めて読み込むように
                    FolderItems.ForEach(x => x.RestoreThumbnailLoadingTask());

                    // 最後に読んだ位置を更新
                    FolderItems.ForEach(x => x.UpdateLastReadPosition());

                    RaisePropertyChanged(nameof(FolderItems));
                }
               
                if (mode != NavigationMode.New)
                {
                    var lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(_currentItem.Path);
                    if (lastIntaractItem != null)
                    {
                        StorageItemViewModel lastIntractItemVM = null;
                        foreach (var item in FolderItems)
                        {
                            if (item.Name == lastIntaractItem)
                            {
                                lastIntractItemVM = item;
                                break;
                            }
                        }

                        lastIntractItemVM?.ThumbnailChanged();
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

            _navigationDisposables = new CompositeDisposable();
            Observable.CombineLatest(
                SelectedFileSortType,
                IsSortWithTitleDigitCompletion,
                (sortType, withInterpolation) => (sortType, withInterpolation)
                )
                .Pairwise()
                .Where(x => x.NewItem != x.OldItem)
                .Select(x => x.NewItem)
                .Subscribe(x => _ = SetSort(x.sortType, x.withInterpolation, _leavePageCancellationTokenSource?.Token ?? default))
                .AddTo(_navigationDisposables);

            await base.OnNavigatedToAsync(parameters);
        }


        #region Refresh Item

        static FastAsyncLock _RefreshLock = new FastAsyncLock();
        private async Task RefreshFolderItems(CancellationToken ct)
        {
            using var lockObject = await _RefreshLock.LockAsync(ct);

            FolderItems.Clear();
            DisplayCurrentArchiveFolderName = null;
            CurrentFolderItem = null;

            _isCompleteEnumeration = false;
            IImageCollectionContext imageCollectionContext = null;
            try
            {
                if (_currentItem is StorageFolder folder)
                {
                    Debug.WriteLine(folder.Path);
                    imageCollectionContext = await _imageCollectionManager.GetFolderImageCollectionContextAsync(folder, ct);
                    CurrentFolderItem = new StorageItemViewModel(new StorageItemImageSource(_currentItem, _thumbnailManager), _currentItemRootFolderToken, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager);
                }
                else if (_currentItem is StorageFile file)
                {
                    Debug.WriteLine(file.Path);
                    if (file.IsSupportedImageFile())
                    {
                        try
                        {
                            var parentFolder = await file.GetParentAsync();
                            imageCollectionContext = await _imageCollectionManager.GetFolderImageCollectionContextAsync(parentFolder, ct);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            var parentItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(_currentItemRootFolderToken.TokenString, Path.GetDirectoryName(_currentPath));
                            if (parentItem is StorageFolder parentFolder)
                            {
                                imageCollectionContext = await _imageCollectionManager.GetFolderImageCollectionContextAsync(parentFolder, ct);
                            }
                        }

                        CurrentFolderItem = new StorageItemViewModel(new StorageItemImageSource(_currentItem, _thumbnailManager), _currentItemRootFolderToken, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager);
                    }
                    else if (file.IsSupportedMangaFile())
                    {
                        // string.Emptyを渡すことでルートフォルダのフォルダ取得を行える
                        imageCollectionContext = await _imageCollectionManager.GetArchiveImageCollectionContextAsync(file, _currentArchiveFolderName ?? string.Empty, ct);
                        DisplayCurrentArchiveFolderName = _currentArchiveFolderName;
                        if (_currentArchiveFolderName == null)
                        {
                            CurrentFolderItem = new StorageItemViewModel(new StorageItemImageSource(_currentItem, _thumbnailManager), _currentItemRootFolderToken, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager);
                        }
                        else if (imageCollectionContext is ArchiveImageCollectionContext aic)
                        {
                            CurrentFolderItem = new StorageItemViewModel(new ArchiveDirectoryImageSource(aic.ArchiveImageCollection, aic.ArchiveDirectoryToken, _thumbnailManager), _currentItemRootFolderToken, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager);
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }

                _isCompleteEnumeration = true;
            }
            catch (OperationCanceledException)
            {

            }

            if (imageCollectionContext == null) { return; }

            _ImageCollectionDisposer = imageCollectionContext as IDisposable;

            var items = await imageCollectionContext.GetFolderOrArchiveFilesAsync(ct);
            using (FileItemsView.DeferRefresh())
            {
                foreach (var item in items)
                {
                    FolderItems.Add(new StorageItemViewModel(item, _currentItemRootFolderToken, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
                }
            }

            ct.ThrowIfCancellationRequested();

            _ = Task.Run(async () => 
            {
                bool exist = await imageCollectionContext.IsExistImageFileAsync(ct);
                _scheduler.Schedule(() => HasFileItem = exist);

                foreach (var item in items)
                {
                    _PathReferenceCountManager.Upsert(item.Path, _currentItemRootFolderToken.TokenString);
                }
            }, ct);

            
        }

#endregion



#region FileSortType


        public static IEnumerable<SortDescription> ToSortDescription(FileSortType fileSortType, bool withTitleDigitCompletion)
        {
            IComparer TitleDigitCompletionComparer = withTitleDigitCompletion ? Sorting.TitleDigitCompletionComparer.Default : null;
            return fileSortType switch
            {
                FileSortType.UpdateTimeDescThenTitleAsc => new[] { new SortDescription(nameof(StorageItemViewModel.DateCreated), SortDirection.Descending), new SortDescription(nameof(StorageItemViewModel.Name), SortDirection.Ascending, TitleDigitCompletionComparer) },
                FileSortType.TitleAscending => new[] { new SortDescription(nameof(StorageItemViewModel.Name), SortDirection.Ascending, TitleDigitCompletionComparer) },
                FileSortType.TitleDecending => new[] { new SortDescription(nameof(StorageItemViewModel.Name), SortDirection.Descending, TitleDigitCompletionComparer) },
                FileSortType.UpdateTimeAscending => new[] { new SortDescription(nameof(StorageItemViewModel.DateCreated), SortDirection.Ascending) },
                FileSortType.UpdateTimeDecending => new[] { new SortDescription(nameof(StorageItemViewModel.DateCreated), SortDirection.Descending) },
                _ => throw new NotSupportedException(),
            };
        }

        private DelegateCommand<object> _ChangeFileSortCommand;
        public DelegateCommand<object> ChangeFileSortCommand =>
            _ChangeFileSortCommand ??= new DelegateCommand<object>(async sort =>
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

        private async Task SetSort(FileSortType fileSort, bool withNameInterpolation, CancellationToken ct)
        {
            using (await _RefreshLock.LockAsync(ct))
            {
                SetSortAsyncUnsafe(fileSort, withNameInterpolation);
            }
        }

        private void SetSortAsyncUnsafe(FileSortType fileSort, bool withNameInterpolation)
        {
            var sortDescriptions = ToSortDescription(fileSort, withNameInterpolation);
            using (FileItemsView.DeferRefresh())
            {
                FileItemsView.SortDescriptions.Clear();
                FileItemsView.SortDescriptions.Add(new SortDescription(nameof(StorageItemViewModel.Type), SortDirection.Ascending));
                foreach (var sort in sortDescriptions)
                {
                    FileItemsView.SortDescriptions.Add(sort);
                }
            }

            FolderLastIntractItem.Value = FileItemsView.FirstOrDefault() as StorageItemViewModel;
            FolderLastIntractItem.Value = null;
            _displaySettingsByPathRepository.SetFolderAndArchiveSettings(
                _currentPath,
                fileSort,
                withNameInterpolation
                );
        }


        private DelegateCommand<object> _ChangeChildFileSortCommand;
        public DelegateCommand<object> ChangeChildFileSortCommand =>
            _ChangeChildFileSortCommand ??= new DelegateCommand<object>(sort =>
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
