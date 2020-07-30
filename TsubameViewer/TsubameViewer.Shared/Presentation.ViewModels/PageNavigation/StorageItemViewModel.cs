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
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.SourceFolders;
using Uno.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

    public sealed class StorageItemViewModel : BindableBase, IImageGenerater, IDisposable
    {

        #region Navigation Parameters

        public static async ValueTask<INavigationParameters> CreatePageParameterAsync(StorageItemViewModel vm)
        {
            var item = await vm.GetTokenStorageItem();
            if (item is IStorageFolder folder)
            {
                var path = GetSubtractPath(folder, vm.Item);
                return new NavigationParameters(("token", vm.Token), ("path", Uri.EscapeDataString(path)));
            }
            else if (item is IStorageFile file)
            {
                return new NavigationParameters(("token", vm.Token), ("path", Uri.EscapeDataString(file.Name)));
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public static async Task<string> GetRawSubtractPath(StorageItemViewModel vm)
        {
            var item = await vm.GetTokenStorageItem();
            if (item is IStorageFolder folder)
            {
                return GetSubtractPath(folder, vm.Item);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private static string GetSubtractPath(IStorageFolder lt, IStorageItem rt)
        {
            if (!rt.Path.StartsWith(lt.Path))
            {
                throw new ArgumentException("差分パスの取得には親子関係にあるフォルダとアイテムが必要です。");
            }

            return rt.Path.Substring(lt.Path.Length);
        }


        internal async Task<IStorageItem> GetTokenStorageItem()
        {
            var item = await _sourceStorageItemsRepository.GetStorageItemAsync(Token);
            return item.item;
        }

        #endregion

        public StorageItemViewModel(SourceStorageItemsRepository sourceStorageItemsRepository, ThumbnailManager thumbnailManager, FolderListingSettings folderListingSettings) 
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _thumbnailManager = thumbnailManager;
            _folderListingSettings = folderListingSettings;

#if WINDOWS_UWP
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                // Load design-time books.
                _Name = "テスト";
            }
#endif
        }

        public StorageItemViewModel() { }

        public StorageItemViewModel(IStorageItem item, string token, SourceStorageItemsRepository sourceStorageItemsRepository, ThumbnailManager thumbnailManager, FolderListingSettings folderListingSettings)
             : this(sourceStorageItemsRepository, thumbnailManager, folderListingSettings)
        {
            Item = item;
            _DateCreated = item.DateCreated;
            Token = token;
            _Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(item);
            _Name = Item.Name;
            _Path = Item.Path;
        }

        public IStorageItem Item { get; private set; }
        public string Token { get; private set; }

        private string _Name;
        public string Name
        {
            get { return _Name; }
            set { SetProperty(ref _Name, value); }
        }

        private string _Path;
        public string Path
        {
            get { return _Path; }
            set { SetProperty(ref _Path, value); }
        }

        private DateTimeOffset _DateCreated;
        public DateTimeOffset DateCreated
        {
            get { return _DateCreated; }
            set { SetProperty(ref _DateCreated, value); }
        }

        private BitmapImage _image;
        public BitmapImage Image
        {
            get { return _image; }
            private set { SetProperty(ref _image, value); }
        }

        private StorageItemTypes _Type;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderListingSettings _folderListingSettings;

        public StorageItemTypes Type
        {
            get { return _Type; }
            private set { SetProperty(ref _Type, value); }
        }


        public void Setup(IStorageItem item, string token)
        {
            Item = item;
            Token = token;
            Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(item);
            Name = Item.Name;
            Path = Item.Path;
            DateCreated = Item.DateCreated;
        }

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private static SemaphoreSlim _loadinLock = new SemaphoreSlim(3, 3);

        public async Task<BitmapImage> GenerateBitmapImageAsync(CancellationToken ct)
        {
            if (Item is StorageFile file)
            {
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType))
                {
                    if (!_folderListingSettings.IsImageFileThumbnailEnabled) { return null; }

                    //var image = new BitmapImage(new Uri(file.Path, UriKind.Absolute));
                    using (var thumbImage = await file.GetScaledImageAsThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, (uint)ListingImageConstants.LargeFileThumbnailImageHeight))
                    {
                        var image = new BitmapImage();
                        image.SetSource(thumbImage);
                        return image;
                    }
                }
                else if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType))
                {
                    if (!_folderListingSettings.IsArchiveFileThumbnailEnabled) { return null; }

                    var thumbnailFile = await _thumbnailManager.GetArchiveThumbnailAsync(file);
                    if (thumbnailFile == null) { return null; }
                    var image = new BitmapImage();

                    using (var stream = await thumbnailFile.OpenStreamForReadAsync())
                    {
                        image.SetSource(stream.AsRandomAccessStream());
                    }

                    return image;
                }
            }
            else if (Item is StorageFolder folder)
            {
                if (!_folderListingSettings.IsFolderThumbnailEnabled) { return null; }

                var uri = await _thumbnailManager.GetFolderThumbnailAsync(folder);
                if (uri == null) { return null; }
                var image = new BitmapImage(uri);
                return image;
            }

            return new BitmapImage();
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
            _cts?.Cancel();
        }

        private bool _isAppearingRequestButLoadingCancelled;

        bool _isInitialized = false;
        public async void Initialize()
        {
            if (_isInitialized) { return; }

            try
            {
                if (_cts.IsCancellationRequested) 
                {
                    _isAppearingRequestButLoadingCancelled = true;
                    return; 
                }

                _isInitialized = true;
                var ct = _cts.Token;
                ct.ThrowIfCancellationRequested();

                await _loadinLock.WaitAsync(ct);

                try
                {
                    ct.ThrowIfCancellationRequested();

                    _isAppearingRequestButLoadingCancelled = false;

                    Image = await GenerateBitmapImageAsync(ct);
                }
                finally
                {
                    _loadinLock.Release();
                }

                Debug.WriteLine("Thumbnail Load: " + Name);
            }
            catch (OperationCanceledException)
            {
                _isInitialized = false;
                _isAppearingRequestButLoadingCancelled = true;
                return;
            }
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

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
