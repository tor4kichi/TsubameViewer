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
                return new NavigationParameters((PageNavigationConstants.Path, Uri.EscapeDataString(PageNavigationConstants.MakeStorageItemIdWithPage(vm.Item.StorageItem.Path, vm.Name))));
            }
            else if (vm.Type == StorageItemTypes.ArchiveFolder)
            {
                var archiveFolderImageSource = vm.Item as ArchiveDirectoryImageSource;

                return new NavigationParameters((PageNavigationConstants.Path, Uri.EscapeDataString(PageNavigationConstants.MakeStorageItemIdWithArchiveFolder(vm.Item.StorageItem.Path, archiveFolderImageSource.Path))));
            }
            else
            {
                return new NavigationParameters((PageNavigationConstants.Path, Uri.EscapeDataString(vm.Item.StorageItem.Path)));
            }
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

        public StorageItemViewModel(SourceStorageItemsRepository sourceStorageItemsRepository, BookmarkManager bookmarkManager) 
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _bookmarkManager = bookmarkManager;

#if WINDOWS_UWP
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                // Load design-time books.
                Name = "テスト";
            }
#endif
        }

        public bool IsSourceStorageItem => _sourceStorageItemsRepository.IsSourceStorageItem(Path);


        public StorageItemViewModel() { }

        public StorageItemViewModel(IImageSource item, SourceStorageItemsRepository sourceStorageItemsRepository, BookmarkManager bookmarkManager)
             : this(sourceStorageItemsRepository, bookmarkManager)
        {
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
