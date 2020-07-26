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
using TsubameViewer.Models.UseCase.PageNavigation;
using TsubameViewer.Models.UseCase.PageNavigation.Commands;
using Uno.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;
using Windows.UI.Xaml.Media.Animation;

namespace TsubameViewer.Presentation.ViewModels
{
    using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

    public enum FolderViewFirstSort
    {
        Folders,
        Files
    }

    public enum FolderViewOtherSort
    {
        TitleAscending,
        TitleDecending,
        UpdateTimeAscending,
        UpdateTimeDecending,
    }

    public sealed class FolderListupPageViewModel : ViewModelBase
    {
        // Note: FolderListupPage内でフォルダ階層のコントロールをしている
        // フォルダ階層の移動はNavigationServiceを通さずにページ内で完結してる
        // 

        DelegateCommand<object> _OpenFolderItemCommand;
        public DelegateCommand<object> OpenFolderItemCommand =>
            _OpenFolderItemCommand ??= new DelegateCommand<object>(async (item) =>
            {
                if (item is StorageItemViewModel itemVM)
                {
                    if (itemVM.Type == StorageItemTypes.Image || itemVM.Type == StorageItemTypes.Archive)
                    {
                        var parameters = await StorageItemViewModel.CreatePageParameterAsync(itemVM);
                        var result = await _navigationService.NavigateAsync(nameof(Views.ImageCollectionViewerPage), parameters, new DrillInNavigationTransitionInfo());
                    }
                    else if (itemVM.Type == StorageItemTypes.Folder)
                    {
                        await PushCurrentFolderNavigationInfoAndSetNewFolder(itemVM);
                    }
                }
            });

        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderListingSettings _folderListingSettings;
        public OpenPageCommand OpenPageCommand { get; }



        public ObservableCollection<StorageItemViewModel> FolderItems { get; }
        public ObservableCollection<StorageItemViewModel> FileItems { get; }

        public ReactivePropertySlim<FolderViewFirstSort> SelectedFolderViewFirstSort { get; }

        static FastAsyncLock _NavigationLock = new FastAsyncLock();

        IReadOnlyReactiveProperty<QueryOptions> _currentQueryOptions;

        private string _currentToken;
        private StorageFolder _tokenGettingFolder;

        private string _currentPath;
        private StorageFolder _currentFolder;

        private CancellationTokenSource _leavePageCancellationTokenSource;

        bool _isCompleteEnumeration = false;
        int _previousEnumerationIndex = 0;

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

        public string FoldersManagementPageName => nameof(Views.StoredFoldersManagementPage);

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
            SelectedFolderViewFirstSort = new ReactivePropertySlim<FolderViewFirstSort>(FolderViewFirstSort.Folders);

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
            FileDisplayMode.Subscribe(x =>
            {
                StorageItemViewModel.CurrentFileDisplayMode = x;
            });
        }

        public override async Task<bool> CanNavigateAsync(INavigationParameters parameters)
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

            base.OnNavigatedFrom(parameters);
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationService = parameters.GetNavigationService();


            var mode = parameters.GetNavigationMode();

            _leavePageCancellationTokenSource = new CancellationTokenSource();

            if (mode == NavigationMode.New)
            {
                // 
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
                        if (isTokenChanged)
                        {
                            FolderItems.Clear();
                            FileItems.Clear();
                            _previousEnumerationIndex = 0;

                            _tokenGettingFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
                        }

                        if (_tokenGettingFolder == null)
                        {
                            throw new Exception("token parameter is require for path parameter.");
                        }

                        FolderItems.Clear();
                        FileItems.Clear();
                        _previousEnumerationIndex = 0;

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
                using (await _NavigationLock.LockAsync(default))
                {
                    await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                }
            }
            else if (mode == NavigationMode.Forward)
            {
                await TryForwardNavigation();
            }
            else if (!_isCompleteEnumeration)
            {
                using (await _NavigationLock.LockAsync(default))
                {
                    await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                }
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

            int currentIndex = _previousEnumerationIndex;
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
                    FileItems.Add(item);
                }
                _previousEnumerationIndex = currentIndex;
            }
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

                CurrentDepth++;
                FolderItems.Clear();
                FileItems.Clear();
                _previousEnumerationIndex = 0;

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
                _leavePageCancellationTokenSource.Cancel();
                _leavePageCancellationTokenSource.Dispose();
                _leavePageCancellationTokenSource = new CancellationTokenSource();

                using (await _RefreshLock.LockAsync(_leavePageCancellationTokenSource.Token)) { }

                if (CurrentDepth == 0) { return true; }

                CurrentDepth--;
                var prevFolderInfo = _stack.ElementAt(CurrentDepth);

                FolderItems.Clear();
                FileItems.Clear();
                _previousEnumerationIndex = 0;

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

                CurrentDepth++;
                var forwardFolderInfo = _stack.ElementAt(CurrentDepth);

                FolderItems.Clear();
                FileItems.Clear();
                _previousEnumerationIndex = 0;

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
    }

    class FolderListupPageParameter
    {
        public string Token { get; set; }
        public string Path { get; set; }
    }
    
}
