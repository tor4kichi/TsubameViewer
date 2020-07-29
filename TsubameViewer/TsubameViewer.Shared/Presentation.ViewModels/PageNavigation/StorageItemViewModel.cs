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
using Uno.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

    public sealed class StorageItemViewModel : BindableBase, IImageGenerater, IDisposable
    {
        public static FileDisplayMode CurrentFileDisplayMode { get; set; }

        #region Navigation Parameters

        public static async ValueTask<INavigationParameters> CreatePageParameterAsync(StorageItemViewModel vm)
        {
            var item = await StorageApplicationPermissions.FutureAccessList.GetItemAsync(vm.Token);
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
            var item = await StorageApplicationPermissions.FutureAccessList.GetItemAsync(vm.Token);
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

        #endregion

        public StorageItemViewModel(ThumbnailManager thumbnailManager, FolderListingSettings folderListingSettings) 
        {
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

        public StorageItemViewModel(IStorageItem item, string token, ThumbnailManager thumbnailManager, FolderListingSettings folderListingSettings)
             : this(thumbnailManager, folderListingSettings)
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

        private uint _ImageWidth = 32; // ItemsRepeaterのレイアウト計算バグ回避のため1以上で初期化
        public uint ImageWidth
        {
            get { return _ImageWidth; }
            private set { SetProperty(ref _ImageWidth, value); }
        }

        private BitmapImage _image;
        public BitmapImage Image
        {
            get { return _image; }
            private set { SetProperty(ref _image, value); }
        }


        private StorageItemTypes _Type;
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

        private CancellationTokenSource _cts;
        private static SemaphoreSlim _loadinLock = new SemaphoreSlim(3, 3);

        public async Task<BitmapImage> GenerateBitmapImageAsync(CancellationToken ct)
        {
            if (Item is StorageFile file)
            {
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType))
                {
                    if (!_folderListingSettings.IsImageFileThumbnailEnabled) { return null; }

                    //var image = new BitmapImage(new Uri(file.Path, UriKind.Absolute));

                    var requestSize = CurrentFileDisplayMode switch
                    {
                        FileDisplayMode.Line => 1,
                        FileDisplayMode.Small => ListingImageConstants.SmallFileThumbnailImageHeight,
                        FileDisplayMode.Midium => ListingImageConstants.MidiumFileThumbnailImageHeight,
                        FileDisplayMode.Large => ListingImageConstants.LargeFileThumbnailImageHeight,
                        _ => throw new NotSupportedException()
                    };

                    if (requestSize < ImageWidth)
                    {
                        requestSize = (int)ImageWidth;
                    }

                    using (var thumbImage = await file.GetScaledImageAsThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, (uint)requestSize))
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

                    if (ImageWidth == 0)
                    {
                        var ratio = CurrentFileDisplayMode switch
                        {
                            FileDisplayMode.Line => 1,
                            FileDisplayMode.Small => ListingImageConstants.SmallFileThumbnailImageHeight / (float)image.PixelHeight,
                            FileDisplayMode.Midium => ListingImageConstants.MidiumFileThumbnailImageHeight / (float)image.PixelHeight,
                            FileDisplayMode.Large => ListingImageConstants.LargeFileThumbnailImageHeight / (float)image.PixelHeight,
                            _ => throw new NotSupportedException(),
                        };

                        ImageWidth = (uint)Math.Floor(image.PixelWidth * ratio);
                    }

                    if (image.PixelWidth > image.PixelHeight)
                    {
                        image.DecodePixelHeight = CurrentFileDisplayMode switch
                        {
                            FileDisplayMode.Line => 1,
                            FileDisplayMode.Small => ListingImageConstants.SmallFileThumbnailImageHeight,
                            FileDisplayMode.Midium => ListingImageConstants.MidiumFileThumbnailImageHeight,
                            FileDisplayMode.Large => ListingImageConstants.LargeFileThumbnailImageHeight,
                            _ => throw new NotSupportedException()
                        };
                    }
                    else
                    {
                        image.DecodePixelWidth = CurrentFileDisplayMode switch
                        {
                            FileDisplayMode.Line => 1,
                            FileDisplayMode.Small => ListingImageConstants.SmallFileThumbnailImageWidth,
                            FileDisplayMode.Midium => ListingImageConstants.MidiumFileThumbnailImageWidth,
                            FileDisplayMode.Large => ListingImageConstants.LargeFileThumbnailImageWidth,
                            _ => throw new NotSupportedException()
                        };
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

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            Image = null;
        }

        public void StopImageLoading()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private bool _isAppearingRequestButLoadingCancelled;

        private FileDisplayMode? _prevFileDisplayMode;

        bool isInitialized = false;
        public async void Initialize()
        {
            if (isInitialized) { return; }

            try
            {
                isInitialized = true;
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                var ct = _cts.Token;
                ct.ThrowIfCancellationRequested();

                await _loadinLock.WaitAsync(ct);

                try
                {
                    if (Type == StorageItemTypes.Image
                        && CurrentFileDisplayMode == FileDisplayMode.Line
                        )
                    {
                        return;
                    }

                    if (Image != null && _prevFileDisplayMode == CurrentFileDisplayMode)
                    {
                        return;
                    }

                    ct.ThrowIfCancellationRequested();

                    _isAppearingRequestButLoadingCancelled = false;

                    Image = await GenerateBitmapImageAsync(ct);
                }
                finally
                {
                    _loadinLock.Release();
                }

                Debug.WriteLine("Thumbnail Load: " + Name);
                _prevFileDisplayMode = CurrentFileDisplayMode;
            }
            catch (OperationCanceledException)
            {
                isInitialized = false;
                _isAppearingRequestButLoadingCancelled = true;
                return;
            }
        }

        public async Task InitializeAsync(CancellationToken ct)
        {
            isInitialized = false;
            if (Type == StorageItemTypes.Image)
            {
                using (var thubm = await (Item as StorageFile).GetScaledImageAsThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, ListingImageConstants.MidiumFileThumbnailImageHeight).AsTask(ct))
                {
                    var ratio = CurrentFileDisplayMode switch
                    {
                        FileDisplayMode.Line => 1,
                        FileDisplayMode.Small => ListingImageConstants.SmallFileThumbnailImageHeight / (float)thubm.OriginalHeight,
                        FileDisplayMode.Midium => ListingImageConstants.MidiumFileThumbnailImageHeight / (float)thubm.OriginalHeight,
                        FileDisplayMode.Large => ListingImageConstants.LargeFileThumbnailImageHeight / (float)thubm.OriginalHeight,
                        _ => throw new NotSupportedException(),
                    };

                    ImageWidth = (uint)Math.Floor(thubm.OriginalWidth * ratio);
                }
            }
            else if (Type == StorageItemTypes.Archive)
            {
                var size = _thumbnailManager.GetThubmnailOriginalSize(Item as StorageFile);
                if (size.HasValue)
                {
                    var thumbHeight = size.Value.Height;
                    var thumbWidth = size.Value.Width;

                    var ratio = CurrentFileDisplayMode switch
                    {
                        FileDisplayMode.Line => 1,
                        FileDisplayMode.Small => ListingImageConstants.SmallFileThumbnailImageHeight / (float)thumbHeight,
                        FileDisplayMode.Midium => ListingImageConstants.MidiumFileThumbnailImageHeight / (float)thumbHeight,
                        FileDisplayMode.Large => ListingImageConstants.LargeFileThumbnailImageHeight / (float)thumbHeight,
                        _ => throw new NotSupportedException(),
                    };

                    ImageWidth = (uint)Math.Floor(thumbWidth * ratio);
                }
            }
        }

        public void RestoreThumbnailLoadingTask()
        {
            if (_isAppearingRequestButLoadingCancelled)
            {
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
