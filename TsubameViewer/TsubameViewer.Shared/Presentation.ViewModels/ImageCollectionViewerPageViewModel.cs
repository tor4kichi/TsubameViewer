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
using TsubameViewer.Models.Domain.ImageView;
using TsubameViewer.Models.UseCase.PageNavigation.Commands;
using TsubameViewer.Models.UseCase.ViewManagement.Commands;
using Uno.Extensions;
using Uno.Threading;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Presentation.ViewModels
{

    public sealed class ImageCollectionViewerPageViewModel : ViewModelBase
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

        
        private BitmapImage _CurrentImage;
        public BitmapImage CurrentImage
        {
            get { return _CurrentImage; }
            private set { SetProperty(ref _CurrentImage, value); }
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

        private ApplicationView _appView;
        CompositeDisposable _navigationDisposables;

        internal static readonly Uno.Threading.AsyncLock ProcessLock = new Uno.Threading.AsyncLock();
        private readonly IScheduler _scheduler;
        private readonly ImageCollectionManager _imageCollectionManager;

        public ImageCollectionViewerPageViewModel(
            IScheduler scheduler,
            ImageCollectionManager imageCollectionManager,
            ImageCollectionPageSettings imageCollectionSettings,
            BackNavigationCommand backNavigationCommand,
            ToggleFullScreenCommand toggleFullScreenCommand
            )
        {
            _scheduler = scheduler;
            _imageCollectionManager = imageCollectionManager;
            ImageCollectionSettings = imageCollectionSettings;
            BackNavigationCommand = backNavigationCommand;
            ToggleFullScreenCommand = toggleFullScreenCommand;

            _PrefetchImages = new ReactivePropertySlim<IImageSource>[MaxPrefetchImageCount];
            for (var i = 0; i < MaxPrefetchImageCount; i++)
            {
                _PrefetchImages[i] = new ReactivePropertySlim<IImageSource>();
            }
            PrefetchImages = _PrefetchImages;

            DisplayCurrentImageIndex = this.ObserveProperty(x => CurrentImageIndex)
                .Select(x => x + 1)
                .ToReadOnlyReactivePropertySlim();

            _appView = ApplicationView.GetForCurrentView();
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
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
                bool isTokenChanged = false;
                if (parameters.TryGetValue("token", out string token))
                {
                    if (_currentToken != token)
                    {
                        _currentToken = token;
                        _tokenGettingFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);

                        ClearPrefetchImages();

                        Images = default;
                        CurrentImage = _emptyImage;
                        _CurrentImageIndex = 0;

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
                        _currentPath = unescapedPath;

                        if (_tokenGettingFolder == null)
                        {
                            throw new Exception("token parameter is require for path parameter.");
                        }

                        ClearPrefetchImages();

                        Images = default;
                        CurrentImage = _emptyImage;
                        _CurrentImageIndex = 0;

                        _currentFolderItem = await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);

                        isPathChanged = true;
                    }
                }

                // TODO: CurrentImageIndexをINavigationParametersから設定できるようにする


                // 以下の場合に表示内容を更新する
                //    1. 表示フォルダが変更された場合
                //    2. 前回の更新が未完了だった場合
                if (isTokenChanged || isPathChanged)
                {
                    await RefreshItems(_leavePageCancellationTokenSource.Token);

                    ResetPrefetchImageRange(_CurrentImageIndex);
                }
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

                        var image = GetPrefetchImage(CurrentPrefetchImageIndex);
                        CurrentImage = await image.GetOrCacheImageAsync();
#if DEBUG
                        Debug.WriteLine($"index: {CurrentImageIndex}, PrefetchIndex: {CurrentPrefetchImageIndex}, ImageName: {image.Name}");
                        Debug.WriteLine($"w={CurrentImage?.PixelWidth:F2}, h={CurrentImage?.PixelHeight:F2}");
#endif
                    }
                })
                .AddTo(_navigationDisposables);

            

            await base.OnNavigatedToAsync(parameters);
        }


        CancellationTokenSource _loadingCts;
        FastAsyncLock _loadingLock = new FastAsyncLock();


        #region Commands

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

        void SetPrefetchImage(int index, IImageSource image)
        {
            _PrefetchImages[index].Value = image;

            Debug.WriteLine($"[Prefetch Image] index: {index}, Name: {image?.Name}");
        }

        private int _CurrentPrefetchImageIndex;
        public int CurrentPrefetchImageIndex
        {
            get => _CurrentPrefetchImageIndex;
            set => SetProperty(ref _CurrentPrefetchImageIndex, value);
        }
        public ImageCollectionPageSettings ImageCollectionSettings { get; }
        public BackNavigationCommand BackNavigationCommand { get; }
        public ToggleFullScreenCommand ToggleFullScreenCommand { get; }

        // Note: Image Prefetchとは
        // 
        //  　Image切り替え時のチラツキ防止と素早い切替に対応するためのイメージ読み込みの交通整理機能
        //

        // 


        // CurrentImageIndexの切り替わりに反応してCurrentPrefetchImageIndexを切り替える
        // 同時に、Prefetch範囲から外れるImageと新たにPrefetch範囲に入ったImageを入れ替える

        private void ClearPrefetchImages()
        {
            // 一旦全部リセットする
            foreach (var i in _PrefetchImages)
            {
                i.Value = null;
            }
        }

        private void ResetPrefetchImageRange(int initialImageIndex)
        {
            ClearPrefetchImages();

            // 現在プリフェッチ画像を設定してプリフェッチ画像インデックスを強制通知（前と同一インデックスでも通知する）
            SetPrefetchImage(SwitchTargetIndexSubstraction, Images.ElementAtOrDefault(initialImageIndex));
            _CurrentPrefetchImageIndex = SwitchTargetIndexSubstraction;
            RaisePropertyChanged(nameof(CurrentPrefetchImageIndex));

            // Next方向の画像をプリフェッチ
            foreach (var i in Enumerable.Range(1, SwitchTargetIndexSubstraction))
            {
                var prefetchIndex = SwitchTargetIndexSubstraction + i;
                var realIndex = initialImageIndex + i;
                SetPrefetchImage(prefetchIndex, Images.ElementAtOrDefault(realIndex));
            }

            // Prev方向の画像をプリフェッチ
            foreach (var i in Enumerable.Range(1, SwitchTargetIndexSubstraction))
            {
                var prefetchIndex = SwitchTargetIndexSubstraction - i;
                var realIndex = initialImageIndex - i;
                SetPrefetchImage(prefetchIndex, Images.ElementAtOrDefault(realIndex));
            }

            _prevCurrentImageIndex = initialImageIndex;
        }

        int _prevCurrentImageIndex;

        private void UpdatePrefetchImageRange(int targetImageIndex)
        {
            var subtractIndex = targetImageIndex - _prevCurrentImageIndex;
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
                ResetPrefetchImageRange(targetImageIndex);
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
                var switchTargetPrefetchImageIndex = (nextPrefetchImageIndex + SwitchTargetIndexSubstraction) % MaxPrefetchImageCount;
                var switchTargetPrefetchImageRealIndex = targetImageIndex + SwitchTargetIndexSubstraction;
                // プリフェッチ対象に実際のインデックス位置となる画像を設定（範囲外の場合はnullで埋める）
                SetPrefetchImage(switchTargetPrefetchImageIndex, Images.ElementAtOrDefault(switchTargetPrefetchImageRealIndex));
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
                var switchTargetPrefetchImageIndex = (prevPrefetchImageIndex + SwitchTargetIndexSubstraction + 1) % MaxPrefetchImageCount;
                var switchTargetPrefetchImageRealIndex = targetImageIndex - SwitchTargetIndexSubstraction;
                // プリフェッチ対象に実際のインデックス位置となる画像を設定（範囲外の場合はnullで埋める）
                SetPrefetchImage(switchTargetPrefetchImageIndex, Images.ElementAtOrDefault(switchTargetPrefetchImageRealIndex));
            }
        }


        #endregion
    }



}
