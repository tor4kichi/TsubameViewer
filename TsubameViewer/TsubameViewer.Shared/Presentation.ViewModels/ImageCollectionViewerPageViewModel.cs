using Microsoft.Toolkit.Uwp.Helpers;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
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
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uno.Extensions;
using Uno.Threading;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;
using Windows.Storage.Streams;
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

        private IImageSource _CurrentImage;
        public IImageSource CurrentImage
        {
            get { return _CurrentImage; }
            private set { SetProperty(ref _CurrentImage, value); }
        }

        private int _currentImageIndex;
        public int CurrentImageIndex
        {
            get => _currentImageIndex;
            set => SetProperty(ref _currentImageIndex, value);
        }


        CompositeDisposable _navigationDisposables;

        public ImageCollectionViewerPageViewModel()
        {

        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _leavePageCancellationTokenSource.Cancel();
            _navigationDisposables.Dispose();

            base.OnNavigatedFrom(parameters);
        }


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

                    Images = null;
                    CurrentImage = null;
                    CurrentImageIndex = 0;

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

                    Images = null;
                    CurrentImage = null;
                    CurrentImageIndex = 0;

                    _currentFolderItem = await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);

                    isPathChanged = true;
                }
            }

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();

            // 以下の場合に表示内容を更新する
            //    1. 表示フォルダが変更された場合
            //    2. 前回の更新が未完了だった場合
            if (isTokenChanged || isPathChanged)
            {    
                await RefreshItems(_leavePageCancellationTokenSource.Token);
            }

            this.ObserveProperty(x => x.CurrentImageIndex)
                .Subscribe(async _ =>
                {
                    if (Images == null || Images.Length <= 1) { return; }

                    _loadingCts?.Cancel();
                    _loadingCts?.Dispose();
                    _loadingCts = new CancellationTokenSource();
                    try
                    {
                        using (await _loadingLock.LockAsync(_loadingCts.Token))
                        {
                            foreach (var index in Enumerable.Range(_currentImageIndex, 3))
                            {
                                if (Images.Length <= index) { return; }

                                var image = Images[index];
                                if (!image.IsImageGenerated)
                                {
                                    await Images[index].GenerateBitmapSourceAsync();
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
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
            CurrentImage = Images[CurrentImageIndex];

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();

            Debug.WriteLine(CurrentImage?.Name);
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
            CurrentImage = Images[CurrentImageIndex];

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();

            Debug.WriteLine(CurrentImage?.Name);
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
                            index++;

                            if (item.Name == file.Name)
                            {
                                CurrentImage = item;
                                CurrentImageIndex = index;
                            }
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
                        CurrentImage = Images.FirstOrDefault();
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
                    CurrentImage = Images.FirstOrDefault();
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
    }


    public interface IImageSource
    {
        string Name { get; }
        Task<BitmapImage> GenerateBitmapSourceAsync();
        bool IsImageGenerated { get; }
        void CancelLoading();
    }


    public sealed class ArchiveEntryImageSource : IImageSource, IDisposable
    {
        private MemoryStream _imageStream;
        private BitmapImage _image;

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

        public async Task<BitmapImage> GenerateBitmapSourceAsync()
        {
            if (_image != null) { return _image; }

            _image = new BitmapImage();
            using (var stream = _imageStream.AsRandomAccessStream())
            {
                await _image.SetSourceAsync(stream);
            }
            _imageStream = null;
            return _image;
        }

        public void CancelLoading()
        {

        }
    }

    public sealed class StorageFileImageSource : IImageSource
    {
        private readonly StorageFile _file;
        private BitmapImage _image;

        public StorageFileImageSource(StorageFile file)
        {
            _file = file;
        }

        public string Name => _file.Name;
        public bool IsImageGenerated => _image != null;

        public async Task<BitmapImage> GenerateBitmapSourceAsync()
        {
            if (_image != null) { return _image; }

            _image = new BitmapImage();
            using (var fileStream = await _file.OpenReadAsync())
            {
                await _image.SetSourceAsync(fileStream);
            }
            return _image;
        }

        public void CancelLoading()
        {

        }
    }

    public sealed class ZipArchiveEntryImageSource : IImageSource
    {
        private readonly ZipArchiveEntry _entry;
        private BitmapImage _image = null;

        public ZipArchiveEntryImageSource(ZipArchiveEntry entry)
        {
            _entry = entry;
        }

        public string Name => _entry.Name;
        public bool IsImageGenerated => _image != null;

        static FastAsyncLock _Lock = new FastAsyncLock();
        CancellationTokenSource _cts;
        public async Task<BitmapImage> GenerateBitmapSourceAsync()
        {
            _cts ??= new CancellationTokenSource();
            using (await _Lock.LockAsync(_cts.Token))
            {
                if (_image != null) { return _image; }

                var ct = _cts.Token;
                using (InMemoryRandomAccessStream ms = new InMemoryRandomAccessStream())
                using (var entryStream = _entry.Open())
                {
                    await entryStream.CopyToAsync(ms.AsStream(), ct);
                    await ms.FlushAsync();
                    ms.Seek(0);
                    _image = new BitmapImage();
                    _image.SetSource(ms);
                    return _image;
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
