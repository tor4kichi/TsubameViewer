using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
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
using TsubameViewer.Presentation.ViewModels.Commands;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;

namespace TsubameViewer.Presentation.ViewModels
{

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
        static readonly string[] SupportedFileExtensions = new[] 
        {
            ".png", ".jpg",
            ".zip"
        };

        public ObservableCollection<StorageItemViewModel> FolderItems { get; }

        public ReactivePropertySlim<FolderViewFirstSort> SelectedFolderViewFirstSort { get; }
        public OpenFolderItemCommand OpenFolderItemCommand { get; }

        IReadOnlyReactiveProperty<QueryOptions> _currentQueryOptions;

        private string _currentToken;
        private StorageFolder _tokenGettingFolder;

        private string _currentPath;
        private StorageFolder _currentFolder;

        private CancellationTokenSource _leavePageCancellationTokenSource;

        bool _isCompleteEnumeration = false;
        int _previousEnumerationIndex = 0;

        public FolderListupPageViewModel(
            OpenFolderItemCommand openFolderItemCommand
            )
        {
            FolderItems = new ObservableCollection<StorageItemViewModel>();
            SelectedFolderViewFirstSort = new ReactivePropertySlim<FolderViewFirstSort>(FolderViewFirstSort.Folders);
            OpenFolderItemCommand = openFolderItemCommand;

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

        public override Task<bool> CanNavigateAsync(INavigationParameters parameters)
        {
            var mode = parameters.GetNavigationMode();
            if (mode == NavigationMode.Back)
            {
                return Task.FromResult(_BackNavigationParameterStack.Any());
            }
            else if (mode == NavigationMode.Forward)
            {
                return Task.FromResult(_ForwardNavigationParameterStack.Any());
            }

            return Task.FromResult(true);
        }


        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            var mode = parameters.GetNavigationMode();
            if (mode == NavigationMode.New || mode == NavigationMode.Forward)
            {
                _BackNavigationParameterStack.Push(_prevNavigationParameters);
            }
            else if (mode == NavigationMode.Back)
            {
                _ForwardNavigationParameterStack.Push(_prevNavigationParameters);
            }

            _leavePageCancellationTokenSource.Cancel();
            _leavePageCancellationTokenSource.Dispose();
            _leavePageCancellationTokenSource = null;

            base.OnNavigatedFrom(parameters);
        }

        static Stack<INavigationParameters> _BackNavigationParameterStack = new Stack<INavigationParameters>();
        static Stack<INavigationParameters> _ForwardNavigationParameterStack = new Stack<INavigationParameters>();

        INavigationParameters _prevNavigationParameters;
        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            var mode = parameters.GetNavigationMode();
            if (mode == NavigationMode.Back)
            {
                parameters = _BackNavigationParameterStack.Pop();
            }
            else if (mode == NavigationMode.Forward)
            {
                parameters = _ForwardNavigationParameterStack.Pop();
            }

            _prevNavigationParameters = parameters;

            _leavePageCancellationTokenSource = new CancellationTokenSource();

            if (mode != NavigationMode.Refresh)
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
                if (isTokenChanged || isPathChanged || !_isCompleteEnumeration)
                {
                    if (isTokenChanged)
                    {
                        FolderItems.Clear();
                        _previousEnumerationIndex = 0;

                        _tokenGettingFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
                    }

                    if (isPathChanged)
                    {
                        if (_tokenGettingFolder == null)
                        {
                            throw new Exception("token parameter is require for path parameter.");
                        }


                        FolderItems.Clear();
                        _previousEnumerationIndex = 0;

                        _currentFolder = (StorageFolder)await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);
                    }

                    await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
                }
            }
            else
            {
                await RefreshFolderItems(_leavePageCancellationTokenSource.Token);
            }
            

            await base.OnNavigatedToAsync(parameters);

            
        }


        #region Refresh Item

        private async Task RefreshFolderItems(CancellationToken ct)
        {
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
            var itemsResult = await GetFolderItemsAsync(folder, ct);
            if (itemsResult.ItemsCount == 0)
            {
                return;
            }

            int currentIndex = _previousEnumerationIndex;
            await foreach (var folderItem in itemsResult.AsyncEnumerableItems.WithCancellation(ct))
            {
                var item = new StorageItemViewModel(folderItem, _currentToken);
                FolderItems.Add(item);
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


        #region Commands



        #endregion
    }

    
}
