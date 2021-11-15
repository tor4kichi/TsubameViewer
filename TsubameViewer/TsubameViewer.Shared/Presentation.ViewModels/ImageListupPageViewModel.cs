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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Domain.ReadingFeature;
using TsubameViewer.Models.Domain.RestoreNavigation;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.Services.UWP;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.ViewModels.SourceFolders.Commands;
using TsubameViewer.Presentation.Views;
using Uno.Extensions;
using Uno.Threading;
using Windows.Storage;
using static TsubameViewer.Models.Domain.ImageViewer.ImageCollectionManager;
using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class ImageListupPageViewModel : ViewModelBase
    {
        const int FolderListupItemsCacheCount = 200;
        static List<string> _CacheFolderListupItemsOrder = new List<string>();

        static Dictionary<string, CachedFolderListupItems> _CachedFolderListupItems = new Dictionary<string, CachedFolderListupItems>();


        private readonly BookmarkManager _bookmarkManager;
        private readonly ImageCollectionManager _imageCollectionManager;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly PathReferenceCountManager _PathReferenceCountManager;
        private readonly FolderLastIntractItemManager _folderLastIntractItemManager;
        private readonly FolderListingSettings _folderListingSettings;

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
        public ObservableCollection<StorageItemViewModel> ImageFileItems { get; private set; }



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




        public ReactivePropertySlim<FileSortType> SelectedFileSortType { get; }

        public ReactivePropertySlim<int> ImageLastIntractItem { get; }

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

        StorageItemToken _currentItemRootFolderToken;

        private string _currentArchiveFolderName;

        private string _DisplayCurrentArchiveFolderName;
        public string DisplayCurrentArchiveFolderName
        {
            get { return _DisplayCurrentArchiveFolderName; }
            private set { SetProperty(ref _DisplayCurrentArchiveFolderName, value); }
        }


        private bool _IsRestrictImageFileThumbnail;
        public bool IsRestrictImageFileThumbnail
        {
            get { return _IsRestrictImageFileThumbnail; }
            set { SetProperty(ref _IsRestrictImageFileThumbnail, value); }
        }
        public ReactiveProperty<FileDisplayMode> FileDisplayMode { get; }
        public FileDisplayMode[] FileDisplayModeItems { get; } = new FileDisplayMode[]
        {
            Models.Domain.FolderItemListing.FileDisplayMode.Large,
            Models.Domain.FolderItemListing.FileDisplayMode.Midium,
            Models.Domain.FolderItemListing.FileDisplayMode.Small,
            Models.Domain.FolderItemListing.FileDisplayMode.Line,
        };

        static bool _LastIsImageFileThumbnailEnabled;


        public string FoldersManagementPageName => nameof(Views.SourceStorageItemsPage);

        IDisposable _ImageCollectionDisposer;

        public ImageListupPageViewModel(
            BookmarkManager bookmarkManager,
            ImageCollectionManager imageCollectionManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            ThumbnailManager thumbnailManager,
            PathReferenceCountManager PathReferenceCountManager,
            SecondaryTileManager secondaryTileManager,
            FolderLastIntractItemManager folderLastIntractItemManager,
            FolderListingSettings folderListingSettings,
            OpenPageCommand openPageCommand,
            OpenFolderItemCommand openFolderItemCommand,
            OpenImageViewerCommand openImageViewerCommand,
            OpenFolderListupCommand openFolderListupCommand,
            OpenWithExplorerCommand openWithExplorerCommand,
            SecondaryTileAddCommand secondaryTileAddCommand,
            SecondaryTileRemoveCommand secondaryTileRemoveCommand,
            ChangeStorageItemThumbnailImageCommand changeStorageItemThumbnailImageCommand,
            OpenWithExternalApplicationCommand openWithExternalApplicationCommand
            )
        {
            _bookmarkManager = bookmarkManager;
            _imageCollectionManager = imageCollectionManager;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _thumbnailManager = thumbnailManager;
            _PathReferenceCountManager = PathReferenceCountManager;
            SecondaryTileManager = secondaryTileManager;
            _folderLastIntractItemManager = folderLastIntractItemManager;
            _folderListingSettings = folderListingSettings;
            OpenPageCommand = openPageCommand;
            OpenFolderItemCommand = openFolderItemCommand;
            OpenImageViewerCommand = openImageViewerCommand;
            OpenFolderListupCommand = openFolderListupCommand;
            OpenWithExplorerCommand = openWithExplorerCommand;
            SecondaryTileAddCommand = secondaryTileAddCommand;
            SecondaryTileRemoveCommand = secondaryTileRemoveCommand;
            ChangeStorageItemThumbnailImageCommand = changeStorageItemThumbnailImageCommand;
            OpenWithExternalApplicationCommand = openWithExternalApplicationCommand;
            ImageFileItems = new ObservableCollection<StorageItemViewModel>();

            FileItemsView = new AdvancedCollectionView(ImageFileItems);
            SelectedFileSortType = new ReactivePropertySlim<FileSortType>(FileSortType.TitleAscending);

            FileDisplayMode = _folderListingSettings.ToReactivePropertyAsSynchronized(x => x.FileDisplayMode);
            ImageLastIntractItem = new ReactivePropertySlim<int>();
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _ImageCollectionDisposer?.Dispose();
            _ImageCollectionDisposer = null;

            base.OnNavigatedFrom(parameters);
        }

        public override void OnNavigatingTo(INavigationParameters parameters)
        {
            Views.PrimaryWindowCoreLayout.SetCurrentNavigationParameters(parameters);

            base.OnNavigatingTo(parameters);
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            IsRestrictImageFileThumbnail = !_folderListingSettings.IsImageFileThumbnailEnabled;

            NowProcessing = true;
            try
            {
                // Note: ファイル表示用のItemsRepeaterのItemTemplateが
                // VisualStateによって変更されるのを待つ
                await Task.Delay(50);

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
                        }

                        if (_currentPath != null && _CachedFolderListupItems.Remove(_currentPath, out var cachedItems))
                        {
                            ImageFileItems = cachedItems.FolderItems;


                            // 最後に読んだ位置を更新
                            ImageFileItems.ForEach(x => x.UpdateLastReadPosition());

                            _FileItemsView = new AdvancedCollectionView(ImageFileItems);
                            using (FileItemsView.DeferRefresh())
                            {
                                var sortDescription = ToSortDescription(SelectedFileSortType.Value);

                                FileItemsView.SortDescriptions.Clear();
                                FileItemsView.SortDescriptions.Add(sortDescription);
                            }
                            RaisePropertyChanged(nameof(FileItemsView));
                        }
                        else
                        {
                            await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                        }

                        HasFileItem = ImageFileItems.Any();
                    }
                }
                else if (!_isCompleteEnumeration
                    || _LastIsImageFileThumbnailEnabled != _folderListingSettings.IsImageFileThumbnailEnabled
                    )
                {
                    if (_CachedFolderListupItems.Remove(_currentPath, out var cachedItems))
                    {
                        ImageFileItems = cachedItems.FolderItems;

                        // 最後に読んだ位置を更新
                        ImageFileItems.ForEach(x => x.UpdateLastReadPosition());

                        _FileItemsView = new AdvancedCollectionView(ImageFileItems);
                        using (FileItemsView.DeferRefresh())
                        {
                            var sortDescription = ToSortDescription(SelectedFileSortType.Value);

                            FileItemsView.SortDescriptions.Clear();
                            FileItemsView.SortDescriptions.Add(sortDescription);
                        }
                        RaisePropertyChanged(nameof(FileItemsView));
                    }
                    else
                    {
                        await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                    }
                }
                else 
                {
                    // 前回読み込みキャンセルしていたものを改めて読み込むように
                    ImageFileItems.ForEach(x => x.RestoreThumbnailLoadingTask());

                    // 最後に読んだ位置を更新
                    ImageFileItems.ForEach(x => x.UpdateLastReadPosition());
                }

                RaisePropertyChanged(nameof(ImageFileItems));

                if (mode != NavigationMode.New)
                {
                    var lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(_currentItem.Path);
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

            await base.OnNavigatedToAsync(parameters);
        }

        #region Refresh Item

        static FastAsyncLock _RefreshLock = new FastAsyncLock();
        private async Task RefreshFolderItems(CancellationToken ct)
        {
            using var _ = await _RefreshLock.LockAsync(ct);

            _ImageCollectionDisposer?.Dispose();
            _ImageCollectionDisposer = null;
            ImageFileItems.Clear();

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
                        imageCollectionContext = await _imageCollectionManager.GetArchiveImageCollectionContextAsync(file, _currentArchiveFolderName ?? String.Empty, ct);
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
            foreach (var folderItem in await imageCollectionContext.GetImageFilesAsync(ct))
            {
                _PathReferenceCountManager.Upsert(folderItem.StorageItem.Path, _currentItemRootFolderToken.TokenString);
                ct.ThrowIfCancellationRequested();
                var item = new StorageItemViewModel(folderItem, _currentItemRootFolderToken, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager);
                if (item.Type == StorageItemTypes.Image)
                {
                    ImageFileItems.Add(item);
                }
            }

            HasFileItem = ImageFileItems.Any();
            HasFolderOrBookItem = await imageCollectionContext.IsExistFolderOrArchiveFileAsync(ct);
        }

        #endregion

        #region FileSortType


        public static SortDescription ToSortDescription(FileSortType fileSortType)
        {
            return fileSortType switch
            {
                FileSortType.TitleAscending => new SortDescription(nameof(StorageItemViewModel.Name), SortDirection.Ascending),
                FileSortType.TitleDecending => new SortDescription(nameof(StorageItemViewModel.Name), SortDirection.Descending),
                FileSortType.UpdateTimeAscending => new SortDescription(nameof(StorageItemViewModel.DateCreated), SortDirection.Ascending),
                FileSortType.UpdateTimeDecending => new SortDescription(nameof(StorageItemViewModel.DateCreated), SortDirection.Descending),
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

                    using (await _RefreshLock.LockAsync(_leavePageCancellationTokenSource.Token))
                    {
                        if (ImageFileItems.Any())
                        {
                            using (FileItemsView.DeferRefresh())
                            {
                                var sortDescription = ToSortDescription(SelectedFileSortType.Value);

                                FileItemsView.SortDescriptions.Clear();
                                FileItemsView.SortDescriptions.Add(sortDescription);
                            }
                        }

                        RaisePropertyChanged(nameof(ImageFileItems));
                    }
                }
            });


        #endregion
    }
}
