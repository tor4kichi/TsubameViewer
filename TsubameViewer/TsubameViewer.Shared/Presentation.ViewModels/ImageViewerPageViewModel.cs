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
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
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

        private IImageSource[] _Images;
        public IImageSource[] Images
        {
            get { return _Images; }
            private set { SetProperty(ref _Images, value); }
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

        public ImageViewerPageSettings ImageCollectionSettings { get; }

        internal static readonly Uno.Threading.AsyncLock ProcessLock = new Uno.Threading.AsyncLock();
        private readonly ImageCollectionManager _imageCollectionManager;

        CompositeDisposable _disposables = new CompositeDisposable();

        public ImageViewerPageViewModel(
            ImageCollectionManager imageCollectionManager,
            ImageViewerPageSettings imageCollectionSettings,
            ToggleFullScreenCommand toggleFullScreenCommand
            )
        {
            _imageCollectionManager = imageCollectionManager;
            ImageCollectionSettings = imageCollectionSettings;
            ToggleFullScreenCommand = toggleFullScreenCommand;

            _PrefetchImages = new ReactivePropertySlim<IImageSource>[MaxPrefetchImageCount];
            for (var i = 0; i < MaxPrefetchImageCount; i++)
            {
                _PrefetchImages[i] = new ReactivePropertySlim<IImageSource>()
                    .AddTo(_disposables);
            }
            PrefetchImages = _PrefetchImages;

            DisplayCurrentImageIndex = this.ObserveProperty(x => CurrentImageIndex)
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
            ClearPrefetchImages();

            if (Images?.Any() ?? false)
            {
                var images = Images.ToArray();
                Images = null;
                images.AsParallel().ForAll(x => (x as IDisposable)?.Dispose());
            }

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

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationDisposables = new CompositeDisposable();
            _leavePageCancellationTokenSource = new CancellationTokenSource()
                .AddTo(_navigationDisposables);

            // 一旦ボタン類を押せないように変更通知
            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();

            var mode = parameters.GetNavigationMode();
            if (mode == NavigationMode.New)
            {
                // TODO: CurrentImageIndexをINavigationParametersから設定できるようにする


                if (parameters.TryGetValue("token", out string token))
                {
                    if (_currentToken != token)
                    {
                        _currentPath = null;
                        _currentFolderItem = null;

                        _currentToken = token;
                        _tokenGettingFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);

                        ClearPrefetchImages();

                        Images = default;
                        _CurrentImageIndex = 0;
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
                            throw new Exception("token parameter is require for path parameter.");
                        }

                        ClearPrefetchImages();

                        Images = default;
                        _CurrentImageIndex = 0;

                        _currentFolderItem = await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);
                    }
                }
            }

            // 以下の場合に表示内容を更新する
            //    1. 表示フォルダが変更された場合
            //    2. 前回の更新が未完了だった場合
            if (_tokenGettingFolder != null || _currentFolderItem != null)
            {
                await RefreshItems(_leavePageCancellationTokenSource.Token);

                await ResetPrefetchImageRange(_CurrentImageIndex);
            }

            // 表示画像が揃ったら改めてボタンを有効化
            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();


            // 画像インデックス更新によるプリフェッチ済み画像への切り替えとプリフェッチ対象の更新
            this.ObserveProperty(x => x.CurrentImageIndex)
                .Subscribe(async index =>
                {
                    using (await ProcessLock.LockAsync(_leavePageCancellationTokenSource.Token))
                    {
                        if (Images == null || Images.Length <= 1) { return; }

                        // タイトル
                        _appView.Title = $"{Images[index].Name} - {DisplayCurrentImageIndex.Value}/{Images.Length}";

                        // プリフェッチ範囲更新
                        UpdatePrefetchImageRange(index);

#if DEBUG
                        var image = GetPrefetchImage(CurrentPrefetchImageIndex);
                        var currentImage = await image.GenerateBitmapImageAsync((int)CanvasWidth.Value, (int)CanvasHeight.Value);
                        Debug.WriteLine($"index: {CurrentImageIndex}, PrefetchIndex: {CurrentPrefetchImageIndex}, ImageName: {image.Name}");
                        Debug.WriteLine($"w={currentImage?.PixelWidth:F2}, h={currentImage?.PixelHeight:F2}");
#endif
                    }
                })
                .AddTo(_navigationDisposables);

            

            await base.OnNavigatedToAsync(parameters);
        }


        CancellationTokenSource _loadingCts;
        FastAsyncLock _loadingLock = new FastAsyncLock();


        #region Commands

        public ToggleFullScreenCommand ToggleFullScreenCommand { get; }

        private DelegateCommand _GoNextImageCommand;
        public DelegateCommand GoNextImageCommand =>
            _GoNextImageCommand ??= new DelegateCommand(ExecuteGoNextImageCommand, CanGoNextCommand);

        private void ExecuteGoNextImageCommand()
        {
            CurrentImageIndex++;

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
            CurrentImageIndex--;

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }

        private bool CanGoPrevCommand()
        {
            return CurrentImageIndex > 0 && Images?.Length > 0;
        }

        #endregion

        #region Refresh ImageCollection

        IDisposable _ImageEnumerationDisposer;

        private async Task RefreshItems(CancellationToken ct)
        {
            _ImageEnumerationDisposer?.Dispose();
            _ImageEnumerationDisposer = null;

            var result = await _imageCollectionManager.GetImageSources(_currentFolderItem);
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


        #endregion



        #region Prefetch Image

        const int MaxPrefetchImageCount = 5; // 必ず奇数
        const int SwitchTargetIndexSubstraction = 2; // MaxPrefetchImageCount / 2;

        private ReactivePropertySlim<IImageSource>[] _PrefetchImages;
        public IReadOnlyReactiveProperty<IImageSource>[] PrefetchImages { get; }

        IImageSource GetPrefetchImage(int index)
        {
            return _PrefetchImages[index].Value;
        }


        private DelegateCommand _SizeChangedCommand;
        public DelegateCommand SizeChangedCommand =>
            _SizeChangedCommand ??= new DelegateCommand(() => 
            {
                if (!(Images?.Any() ?? false)) { return; }

                _ = ResetPrefetchImageRange(CurrentImageIndex);
            });

        async Task SetPrefetchImage(int index, IImageSource image)
        {
            // 入れ替え前のプリフェッチ画像は破棄させる
            _PrefetchImages[index].Value?.ClearImage();

            // プリフェッチ画像を設定し
            _PrefetchImages[index].Value = image;

            // 非同期で画像読み込みを開始
            if (image != null)
            {
                await image.GenerateBitmapImageAsync((int)CanvasWidth.Value, (int)CanvasHeight.Value);
            }

            Debug.WriteLine($"[Prefetch Image] index: {index}, Name: {image?.Name}");
        }

        private int _CurrentPrefetchImageIndex;
        public int CurrentPrefetchImageIndex
        {
            get => _CurrentPrefetchImageIndex;
            set => SetProperty(ref _CurrentPrefetchImageIndex, value);
        }

        // Note: Image Prefetchとは
        // 
        //  　Image切り替え時のチラツキ防止と素早い切替に対応するための先行読み込みを管理する仕組み
        //

        // CurrentImageIndexの切り替わりに反応してCurrentPrefetchImageIndexを切り替える
        // 同時に、Prefetch範囲から外れるImageと新たにPrefetch範囲に入ったImageを入れ替える

        private void ClearPrefetchImages()
        {
            // 一旦全部リセットする
            foreach (var i in _PrefetchImages)
            {
                i.Value?.ClearImage();
                i.Value = null;
            }
        }

        private async Task ResetPrefetchImageRange(int initialImageIndex)
        {
            ClearPrefetchImages();

            // 現在プリフェッチ画像を設定してプリフェッチ画像インデックスを強制通知（前と同一インデックスでも通知する）
            await SetPrefetchImage(SwitchTargetIndexSubstraction, Images.ElementAtOrDefault(initialImageIndex));
            _CurrentPrefetchImageIndex = SwitchTargetIndexSubstraction;
            RaisePropertyChanged(nameof(CurrentPrefetchImageIndex));

            // Next方向の画像をプリフェッチ
            foreach (var i in Enumerable.Range(1, SwitchTargetIndexSubstraction))
            {
                var prefetchIndex = SwitchTargetIndexSubstraction + i;
                var realIndex = initialImageIndex + i;
                await SetPrefetchImage(prefetchIndex, Images.ElementAtOrDefault(realIndex));
            }

            // Prev方向の画像をプリフェッチ
            foreach (var i in Enumerable.Range(1, SwitchTargetIndexSubstraction))
            {
                var prefetchIndex = SwitchTargetIndexSubstraction - i;
                var realIndex = initialImageIndex - i;
                await SetPrefetchImage(prefetchIndex, Images.ElementAtOrDefault(realIndex));
            }

            _prevCurrentImageIndex = initialImageIndex;
        }

        int _prevCurrentImageIndex;

        private void UpdatePrefetchImageRange(int targetImageIndex)
        {
            var subtractIndex = targetImageIndex - _prevCurrentImageIndex;
            
            if (subtractIndex == 0) { return; }
            
            if (subtractIndex == 1)
            {
                GoNextPrefetchImageRange(targetImageIndex);
            }
            else if (subtractIndex == -1)
            {
                GoPreviewPrefetchImageRange(targetImageIndex);
            }
            else
            {
                _ = ResetPrefetchImageRange(targetImageIndex).ConfigureAwait(false);
            }

            _prevCurrentImageIndex = targetImageIndex;

            void GoNextPrefetchImageRange(int targetImageIndex)
            {
                // プリフェッチ済みの次画像にインデックスを移動
                var nextPrefetchImageIndex = (_CurrentPrefetchImageIndex + 1) % MaxPrefetchImageCount;
                var nextPrefetchImage = GetPrefetchImage(nextPrefetchImageIndex);
                if (nextPrefetchImage == null)
                {
                    throw new Exception();
                }                
                CurrentPrefetchImageIndex = nextPrefetchImageIndex;

                // 新しいプリフェッチ対象の読み込み
                // プリフェッチ対象に実際のインデックス位置となる画像を設定（範囲外の場合はnullで埋める）
                var switchTargetPrefetchImageIndex = (nextPrefetchImageIndex + SwitchTargetIndexSubstraction) % MaxPrefetchImageCount;
                var switchTargetPrefetchImageRealIndex = targetImageIndex + SwitchTargetIndexSubstraction;
                 _ = SetPrefetchImage(switchTargetPrefetchImageIndex, Images.ElementAtOrDefault(switchTargetPrefetchImageRealIndex)).ConfigureAwait(false);
            }

            void GoPreviewPrefetchImageRange(int targetImageIndex)
            {
                // プリフェッチ済みの前画像にインデックスを移動
                var prevPrefetchImageIndex = (_CurrentPrefetchImageIndex - 1) < 0 ? MaxPrefetchImageCount - 1 : _CurrentPrefetchImageIndex - 1;
                var prevPrefetchImage = GetPrefetchImage(prevPrefetchImageIndex);
                if (prevPrefetchImage == null)
                {
                    throw new Exception();
                }
                CurrentPrefetchImageIndex = prevPrefetchImageIndex;

                // 新しいプリフェッチ対象の読み込み
                // プリフェッチ対象に実際のインデックス位置となる画像を設定（範囲外の場合はnullで埋める）
                var switchTargetPrefetchImageIndex = (prevPrefetchImageIndex + SwitchTargetIndexSubstraction + 1) % MaxPrefetchImageCount;
                var switchTargetPrefetchImageRealIndex = targetImageIndex - SwitchTargetIndexSubstraction;
                _ = SetPrefetchImage(switchTargetPrefetchImageIndex, Images.ElementAtOrDefault(switchTargetPrefetchImageRealIndex)).ConfigureAwait(false);
            }
        }

        #endregion
    }



}
