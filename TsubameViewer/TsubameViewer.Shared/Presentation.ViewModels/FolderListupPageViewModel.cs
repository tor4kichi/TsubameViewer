﻿using Microsoft.Toolkit.Uwp.UI;
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
using TsubameViewer.Models.Domain.Bookmark;
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

namespace TsubameViewer.Presentation.ViewModels
{
    using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;


    public class CachedFolderListupItems
    {
        public ObservableCollection<StorageItemViewModel> FolderItems { get; set; }
        public ObservableCollection<StorageItemViewModel> ArchiveFileItems { get; set; }
        public ObservableCollection<StorageItemViewModel> EBookFileItems { get; set; }
        public ObservableCollection<StorageItemViewModel> ImageFileItems { get; set; }

        public int GetTotalCount()
        {
            return FolderItems.Count
                + ArchiveFileItems.Count
                + EBookFileItems.Count
                + ImageFileItems.Count
                ;
        }

        public void DisposeItems()
        {
            FolderItems.DisposeAll();
            ArchiveFileItems.DisposeAll();
            ImageFileItems.DisposeAll();
            EBookFileItems.DisposeAll();
        }
    }


    public sealed class FolderListupPageViewModel : ViewModelBase
    {
        const int FolderListupItemsCacheCount = 200;
        static List<string> _CacheFolderListupItemsOrder = new List<string>();

        static Dictionary<string, CachedFolderListupItems> _CachedFolderListupItems = new Dictionary<string, CachedFolderListupItems>();


        private bool _NowProcessing;
        public bool NowProcessing
        {
            get { return _NowProcessing; }
            set { SetProperty(ref _NowProcessing, value); }
        }

        private readonly BookmarkManager _bookmarkManager;
        private readonly ImageCollectionManager _imageCollectionManager;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly PathReferenceCountManager _PathReferenceCountManager;
        private readonly FolderLastIntractItemManager _folderLastIntractItemManager;
        private readonly FolderListingSettings _folderListingSettings;

        public SecondaryTileManager SecondaryTileManager { get; }
        public OpenPageCommand OpenPageCommand { get; }
        public OpenFolderItemCommand OpenFolderItemCommand { get; }
        public OpenImageViewerCommand OpenImageViewerCommand { get; }
        public OpenFolderListupCommand OpenFolderListupCommand { get; }
        public OpenWithExplorerCommand OpenWithExplorerCommand { get; }
        public SecondaryTileAddCommand SecondaryTileAddCommand { get; }
        public SecondaryTileRemoveCommand SecondaryTileRemoveCommand { get; }
        public ObservableCollection<StorageItemViewModel> FolderItems { get; private set; }
        public ObservableCollection<StorageItemViewModel> ArchiveFileItems { get; private set; }
        public ObservableCollection<StorageItemViewModel> EBookFileItems { get; private set; }
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


        private FolderItemsGroupBase[] _groups;
        public FolderItemsGroupBase[] Groups
        {
            get { return _groups; }
            set { SetProperty(ref _groups, value); }
        }

        public ReactivePropertySlim<FileSortType> SelectedFileSortType { get; }

        public ReactivePropertySlim<StorageItemViewModel> FolderLastIntractItem { get; }
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

        public string FoldersManagementPageName => nameof(Views.SourceStorageItemsPage);


        static bool _LastIsImageFileThumbnailEnabled;
        static bool _LastIsArchiveFileThumbnailEnabled;
        static bool _LastIsFolderThumbnailEnabled;

        public FolderListupPageViewModel(
            BookmarkManager bookmarkManager,
            ImageCollectionManager imageCollectionManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
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
            SecondaryTileRemoveCommand secondaryTileRemoveCommand
            )
        {
            _bookmarkManager = bookmarkManager;
            _imageCollectionManager = imageCollectionManager;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
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
            FolderItems = new ObservableCollection<StorageItemViewModel>();
            ArchiveFileItems = new ObservableCollection<StorageItemViewModel>();
            EBookFileItems = new ObservableCollection<StorageItemViewModel>();
            ImageFileItems = new ObservableCollection<StorageItemViewModel>();

            FileItemsView = new AdvancedCollectionView(ImageFileItems);
            SelectedFileSortType = new ReactivePropertySlim<FileSortType>(FileSortType.TitleAscending);

            


            FileDisplayMode = _folderListingSettings.ToReactivePropertyAsSynchronized(x => x.FileDisplayMode);
            FolderLastIntractItem = new ReactivePropertySlim<StorageItemViewModel>();
            ImageLastIntractItem = new ReactivePropertySlim<int>();
            /*
            _currentQueryOptions = Observable.CombineLatest(
                SelectedFolderViewFirstSort,
                (queryType, sort) => (queryType, sort)
                )
                .Select(_ =>
                {
                    var options = new QueryOptions();
                    options.FolderDepth = FolderDepth.Shallow;
                    options.SetPropertyPrefetch(Windows.Storage.FileProperties.PropertyPrefetchOptions.ImageProperties, Enumerable.Empty<string>());
                    return options;
                })
                .ToReadOnlyReactivePropertySlim();
                */

        }


        public override async void OnNavigatedFrom(INavigationParameters parameters)
        {
            using (await _NavigationLock.LockAsync(default))
            {
                _leavePageCancellationTokenSource?.Cancel();
                _leavePageCancellationTokenSource?.Dispose();
                _leavePageCancellationTokenSource = null;

                ImageFileItems.Reverse().ForEach(x => x.StopImageLoading());
                ArchiveFileItems.Reverse().ForEach(x => x.StopImageLoading());
                EBookFileItems.Reverse().ForEach(x => x.StopImageLoading());
                FolderItems.Reverse().ForEach(x => x.StopImageLoading());

                _LastIsImageFileThumbnailEnabled = _folderListingSettings.IsImageFileThumbnailEnabled;
                _LastIsArchiveFileThumbnailEnabled = _folderListingSettings.IsArchiveFileThumbnailEnabled;
                _LastIsFolderThumbnailEnabled = _folderListingSettings.IsFolderThumbnailEnabled;

                if (_currentPath != null && parameters.TryGetValue(PageNavigationConstants.Path, out string path))
                {
                    _folderLastIntractItemManager.SetLastIntractItemName(_currentPath, Uri.UnescapeDataString(path));
                }


                // 
                _CachedFolderListupItems.Add(_currentPath, new CachedFolderListupItems() 
                {
                    ArchiveFileItems = ArchiveFileItems,
                    EBookFileItems = EBookFileItems,
                    FolderItems = FolderItems,
                    ImageFileItems = ImageFileItems,
                });

                _CacheFolderListupItemsOrder.Remove(_currentPath);
                _CacheFolderListupItemsOrder.Add(_currentPath);

                while (_CachedFolderListupItems.Select(x => x.Value.GetTotalCount()).Sum() > FolderListupItemsCacheCount)
                {
                    var item = _CacheFolderListupItemsOrder.First();
                    if (_CachedFolderListupItems.Remove(item, out var cachedItems))
                    {
                        cachedItems.DisposeItems();
                    }
                    _CacheFolderListupItemsOrder.Remove(item);
                }

                Groups = null;

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
                            foreach (var _ in Enumerable.Repeat(0, 100))
                            {
                                token = _PathReferenceCountManager.GetToken(_currentPath);
                                if (token != null)
                                {
                                    break;
                                }
                                await Task.Delay(100);
                            }
                            var currentPathItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(token, _currentPath);
                            _currentItem = currentPathItem;
                            DisplayCurrentPath = _currentItem.Path;
                        }

                        if (_CachedFolderListupItems.Remove(_currentPath, out var cachedItems))
                        {
                            FolderItems = cachedItems.FolderItems;
                            ArchiveFileItems = cachedItems.ArchiveFileItems;
                            EBookFileItems = cachedItems.EBookFileItems;
                            ImageFileItems = cachedItems.ImageFileItems;


                            // 最後に読んだ位置を更新
                            ImageFileItems.ForEach(x => x.UpdateLastReadPosition());
                            ArchiveFileItems.ForEach(x => x.UpdateLastReadPosition());
                            EBookFileItems.ForEach(x => x.UpdateLastReadPosition());
                            FolderItems.ForEach(x => x.UpdateLastReadPosition());

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
                    || _LastIsArchiveFileThumbnailEnabled != _folderListingSettings.IsArchiveFileThumbnailEnabled
                    || _LastIsFolderThumbnailEnabled != _folderListingSettings.IsFolderThumbnailEnabled
                    )
                {
                    if (_CachedFolderListupItems.Remove(_currentPath, out var cachedItems))
                    {
                        FolderItems = cachedItems.FolderItems;
                        ArchiveFileItems = cachedItems.ArchiveFileItems;
                        EBookFileItems = cachedItems.EBookFileItems;
                        ImageFileItems = cachedItems.ImageFileItems;

                        // 最後に読んだ位置を更新
                        ImageFileItems.ForEach(x => x.UpdateLastReadPosition());
                        ArchiveFileItems.ForEach(x => x.UpdateLastReadPosition());
                        EBookFileItems.ForEach(x => x.UpdateLastReadPosition());
                        FolderItems.ForEach(x => x.UpdateLastReadPosition());

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
                    ArchiveFileItems.ForEach(x => x.RestoreThumbnailLoadingTask());
                    EBookFileItems.ForEach(x => x.RestoreThumbnailLoadingTask());
                    FolderItems.ForEach(x => x.RestoreThumbnailLoadingTask());

                    // 最後に読んだ位置を更新
                    ImageFileItems.ForEach(x => x.UpdateLastReadPosition());
                    ArchiveFileItems.ForEach(x => x.UpdateLastReadPosition());
                    EBookFileItems.ForEach(x => x.UpdateLastReadPosition());
                    FolderItems.ForEach(x => x.UpdateLastReadPosition());
                }

                

                Groups = new FolderItemsGroupBase[]
                {
                    new FolderFolderItemsGroup()
                    {
                        Items = FolderItems,
                    },
                    new FileFolderItemsGroup()
                    {
                        Items = ArchiveFileItems
                    },
                    new FileFolderItemsGroup()
                    {
                        Items = EBookFileItems
                    },
                };

                RaisePropertyChanged(nameof(ImageFileItems));

                if (mode != NavigationMode.New)
                {
                    var lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(_currentItem.Path);
                    if (lastIntaractItem != null)
                    {
                        StorageItemViewModel lastIntractItemVM = null;
                        foreach (var item in new[] { FolderItems, ArchiveFileItems, EBookFileItems, }.SelectMany(x => x))
                        {
                            if (item.Name == lastIntaractItem)
                            {
                                lastIntractItemVM = item;
                                break;
                            }
                        }

                        FolderLastIntractItem.Value = lastIntractItemVM;

                        if (lastIntractItemVM == null)
                        {
                            var item = ImageFileItems.FirstOrDefault(x => x.Name == lastIntaractItem);
                            ImageLastIntractItem.Value = ImageFileItems.IndexOf(item);
                        }
                    }
                    else
                    {
                        FolderLastIntractItem.Value = null;
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

            FolderItems = new ObservableCollection<StorageItemViewModel>();
            ArchiveFileItems = new ObservableCollection<StorageItemViewModel>();
            EBookFileItems = new ObservableCollection<StorageItemViewModel>();
            ImageFileItems = new ObservableCollection<StorageItemViewModel>();

            _isCompleteEnumeration = false;
            try
            {
                if (_currentItem is StorageFolder folder)
                {
                    Debug.WriteLine(folder.Path);
                    await RefreshFolderItems(folder, ct);
                }
                else if (_currentItem is StorageFile file)
                {
                    Debug.WriteLine(file.Path);
                    await RefreshFolderItems(file, ct);
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
        }

        
        private async ValueTask RefreshFolderItems(IStorageItem storageItem, CancellationToken ct)
        {
            FolderItems.Clear();
            ArchiveFileItems.Clear();
            ImageFileItems.Clear();
            EBookFileItems.Clear();
            var result = await _imageCollectionManager.GetImageSourcesForFolderItemsListingAsync(storageItem, ct);

            if (result.Images?.Any() != true)
            {
                return;
            }

            List<StorageItemViewModel> unsortedFileItems = new List<StorageItemViewModel>();
            foreach (var folderItem in result.Images)
            {
                ct.ThrowIfCancellationRequested();
                var item = new StorageItemViewModel(folderItem, null, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager);
                if (item.Type == StorageItemTypes.Folder)
                {
                    FolderItems.Add(item);
                }
                else if (item.Type == StorageItemTypes.Image)
                {
                    unsortedFileItems.Add(item);
                }
                else if (item.Type == StorageItemTypes.Archive)
                {
                    ArchiveFileItems.Add(item);
                }
                else if (item.Type == StorageItemTypes.EBook)
                {
                    EBookFileItems.Add(item);
                }
            }

            _FileItemsView = new AdvancedCollectionView(ImageFileItems);
            using (FileItemsView.DeferRefresh())
            {
                var sortDescription = ToSortDescription(SelectedFileSortType.Value);

                FileItemsView.SortDescriptions.Clear();
                FileItemsView.SortDescriptions.Add(sortDescription);

                ImageFileItems.AddRange(unsortedFileItems);
            }

            RaisePropertyChanged(nameof(FileItemsView));

            HasFileItem = ImageFileItems.Any();
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
