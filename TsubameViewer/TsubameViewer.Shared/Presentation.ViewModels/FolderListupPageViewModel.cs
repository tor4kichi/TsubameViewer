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
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
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

    

    public sealed class FolderListupPageViewModel : ViewModelBase
    {
        // Note: FolderListupPage内でフォルダ階層のコントロールをしている
        // フォルダ階層の移動はNavigationServiceを通さずにページ内で完結してる
        // 

        private bool _NowProcessing;
        public bool NowProcessing
        {
            get { return _NowProcessing; }
            set { SetProperty(ref _NowProcessing, value); }
        }

        DelegateCommand<object> _OpenFolderItemCommand;
        public DelegateCommand<object> OpenFolderItemCommand =>
            _OpenFolderItemCommand ??= new DelegateCommand<object>(async (item) =>
            {
                if (item is StorageItemViewModel itemVM)
                {
                    if (itemVM.Type == StorageItemTypes.Image || itemVM.Type == StorageItemTypes.Archive)
                    {
                        var parameters = await StorageItemViewModel.CreatePageParameterAsync(itemVM);
                        var result = await _navigationService.NavigateAsync(nameof(Views.ImageViewerPage), parameters, new DrillInNavigationTransitionInfo());
                    }
                    else if (itemVM.Type == StorageItemTypes.Folder)
                    {
                        NowProcessing = true;
                        try
                        {
                            await PushCurrentFolderNavigationInfoAndSetNewFolder(itemVM);
                        }
                        finally
                        {
                            NowProcessing = false;
                        }
                    }
                }
            });

        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderListingSettings _folderListingSettings;
        public OpenPageCommand OpenPageCommand { get; }



        public ObservableCollection<StorageItemViewModel> FolderItems { get; }
        public ObservableCollection<StorageItemViewModel> FileItems { get; }

        public AdvancedCollectionView FileItemsView { get; }

        private bool _HasFileItem;
        public bool HasFileItem
        {
            get { return _HasFileItem; }
            set { SetProperty(ref _HasFileItem, value); }
        }


        public FolderItemsGroupBase[] Groups { get; }

        public ReactivePropertySlim<FileSortType> SelectedFileSortType { get; }

        static FastAsyncLock _NavigationLock = new FastAsyncLock();

        IReadOnlyReactiveProperty<QueryOptions> _currentQueryOptions;

        private string _currentToken;
        private StorageFolder _tokenGettingFolder;

        private string _currentPath;
        private StorageFolder _currentFolder;

        private CancellationTokenSource _leavePageCancellationTokenSource;

        bool _isCompleteEnumeration = false;

        private string _DisplayCurrentPath;
        public string DisplayCurrentPath
        {
            get { return _DisplayCurrentPath; }
            set { SetProperty(ref _DisplayCurrentPath, value); }
        }

        public ReactiveProperty<FileDisplayMode> FileDisplayMode { get; }
        public FileDisplayMode[] FileDisplayModeItems { get; } = new FileDisplayMode[]
        {
            Models.Domain.FolderItemListing.FileDisplayMode.Large,
            Models.Domain.FolderItemListing.FileDisplayMode.Midium,
            Models.Domain.FolderItemListing.FileDisplayMode.Small,
            Models.Domain.FolderItemListing.FileDisplayMode.Line,
        };

        public string FoldersManagementPageName => nameof(Views.SourceFoldersPage);


        static bool _LastIsImageFileThumbnailEnabled;
        static bool _LastIsArchiveFileThumbnailEnabled;
        static bool _LastIsFolderThumbnailEnabled;

        public FolderListupPageViewModel(
            ThumbnailManager thumbnailManager,
            FolderListingSettings folderListingSettings,
            OpenPageCommand openPageCommand
            )
        {
            _thumbnailManager = thumbnailManager;
            _folderListingSettings = folderListingSettings;
            OpenPageCommand = openPageCommand;

            FolderItems = new ObservableCollection<StorageItemViewModel>();
            FileItems = new ObservableCollection<StorageItemViewModel>();
            FileItemsView = new AdvancedCollectionView(FileItems);
            SelectedFileSortType = new ReactivePropertySlim<FileSortType>(FileSortType.TitleAscending);

            Groups = new FolderItemsGroupBase[]
            {
                new FolderFolderItemsGroup()
                {
                    Items = FolderItems,
                },
                new FileFolderItemsGroup()
                { 
                    Items = FileItems
                }
            };

            FileDisplayMode = _folderListingSettings.ToReactivePropertyAsSynchronized(x => x.FileDisplayMode);
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
            StorageItemViewModel.CurrentFileDisplayMode = _folderListingSettings.FileDisplayMode;

            FileDisplayMode.Subscribe(async x =>
            {
                await SoftRefreshItems();
            });
        }

        private async Task SoftRefreshItems()
        {
            if (FileItems.Any())
            {
                var items = FileItems.ToArray();
                FileItems.Clear();

                StorageItemViewModel.CurrentFileDisplayMode = FileDisplayMode.Value;
                FileItems.Reverse().ForEach(x => x.ClearImage());

                await Task.Delay(100);

                var sortedFileItems = SelectedFileSortType.Value switch
                {
                    FileSortType.TitleAscending => items.OrderBy(x => x.Name),
                    FileSortType.TitleDecending => items.OrderByDescending(x => x.Name),
                    FileSortType.UpdateTimeAscending => items.OrderBy(x => x.DateCreated),
                    FileSortType.UpdateTimeDecending => items.OrderByDescending(x => x.DateCreated),
                    _ => throw new NotSupportedException(),
                };

                foreach (var item in sortedFileItems)
                {
                    await item.InitializeAsync(default);
                }

                using (FileItemsView.DeferRefresh())
                {
                    FileItems.AddRange(sortedFileItems);
                }
            }
        }

        public override async Task<bool> CanNavigateAsync(INavigationParameters parameters)
        {
            NowProcessing = true;
            try
            {
                var mode = parameters.GetNavigationMode();
                if (mode == NavigationMode.Back)
                {
                    return await TryBackNavigation();
                }
                else if (mode == NavigationMode.Forward)
                {
                    return await TryForwardNavigation();
                }
            }
            finally
            {
                NowProcessing = false;
            }

            return true;
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _leavePageCancellationTokenSource.Cancel();
            _leavePageCancellationTokenSource.Dispose();
            _leavePageCancellationTokenSource = null;

            var mode = parameters.GetNavigationMode();
            if (mode == NavigationMode.Back)
            {
                PushCurrentFolderNavigationInfoOnBackNavigation();
            }
            else if (mode == NavigationMode.New)
            {
                PushCurrentFolderNavigationInfoOnNewOtherNavigation();
            }

            FileItems.Reverse().ForEach(x => x.StopImageLoading());
            FolderItems.Reverse().ForEach(x => x.StopImageLoading());

            _LastIsImageFileThumbnailEnabled = _folderListingSettings.IsImageFileThumbnailEnabled;
            _LastIsArchiveFileThumbnailEnabled = _folderListingSettings.IsArchiveFileThumbnailEnabled;
            _LastIsFolderThumbnailEnabled = _folderListingSettings.IsFolderThumbnailEnabled;

            base.OnNavigatedFrom(parameters);
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationService = parameters.GetNavigationService();

            NowProcessing = true;
            try
            {
                // Note: ファイル表示用のItemsRepeaterのItemTemplateが
                // VisualStateによって変更されるのを待つ
                await Task.Delay(50);

                var mode = parameters.GetNavigationMode();

                _leavePageCancellationTokenSource = new CancellationTokenSource();

                if (mode == NavigationMode.New)
                {
                    HasFileItem = false;

                    // PageのNavigationCache = "Enabled"としているため
                    // FolderListupPage（及びViewModel） は一つのインスタンスが使い回される
                    // そのため内部にページスタックを表現するためのDepthなどがある
                    // Newで到達した時に CurrentDepth = 0 にリセットすることで
                    // 細かいインクリ・デクリの制御を回避できる
                    CurrentDepth = 0;

                    using (await _NavigationLock.LockAsync(default))
                    {
                        bool isTokenChanged = false;
                        if (parameters.TryGetValue("token", out string token))
                        {
                            if (_currentToken != token)
                            {
                                _currentToken = token;
                                isTokenChanged = true;
                            }
                        }
#if DEBUG
                        else
                        {
                            Debug.Assert(false, "required 'token' parameter in FolderListupPage navigation.");
                        }
#endif

                        bool isPathChanged = false;
                        if (parameters.TryGetValue("path", out string path))
                        {
                            var unescapedPath = Uri.UnescapeDataString(path);
                            if (_currentPath != unescapedPath)
                            {
                                isPathChanged = true;
                                _currentPath = unescapedPath;
                            }
                        }

                        // 以下の場合に表示内容を更新する
                        //    1. 表示フォルダが変更された場合
                        //    2. 前回の更新が未完了だった場合
                        if (isTokenChanged || isPathChanged)
                        {
                            {
                                var fileItems = FileItems.ToArray();
                                var folderItems = FolderItems.ToArray();
                                FolderItems.Clear();
                                FileItems.Clear();
                                fileItems.AsParallel().ForAll(x => x.Dispose());
                                folderItems.AsParallel().ForAll(x => x.Dispose());
                            }

                            if (isTokenChanged)
                            {
                                _tokenGettingFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
                            }

                            if (_tokenGettingFolder == null)
                            {
                                throw new Exception("token parameter is require for path parameter.");
                            }

                            _currentFolder = (StorageFolder)await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);

                            DisplayCurrentPath = _currentFolder.Path;

                            await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                        }
                        else if (!_isCompleteEnumeration)
                        {
                            await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                        }
                    }
                }
                else if (mode == NavigationMode.Refresh)
                {
                    HasFileItem = false;
                    using (await _NavigationLock.LockAsync(default))
                    {
                        await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                    }
                }
                else if (mode == NavigationMode.Forward)
                {
                    HasFileItem = false;
                    await TryForwardNavigation();
                }
                else if (!_isCompleteEnumeration
                    || _LastIsImageFileThumbnailEnabled != _folderListingSettings.IsImageFileThumbnailEnabled
                    || _LastIsArchiveFileThumbnailEnabled != _folderListingSettings.IsArchiveFileThumbnailEnabled
                    || _LastIsFolderThumbnailEnabled != _folderListingSettings.IsFolderThumbnailEnabled
                    )
                {
                    using (await _NavigationLock.LockAsync(default))
                    {
                        await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                    }
                }
                else
                {
                    // 前回読み込みキャンセルしていたものを改めて読み込むように
                    FileItems.ForEach(x => x.RestoreThumbnailLoadingTask());
                    FolderItems.ForEach(x => x.RestoreThumbnailLoadingTask());
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


            _isCompleteEnumeration = false;
            try
            {
                if (_currentFolder != null)
                {
                    Debug.WriteLine(_currentFolder.Path);
                    await RefreshFolderItems(_currentFolder, ct);
                }
                else if (_tokenGettingFolder != null)
                {
                    Debug.WriteLine(_tokenGettingFolder.Path);
                    await RefreshFolderItems(_tokenGettingFolder, ct);
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

        
        private async ValueTask RefreshFolderItems(StorageFolder folder, CancellationToken ct)
        {
            FolderItems.Clear();
            FileItems.Clear();
            var itemsResult = await GetFolderItemsAsync(folder, ct);
            if (itemsResult.ItemsCount == 0)
            {
                return;
            }

            List<StorageItemViewModel> unsortedFileItems = new List<StorageItemViewModel>();
            await foreach (var folderItem in itemsResult.AsyncEnumerableItems.WithCancellation(ct))
            {
                ct.ThrowIfCancellationRequested();
                var item = new StorageItemViewModel(folderItem, _currentToken, _thumbnailManager, _folderListingSettings);
                if (folderItem is StorageFolder)
                {
                    FolderItems.Add(item);
                }
                else if (folderItem is StorageFile file
                    && SupportedFileTypesHelper.IsSupportedFileExtension(file.FileType)
                    )
                {
                    await item.InitializeAsync(ct);
                    unsortedFileItems.Add(item);
                }
            }

            var sortedFileItems = SelectedFileSortType.Value switch
            {
                FileSortType.TitleAscending => unsortedFileItems.OrderBy(x => x.Name),
                FileSortType.TitleDecending => unsortedFileItems.OrderByDescending(x => x.Name),
                FileSortType.UpdateTimeAscending => unsortedFileItems.OrderBy(x => x.DateCreated),
                FileSortType.UpdateTimeDecending => unsortedFileItems.OrderByDescending(x => x.DateCreated),
                _ => throw new NotSupportedException(),
            };
            using (FileItemsView.DeferRefresh())
            {
                FileItems.AddRange(sortedFileItems);
            }

            HasFileItem = FileItems.Any();
        }


        private async ValueTask<(uint ItemsCount, IAsyncEnumerable<IStorageItem> AsyncEnumerableItems)> GetFolderItemsAsync(StorageFolder folder, CancellationToken ct)
        {
#if WINDOWS_UWP
            var storageFolder = (StorageFolder)folder;
            var query = storageFolder.CreateItemQuery();
            var itemsCount = await query.GetItemCountAsync().AsTask(ct);            
            return (itemsCount, FolderHelper.GetEnumerator(query, itemsCount, ct));
#else
            var options = new EnumerationOptions() 
            {
                AttributesToSkip = System.IO.FileAttributes.ReadOnly,
            };
            var items = Directory.EnumerateFileSystemEntries(folder.Path, "*", options);

            var count = (uint)items.Count();
            return (count, GetEnumerator(folder, items, ct));
#endif
        }




        #endregion


        #region Navigation

        private INavigationService _navigationService;

        static List<FolderListupPageParameter> _stack = new List<FolderListupPageParameter>();
        static int CurrentDepth = 0;

        async Task PushCurrentFolderNavigationInfoAndSetNewFolder(StorageItemViewModel folderItemVM)
        {
            using (await _RefreshLock.LockAsync(_leavePageCancellationTokenSource.Token)) { }

            using (await _NavigationLock.LockAsync(default))
            {
                _stack.Add(new FolderListupPageParameter() { Token = _currentToken, Path = _currentPath });

                FileItems.Reverse().ForEach(x => x.ClearImage());
                FolderItems.Reverse().ForEach(x => x.ClearImage());

                CurrentDepth++;
                FolderItems.Clear();
                FileItems.Clear();
                
                _currentPath = await StorageItemViewModel.GetRawSubtractPath(folderItemVM);
                _currentFolder = (StorageFolder)await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);

                DisplayCurrentPath = _currentFolder.Path;

                await RefreshFolderItems(_leavePageCancellationTokenSource.Token);

                TrimNavigationStackToCurrentDepth();

                Debug.WriteLine("PushCurrentFolderNavigationInfoAndSetNewFolder: " + CurrentDepth);
            }
        }

        async void PushCurrentFolderNavigationInfoOnBackNavigation()
        {
            using (await _NavigationLock.LockAsync(default))
            {
                if (!_stack.Any())
                {
                    _stack.Add(new FolderListupPageParameter() { Token = _currentToken, Path = _currentPath });
                }

                CurrentDepth--;

                Debug.WriteLine("PushCurrentFolderNavigationInfoOnBackNavigation: " + CurrentDepth);
            }
        }

        async void PushCurrentFolderNavigationInfoOnNewOtherNavigation()
        {
            using (await _NavigationLock.LockAsync(default))
            {
                _stack.Add(new FolderListupPageParameter() { Token = _currentToken, Path = _currentPath });
            }

            Debug.WriteLine("PushCurrentFolderNavigationInfoOnNewOtherNavigation: " + CurrentDepth);
            //CurrentDepth--;
        }

        void TrimNavigationStackToCurrentDepth()
        {
            var trimTarget = CurrentDepth;
            while (trimTarget < _stack.Count)
            {
                _stack.RemoveAt(_stack.Count - 1);
            }
        }



        async Task<bool> TryBackNavigation()
        {
            using (await _NavigationLock.LockAsync(default))
            {
                _leavePageCancellationTokenSource?.Cancel();
                _leavePageCancellationTokenSource?.Dispose();
                _leavePageCancellationTokenSource = new CancellationTokenSource();

                using (await _RefreshLock.LockAsync(_leavePageCancellationTokenSource.Token)) { }

                if (CurrentDepth == 0) { return true; }

                FileItems.Reverse().ForEach(x => x.ClearImage());
                FolderItems.Reverse().ForEach(x => x.ClearImage());
                FolderItems.Clear();
                FileItems.Clear();

                CurrentDepth--;
                var prevFolderInfo = _stack.ElementAt(CurrentDepth);

                _currentPath = prevFolderInfo.Path;
                _currentFolder = (StorageFolder)await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);

                DisplayCurrentPath = _currentFolder.Path;

                await RefreshFolderItems(_leavePageCancellationTokenSource.Token);

                Debug.WriteLine("TryBackNavigation: " + CurrentDepth);

                return false;
            }
        }


        private async Task<bool> TryForwardNavigation()
        {
            using (await _NavigationLock.LockAsync(default))
            {
                _leavePageCancellationTokenSource.Cancel();
                _leavePageCancellationTokenSource.Dispose();
                _leavePageCancellationTokenSource = new CancellationTokenSource();

                using (await _RefreshLock.LockAsync(_leavePageCancellationTokenSource.Token)) { }

                if (CurrentDepth + 1 >= _stack.Count) { return true; }

                FileItems.Reverse().ForEach(x => x.ClearImage());
                FolderItems.Reverse().ForEach(x => x.ClearImage());

                CurrentDepth++;
                var forwardFolderInfo = _stack.ElementAt(CurrentDepth);

                FolderItems.Clear();
                FileItems.Clear();

                // 別ページからフォワードしてきた場合に_tokenGettingFolderが空になっている場合がある
                _tokenGettingFolder ??= await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(forwardFolderInfo.Token);
                _currentToken = forwardFolderInfo.Token;

                _currentPath = forwardFolderInfo.Path;
                _currentFolder = (StorageFolder)await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);

                DisplayCurrentPath = _currentFolder.Path;

                await RefreshFolderItems(_leavePageCancellationTokenSource.Token);


                Debug.WriteLine("TryForwardNavigation: " + CurrentDepth);

                return false;
            }
        }


        #endregion

        #region FileSortType

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
                        await SoftRefreshItems();
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
