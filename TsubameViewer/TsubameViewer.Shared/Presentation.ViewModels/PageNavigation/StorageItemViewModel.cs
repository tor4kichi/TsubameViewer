using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.ReadingFeature;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Domain.SourceFolders;
using Uno.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml.Media.Imaging;
using Uno.Disposables;
using TsubameViewer.Models.Domain.Albam;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    using static TsubameViewer.Models.Domain.FolderItemListing.ThumbnailManager;
    using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

    public sealed class StorageItemViewModel : BindableBase, IDisposable
    {
        #region Navigation Parameters

        public static NavigationParameters CreatePageParameter(StorageItemViewModel vm)
        {
            if (vm.Type == StorageItemTypes.Image)
            {
                return new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(PageNavigationConstants.MakeStorageItemIdWithPage(vm.Item.StorageItem.Path, vm.Name))));
            }
            else if (vm.Item is ArchiveDirectoryImageSource archiveFolderImageSource)
            {
                return CreatePageParameter(archiveFolderImageSource);
            }
            else if (vm.Item is AlbamImageSource albam)
            {
                return CreatePageParameter(albam);
            }
            else if (vm.Item is AlbamItemImageSource albamItem)
            {
                return CreatePageParameter(albamItem);
            }
            else
            {
                return new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(vm.Item.StorageItem.Path)));
            }
        }

        public static NavigationParameters CreatePageParameter(ArchiveDirectoryImageSource archiveFolderImageSource)
        {
            return new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(PageNavigationConstants.MakeStorageItemIdWithArchiveFolder(archiveFolderImageSource.StorageItem.Path, archiveFolderImageSource.Path))));
        }

        public static NavigationParameters CreatePageParameter(AlbamImageSource albam)
        {
            return new NavigationParameters((PageNavigationConstants.AlbamPathKey, Uri.EscapeDataString(albam.AlbamId.ToString())));
        }

        public static NavigationParameters CreatePageParameter(AlbamItemImageSource albamItem)
        {
            return new NavigationParameters((PageNavigationConstants.AlbamPathKey, Uri.EscapeDataString(PageNavigationConstants.MakeStorageItemIdWithPage(albamItem.AlbamId.ToString(), albamItem.Path))));
        }

        #endregion


        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly BookmarkManager _bookmarkManager;

        public IImageSource Item { get; }
        
        public string Name { get; }

        
        public string Path { get; }

        public DateTimeOffset DateCreated { get; }

        private BitmapImage _image;
        public BitmapImage Image
        {
            get { return _image; }
            set { SetProperty(ref _image, value); }
        }

        private float? _ImageAspectRatioWH;
        public float? ImageAspectRatioWH
        {
            get { return _ImageAspectRatioWH; }
            set { SetProperty(ref _ImageAspectRatioWH, value); }
        }



        public StorageItemTypes Type { get; }

        private CancellationTokenSource _cts;

        private double _ReadParcentage;
        public double ReadParcentage
        {
            get { return _ReadParcentage; }
            set { SetProperty(ref _ReadParcentage, value); }
        }

        public bool IsSourceStorageItem => _sourceStorageItemsRepository?.IsSourceStorageItem(Path) ?? false;


        public StorageItemViewModel(string name, StorageItemTypes storageItemTypes)         
        {
            Name = name;
            Type = storageItemTypes;
        }

        public StorageItemViewModel(IImageSource item, SourceStorageItemsRepository sourceStorageItemsRepository, BookmarkManager bookmarkManager)
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _bookmarkManager = bookmarkManager;

            Item = item;
            DateCreated = Item.DateCreated;
            
            Name = Item.Name;
            Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(item);
            if (item is StorageItemImageSource storageItemImageSource)
            {
                Path = storageItemImageSource.Path;
            }

            _ImageAspectRatioWH = Item.GetThumbnailSize()?.RatioWH;

            UpdateLastReadPosition();
        }

        public void ClearImage()
        {
            if (_disposed) { return; }

#if DEBUG
            if (Image == null)
            {
                Debug.WriteLine("Thumbnail Cancel: " + Name);
            }
#endif

            if (_cts?.IsCancellationRequested == false)
            {
                _cts.Cancel();
            }

            Image = null;
        }

        public void StopImageLoading()
        {
            if (_disposed) { return; }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private bool _isAppearingRequestButLoadingCancelled;
        private readonly static Models.Infrastructure.AsyncLock _asyncLock = new (5);

        bool _isInitialized = false;
        public async void Initialize()
        {
            if (_isInitialized) { return; }
            if (_disposed) { return; }

            // ItemsRepeaterの読み込み順序が対応するためキャンセルが必要
            // ItemsRepeaterは表示しない先の方まで一度サイズを確認するために読み込みを掛けようとする
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                using var lockReleaser = await _asyncLock.LockAsync(ct);

                if (Item == null) { return; }

                if (ct.IsCancellationRequested)
                {
                    _isAppearingRequestButLoadingCancelled = true;
                    return; 
                }

                ct.ThrowIfCancellationRequested();

                _isAppearingRequestButLoadingCancelled = false;

                using (var stream = await Task.Run(async () => await Item.GetThumbnailImageStreamAsync(ct)))
                {
                    if (stream is null || stream.Size == 0) { return; }

                    stream.Seek(0);
                    var bitmapImage = new BitmapImage();
                    bitmapImage.AutoPlay = false;
                    //bitmapImage.DecodePixelHeight = Models.Domain.FolderItemListing.ListingImageConstants.LargeFileThumbnailImageHeight;
                    await bitmapImage.SetSourceAsync(stream).AsTask(ct);
                    Image = bitmapImage;
                }

                ImageAspectRatioWH ??= Item.GetThumbnailSize()?.RatioWH;

                _isInitialized = true;
            }
            catch (OperationCanceledException)
            {
                _isInitialized = false;
                _isAppearingRequestButLoadingCancelled = true;
            }
        }

        public void UpdateLastReadPosition()
        {
            var parcentage = _bookmarkManager.GetBookmarkLastReadPositionInNormalized(Path);
            ReadParcentage = parcentage >= 0.90f ? 1.0 : parcentage;
        }

        public void RestoreThumbnailLoadingTask()
        {
            if (_isAppearingRequestButLoadingCancelled)
            {
                if (Image != null)
                {
                    return;
                }

                _isInitialized = false;
                
                Initialize();
            }
        }

        public void ThumbnailChanged()
        {
            Image = null;
            _isInitialized = false;
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            
            _disposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
            Item.TryDispose();
            Image = null;
        }
        bool _disposed;
    }
}
