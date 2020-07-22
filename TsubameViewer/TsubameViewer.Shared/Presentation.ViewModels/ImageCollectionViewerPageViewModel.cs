using Microsoft.Toolkit.Uwp.Helpers;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Presentation.ViewModels.Commands;
using Uno.Extensions;
using Uno.Threading;
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

        CompositeDisposable _navigationDisposables;

        internal static readonly Uno.Threading.AsyncLock ProcessLock = new Uno.Threading.AsyncLock();
        private readonly IScheduler _scheduler;

        public ImageCollectionViewerPageViewModel(
            IScheduler scheduler,
            BackNavigationCommand backNavigationCommand
            )
        {
            _PrefetchImages = new ReactivePropertySlim<IImageSource>[MaxPrefetchImageCount];
            for (var i = 0; i < MaxPrefetchImageCount; i++)
            {
                _PrefetchImages[i] = new ReactivePropertySlim<IImageSource>();
            }

            PrefetchImages = _PrefetchImages;
            _scheduler = scheduler;
            BackNavigationCommand = backNavigationCommand;
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _leavePageCancellationTokenSource.Cancel();
            _navigationDisposables.Dispose();

            base.OnNavigatedFrom(parameters);
        }

        BitmapImage _emptyImage = new BitmapImage();

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationDisposables = new CompositeDisposable();
            _leavePageCancellationTokenSource = new CancellationTokenSource()
                .AddTo(_navigationDisposables);
            
            bool isTokenChanged = false;
            if (parameters.TryGetValue("token", out string token))
            {
                if (_currentToken != token)
                {
                    _currentToken = token;
                    _tokenGettingFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);

                    ClearPrefetchImages();

                    Images = null;
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

                    Images = null;
                    CurrentImage = _emptyImage;
                    _CurrentImageIndex = 0;

                    _currentFolderItem = await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);

                    isPathChanged = true;
                }
            }

            // 一旦ボタン類を押せないように変更通知
            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();



            // TODO: CurrentImageIndexをINavigationParametersから設定できるようにする


            // 以下の場合に表示内容を更新する
            //    1. 表示フォルダが変更された場合
            //    2. 前回の更新が未完了だった場合
            if (isTokenChanged || isPathChanged)
            {
                await RefreshItems(_leavePageCancellationTokenSource.Token);
                
                ResetPrefetchImageRange(_CurrentImageIndex);
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
            return CurrentImageIndex < Images?.Length;
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

        private async Task RefreshItems(CancellationToken ct)
        {
            if (_currentFolderItem is StorageFile file)
            {
                if (file.FileType == ".jpg" || file.FileType == ".png")
                {
                    var parentFolder = await file.GetParentAsync();
                    // 画像ファイルが選ばれた時、そのファイルの所属フォルダをコレクションとして表示する
                    var result = await Task.Run(async () => await GetImagesFromFolderAsync(parentFolder, ct));
                    try
                    {
                        var images = new IImageSource[result.ItemsCount];
                        int index = 0;
                        await foreach (var item in result.Images.WithCancellation(ct))
                        {
                            images[index] = item;
                            if (item.Name == file.Name)
                            {
                                CurrentImageIndex = index;
                            }
                            index++;
                        }

                        Images = images;                        
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
                else
                {
                    var result = await Task.Run(async () => await GetImagesFromArchiveFileAsync(file, ct));
                    try
                    {
                        Images = result.Images.ToArray();
                    }
                    catch (OperationCanceledException)
                    {
                        result.diposable.Dispose();
                    }
                }
            }
            else if (_currentFolderItem is StorageFolder folder)
            {
                var result = await Task.Run(async () => await GetImagesFromFolderAsync(folder, ct));
                try
                {
                    var images = new IImageSource[result.ItemsCount];
                    int index = 0;
                    await foreach (var item in result.Images.WithCancellation(ct))
                    {
                        images[index] = item;
                        index++;
                    }

                    Images = images;
                }
                catch (OperationCanceledException)
                { 
                }
            }

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }

        private async Task<(uint ItemsCount, IAsyncEnumerable<IImageSource> Images)> GetImagesFromFolderAsync(StorageFolder storageFolder, CancellationToken ct)
        {
#if WINDOWS_UWP
            var query = storageFolder.CreateItemQuery();
            var itemsCount = await query.GetItemCountAsync();
            return (itemsCount, AsyncEnumerableImages(itemsCount, query, ct));
#else
            return (itemsCount, AsyncEnumerableImages(
#endif
        }
#if WINDOWS_UWP
        async IAsyncEnumerable<IImageSource> AsyncEnumerableImages(uint count, StorageItemQueryResult queryResult, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var item in FolderHelper.GetEnumerator(queryResult, count, ct))
            {
                yield return new StorageFileImageSource(item as StorageFile);
            }
        }
#else
                
#endif
    

        private async Task<(uint ItemsCount, CompositeDisposable diposable, IEnumerable<IImageSource> Images)> GetImagesFromArchiveFileAsync(StorageFile file, CancellationToken ct)
        {
            var result = file.FileType switch 
            {
                ".zip" => GetImagesFromZipFile((await file.OpenReadAsync()).AsStreamForRead()),
                _ => throw new NotSupportedException("not supported file type: " + file.FileType),
            };

            return (result.ItemsCount, result.disposable, result.Images);
        }


        private (uint ItemsCount, CompositeDisposable disposable, IEnumerable<IImageSource> Images) GetImagesFromZipFile(Stream stream)
        {
            var disposable = new CompositeDisposable();
            var zipArchive = new ZipArchive(stream)
                .AddTo(disposable);

            return ((uint)zipArchive.Entries.Count, disposable, zipArchive.Entries.Where(x => IsSuppotedImageFileName(x.FullName)).Select(x => (IImageSource)new ZipArchiveEntryImageSource(x)));
        }


        private static bool IsSuppotedImageFileName(string name)
        {
            if (name.EndsWith(".jpg")) { return true; }
            if (name.EndsWith(".png")) { return true; }

            return false;
        }
        /*
        private async Task<(uint ItemsCount, CompositeDisposable disposable, IEnumerable<IImageSource> Images)> GetImagesFromFileAsync(StorageFile file)
        {
            var disposable = new CompositeDisposable();
            try
            {
                var stream = await file.OpenStreamForReadAsync()
                    .AddTo(disposable);
                var reader = ReaderFactory.Open(stream)
                    .AddTo(disposable);
                
                while (!reader.MoveToNextEntry())
                {
                    ct.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch
            {
                disposable.Dispose();
                throw;
            }
        }
        */

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
        public BackNavigationCommand BackNavigationCommand { get; }

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


    public interface IImageSource
    {
        string Name { get; }
        Task<BitmapImage> GetOrCacheImageAsync();
        bool IsImageGenerated { get; }
        void CancelLoading();
    }


    public sealed class ArchiveEntryImageSource : IImageSource, IDisposable
    {
        private MemoryStream _imageStream;
        BitmapImage _image;

        public ArchiveEntryImageSource(string name, MemoryStream imageStream)
        {
            Name = name;
            _imageStream = imageStream;
        }

        public string Name { get; }
        public bool IsImageGenerated => _image != null;

        
        public void Dispose()
        {
            (_imageStream as IDisposable)?.Dispose();
        }

        public async Task<BitmapImage> GetOrCacheImageAsync()
        {
            if (_image != null) { return _image; }

            using (var stream = _imageStream.AsRandomAccessStream())
            {
                var bitmap = new BitmapImage();
                bitmap.SetSource(stream);
                return _image = bitmap;
            }
        }

        public void CancelLoading()
        {

        }
    }

    public sealed class StorageFileImageSource : IImageSource, IDisposable
    {
        private readonly StorageFile _file;
        
        public StorageFileImageSource(StorageFile file)
        {
            _file = file;
        }

        public void Dispose()
        {
        }

        public string Name => _file.Name;
        public bool IsImageGenerated => _image != null;
        BitmapImage _image;

        public async Task<BitmapImage> GetOrCacheImageAsync()
        {
            if (_image != null) { return _image; }

            using (var stream = await _file.OpenReadAsync())
            {
                var bitmap = new BitmapImage();
                bitmap.SetSource(stream);
                return _image = bitmap;
            }
        }

        public void CancelLoading()
        {

        }

    }

    public sealed class ZipArchiveEntryImageSource : IImageSource, IDisposable
    {
        private readonly ZipArchiveEntry _entry;
        private BitmapImage _image;
        public ZipArchiveEntryImageSource(ZipArchiveEntry entry)
        {
            _entry = entry;
        }

        public void Dispose()
        {
            CancelLoading();
        }

        public string Name => _entry.Name;
        public bool IsImageGenerated => _image != null;

        static FastAsyncLock _Lock = new FastAsyncLock();
        CancellationTokenSource _cts = new CancellationTokenSource();
        public async Task<BitmapImage> GetOrCacheImageAsync()
        {
            var ct = _cts.Token;
//            using (await ImageCollectionViewerPageViewModel.ProcessLock.LockAsync(ct))
            {
                if (_image != null) { return _image; }

                using (var entryStream = _entry.Open())
                using (var memoryStream = entryStream.ToMemoryStream())
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.SetSource(memoryStream.AsRandomAccessStream());
                    return _image = bitmapImage;
                }
            }
        }


        public void CancelLoading()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        
    }
}
