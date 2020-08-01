using Microsoft.Toolkit.Uwp.Helpers;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.Bookmark;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views.ViewManagement.Commands;
using Uno;
using Uno.Extensions;
using Uno.Threading;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class ImageViewerPageViewModel : ViewModelBase, IDestructible
    {
        private string _currentToken;
        private StorageFolder _tokenGettingFolder;

        private string _currentPath;
        private IStorageItem _currentFolderItem;

        private CancellationTokenSource _leavePageCancellationTokenSource;

        IDisposable _ImageEnumerationDisposer;

        private IImageSource[] _Images;
        public IImageSource[] Images
        {
            get { return _Images; }
            private set { SetProperty(ref _Images, value); }
        }


        private BitmapImage _CurrentImage;
        public BitmapImage CurrentImage
        {
            get => _CurrentImage;
            private set => SetProperty(ref _CurrentImage, value);
        }

        private int _CurrentImageIndex;
        public int CurrentImageIndex
        {
            get => _CurrentImageIndex;
            set => SetProperty(ref _CurrentImageIndex, value);
        }

        private string _ParentFolderOrArchiveName;
        public string ParentFolderOrArchiveName
        {
            get { return _ParentFolderOrArchiveName; }
            set { SetProperty(ref _ParentFolderOrArchiveName, value); }
        }

        public IReadOnlyReactiveProperty<int> DisplayCurrentImageIndex { get; }

        public ReactiveProperty<double> CanvasWidth { get; }
        public ReactiveProperty<double> CanvasHeight { get; }

        private ApplicationView _appView;
        CompositeDisposable _navigationDisposables;

        public ImageViewerSettings ImageViewerSettings { get; }

        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly ImageCollectionManager _imageCollectionManager;
        private readonly BookmarkManager _bookmarkManager;
        CompositeDisposable _disposables = new CompositeDisposable();

        public ImageViewerPageViewModel(
            SourceStorageItemsRepository sourceStorageItemsRepository,
            ImageCollectionManager imageCollectionManager,
            ImageViewerSettings imageCollectionSettings,
            ToggleFullScreenCommand toggleFullScreenCommand,
            BookmarkManager bookmarkManager
            )
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _imageCollectionManager = imageCollectionManager;
            ImageViewerSettings = imageCollectionSettings;
            ToggleFullScreenCommand = toggleFullScreenCommand;
            _bookmarkManager = bookmarkManager;
            DisplayCurrentImageIndex = this.ObserveProperty(x => x.CurrentImageIndex)
                .Select(x => x + 1)
                .ToReadOnlyReactivePropertySlim()
                .AddTo(_disposables);

            CanvasWidth = new ReactiveProperty<double>()
                .AddTo(_disposables);
            CanvasHeight = new ReactiveProperty<double>()
                .AddTo(_disposables);

            _appView = ApplicationView.GetForCurrentView();


            
        }


        public void Destroy()
        {
            _disposables.Dispose();
        }



        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            if (Images?.Any() ?? false)
            {
                var images = Images.ToArray();
                Images = null;
                images.AsParallel().ForAll(x => (x as IDisposable)?.Dispose());
            }

            CurrentImage = null;

            _leavePageCancellationTokenSource.Cancel();
            _navigationDisposables.Dispose();
            _ImageEnumerationDisposer?.Dispose();
            _ImageEnumerationDisposer = null;

            // フルスクリーンを終了
            ApplicationView.GetForCurrentView().ExitFullScreenMode();

            _appView.Title = String.Empty;
            ParentFolderOrArchiveName = String.Empty;

            base.OnNavigatedFrom(parameters);
        }

        BitmapImage _emptyImage = new BitmapImage();

        public override void OnNavigatingTo(INavigationParameters parameters)
        {
            Views.PrimaryWindowCoreLayout.CurrentNavigationParameters = parameters;

            base.OnNavigatingTo(parameters);
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationDisposables = new CompositeDisposable();
            _leavePageCancellationTokenSource = new CancellationTokenSource()
                .AddTo(_navigationDisposables);

            // 一旦ボタン類を押せないように変更通知
            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();

            var mode = parameters.GetNavigationMode();
            if (mode == NavigationMode.New
                || mode == NavigationMode.Back
                || mode == NavigationMode.Forward
                )
            {
                // TODO: CurrentImageIndexをINavigationParametersから設定できるようにする


                if (parameters.TryGetValue("token", out string token))
                {
                    if (_currentToken != token)
                    {
                        _currentPath = null;
                        _currentFolderItem = null;

                        _currentToken = token;

                        var item = await _sourceStorageItemsRepository.GetItemAsync(token);

                        _tokenGettingFolder = item as StorageFolder;

                        // ファイルアクティベーションなど
                        if (item is StorageFile file)
                        {
                            _currentFolderItem = file;
                        }

                        Images = default;
                        CurrentImageIndex = 0;
                    }
                }
#if DEBUG
                else
                {
                    Debug.Assert(false, "required 'token' parameter in FolderListupPage navigation.");
                }
#endif

                if (parameters.TryGetValue("path", out string path))
                {
                    var unescapedPath = Uri.UnescapeDataString(path);
                    if (_currentPath != unescapedPath)
                    {
                        _currentPath = unescapedPath;
                        if (_tokenGettingFolder == null)
                        {
                            // token がファイルを指す場合は _currentFolderItem を通じて表示する
                            if (_currentFolderItem.Name != unescapedPath)
                            {
                                throw new Exception("token parameter is require for path parameter.");
                            }
                        }
                        else
                        {
                            _currentFolderItem = await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);
                        }

                        Images = default;
                        CurrentImageIndex = 0;
                    }
                }
            }

            // 以下の場合に表示内容を更新する
            //    1. 表示フォルダが変更された場合
            //    2. 前回の更新が未完了だった場合
            if (_tokenGettingFolder != null || _currentFolderItem != null)
            {
                await RefreshItems(_leavePageCancellationTokenSource.Token);
            }

            // 表示する画像を決める
            if (mode == NavigationMode.Forward 
                || parameters.ContainsKey("__restored") 
                || (mode == NavigationMode.New && !parameters.ContainsKey("pageName"))
                )
            {
                var bookmarkPageName = _bookmarkManager.GetBookmarkedPageName(_currentFolderItem.Path);
                if (bookmarkPageName != null)
                {
                    for (var i = 0; i < Images.Length; i++)
                    {
                        if (Images[i].Name == bookmarkPageName)
                        {
                            CurrentImageIndex = i;
                            break;
                        }
                    }
                }
            }
            else if (mode == NavigationMode.New && parameters.ContainsKey("pageName")
                )
            {
                if (parameters.TryGetValue("pageName", out string pageName))
                {
                    var unescapedPageName = Uri.UnescapeDataString(pageName);
                    var firstSelectItem = Images.FirstOrDefault(x => x.Name == unescapedPageName);
                    if (firstSelectItem != null)
                    {
                        CurrentImageIndex = Images.IndexOf(firstSelectItem);
                    }
                }

                // TODO: FileSortTypeを受け取って表示順の入れ替えに対応するべきか否か
                //if (parameters.TryGetValue("sort", out string sortMethod))
                {

                }
            }

            // 表示画像が揃ったら改めてボタンを有効化
            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();

            // 画像更新
            this.ObserveProperty(x => x.CurrentImageIndex)
                .Subscribe(async index =>
                {
                    if (Images == null || Images.Length == 0) { return; }

                    _imageLoadingCts?.Cancel();
                    _imageLoadingCts?.Dispose();
                    _imageLoadingCts = new CancellationTokenSource();

                    var ct = _imageLoadingCts.Token;
                    try
                    {
                        using (await _imageLoadingLock.LockAsync(ct))
                        {
                            var imageSource = Images[index];
                            _appView.Title = $"{imageSource.Name} - {DisplayCurrentImageIndex.Value}/{Images.Length}";

                            await Task.Delay(1);

                            if (ct.IsCancellationRequested) { return; }

                            var bitmapImage = await imageSource.GenerateBitmapImageAsync(ct);

                            // 画面より小さい画像を表示するときはアンチエイリアスと省メモリのため画面サイズにまで縮小
                            var canvasWidth = (int)CanvasWidth.Value;
                            var canvasHeight = (int)CanvasHeight.Value;
                            if (bitmapImage.PixelHeight > bitmapImage.PixelWidth)
                            {
                                if (bitmapImage.PixelHeight > canvasHeight)
                                {
                                    bitmapImage.DecodePixelHeight = canvasHeight;
                                }
                            }
                            else
                            {
                                if (bitmapImage.PixelWidth > canvasWidth)
                                {
                                    bitmapImage.DecodePixelWidth = canvasWidth;
                                }
                            }

                            if (ct.IsCancellationRequested) { return; }

                            CurrentImage = bitmapImage;

                            _bookmarkManager.AddBookmark(_currentFolderItem.Path, imageSource.Name);
                        }
                    }
                    catch (OperationCanceledException) { }
#if DEBUG
                    Debug.WriteLine($"w={CurrentImage?.PixelWidth:F2}, h={CurrentImage?.PixelHeight:F2}");
#endif
                })
                .AddTo(_navigationDisposables);

            await base.OnNavigatedToAsync(parameters);
        }

        CancellationTokenSource _imageLoadingCts;
        FastAsyncLock _imageLoadingLock = new FastAsyncLock();

        private async Task RefreshItems(CancellationToken ct)
        {
            _ImageEnumerationDisposer?.Dispose();
            _ImageEnumerationDisposer = null;

            var result = await _imageCollectionManager.GetImageSourcesAsync(_currentFolderItem);
            if (result != null)
            {
                Images = result.Images;
                CurrentImageIndex = result.FirstSelectedIndex;
                _ImageEnumerationDisposer = result.ItemsEnumeratorDisposer;
                ParentFolderOrArchiveName = result.ParentFolderOrArchiveName;
            }

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }


        #region Commands

        public ToggleFullScreenCommand ToggleFullScreenCommand { get; }

        private DelegateCommand _GoNextImageCommand;
        public DelegateCommand GoNextImageCommand =>
            _GoNextImageCommand ??= new DelegateCommand(ExecuteGoNextImageCommand, CanGoNextCommand);

        private void ExecuteGoNextImageCommand()
        {
            CurrentImageIndex += 1;

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }

        private bool CanGoNextCommand()
        {
            return CurrentImageIndex + 1 < Images?.Length;
        }

        private DelegateCommand _GoPrevImageCommand;
        public DelegateCommand GoPrevImageCommand =>
            _GoPrevImageCommand ??= new DelegateCommand(ExecuteGoPrevImageCommand, CanGoPrevCommand);

        private void ExecuteGoPrevImageCommand()
        {
            CurrentImageIndex -= 1;

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }

        private bool CanGoPrevCommand()
        {
            return CurrentImageIndex > 0 && Images?.Length > 0;
        }



        private DelegateCommand _SizeChangedCommand;
        public DelegateCommand SizeChangedCommand =>
            _SizeChangedCommand ??= new DelegateCommand(async () =>
            {
                if (!(Images?.Any() ?? false)) { return; }

                await Task.Delay(50);

                RaisePropertyChanged(nameof(CurrentImageIndex));
            });


        #endregion

    }



}
