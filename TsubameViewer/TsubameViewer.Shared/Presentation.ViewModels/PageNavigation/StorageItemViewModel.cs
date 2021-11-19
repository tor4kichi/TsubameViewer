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
    using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

    public record StorageItemToken(string RootItemPath, string TokenString)
    {
    }


    public sealed class StorageItemViewModel : BindableBase, IDisposable
    {
        #region Navigation Parameters

        public static NavigationParameters CreatePageParameter(StorageItemViewModel vm)
        {
            var escapedPath = Uri.EscapeDataString(vm.Item.StorageItem.Path);
            if (vm.Type == StorageItemTypes.Image)
            {
                return new NavigationParameters((PageNavigationConstants.Path, escapedPath), (PageNavigationConstants.PageName, Uri.EscapeDataString(vm.Name)));
            }
            else if (vm.Type == StorageItemTypes.ArchiveFolder)
            {
                var archiveFolderImageSource = vm.Item as ArchiveDirectoryImageSource;
                
                return CreateArchiveFolderPageParameter(escapedPath, Uri.EscapeDataString(archiveFolderImageSource.Path));
            }
            else
            {
                return new NavigationParameters((PageNavigationConstants.Path, escapedPath));
            }
        }

        public static NavigationParameters CreateArchiveFolderPageParameter(string filePath, string archiveFolderPath)
        {
            return new NavigationParameters((PageNavigationConstants.Path, filePath), (PageNavigationConstants.ArchiveFolderName, archiveFolderPath));
        }

        #endregion


        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly BookmarkManager _bookmarkManager;

        public IImageSource Item { get; }


        public StorageItemToken Token { get; }

        
        public string Name { get; }

        
        public string Path { get; }

        public DateTimeOffset DateCreated { get; }

        private BitmapImage _image;
        public BitmapImage Image
        {
            get { return _image; }
            private set { SetProperty(ref _image, value); }
        }

        public StorageItemTypes Type { get; }

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private static FastAsyncLock _imageLoadingLock = new FastAsyncLock();

        private double _ReadParcentage;
        public double ReadParcentage
        {
            get { return _ReadParcentage; }
            set { SetProperty(ref _ReadParcentage, value); }
        }

        public StorageItemViewModel(SourceStorageItemsRepository sourceStorageItemsRepository, FolderListingSettings folderListingSettings, BookmarkManager bookmarkManager) 
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _folderListingSettings = folderListingSettings;
            _bookmarkManager = bookmarkManager;

#if WINDOWS_UWP
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                // Load design-time books.
                Name = "テスト";
            }
#endif
        }



        public StorageItemViewModel() { }

        public StorageItemViewModel(IImageSource item, StorageItemToken token, SourceStorageItemsRepository sourceStorageItemsRepository, FolderListingSettings folderListingSettings, BookmarkManager bookmarkManager)
             : this(sourceStorageItemsRepository, folderListingSettings, bookmarkManager)
        {
            Item = item;
            DateCreated = Item.DateCreated;
            Token = token;
            
            Name = Item.Name;
            Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(item);
            if (item is StorageItemImageSource storageItemImageSource)
            {
                Path = storageItemImageSource.Path;
            }

            UpdateLastReadPosition();
        }

        public void ClearImage()
        {
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
            // do nothing.
        }

        private bool _isAppearingRequestButLoadingCancelled;

        bool _isInitialized = false;
        public async void Initialize()
        {
            var ct = _cts.Token;
            try
            {
                using var _ = await _imageLoadingLock.LockAsync(ct);

                if (Item == null) { return; }
                if (_isInitialized) { return; }

                if (Type == StorageItemTypes.Image && !_folderListingSettings.IsImageFileThumbnailEnabled) { return; }
                if (Type == StorageItemTypes.Archive && !_folderListingSettings.IsArchiveFileThumbnailEnabled) { return; }
                if (Type == StorageItemTypes.Folder && !_folderListingSettings.IsFolderThumbnailEnabled) { return; }

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

                    var bitmapImage = new BitmapImage();
                    bitmapImage.AutoPlay = false;                    
                    //bitmapImage.DecodePixelHeight = Models.Domain.FolderItemListing.ListingImageConstants.LargeFileThumbnailImageHeight;
                    await bitmapImage.SetSourceAsync(stream).AsTask(ct);
                    Image = bitmapImage;
                }

                _isInitialized = true;
            }
            catch (OperationCanceledException)
            {
                _isInitialized = false;
                _isAppearingRequestButLoadingCancelled = true;
                return;
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
            Item.TryDispose();
            Image = null;
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
