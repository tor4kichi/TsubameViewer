using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.IO;
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

        public StorageItemViewModel(IStorageItem item, string token, ThumbnailManager thumbnailManager, FolderListingSettings folderListingSettings)
             : this(thumbnailManager, folderListingSettings)
        {
            Item = item;
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
        }

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private static FastAsyncLock _lock = new FastAsyncLock();

        public bool IsImageGenerated => throw new NotImplementedException();
        public async Task<BitmapImage> GenerateBitmapImageAsync()
        {
            using var releaser = await _lock.LockAsync(_cts.Token);
            if (Item is StorageFile file)
            {
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType))
                {
                    if (!_folderListingSettings.IsImageFileThumbnailEnabled) { return null; }

                    using (var stream = await file.OpenStreamForReadAsync())
                    {
                        var image = new BitmapImage();
                        image.DecodePixelWidth = CurrentFileDisplayMode switch
                        {
                            FileDisplayMode.Line => 1,
                            FileDisplayMode.Small => 96,
                            FileDisplayMode.Midium => 180,
                            FileDisplayMode.Large => 300,
                            _ => throw new NotSupportedException()
                        };
                        image.SetSource(stream.AsRandomAccessStream());
                        return image;
                    }
                }
                else if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType))
                {
                    if (!_folderListingSettings.IsArchiveFileThumbnailEnabled) { return null; }

                    var uri = await _thumbnailManager.GetArchiveThumbnailAsync(file);
                    if (uri == null) { return null; }
                    var image = new BitmapImage(uri);
                    image.DecodePixelWidth = 320;
                    return image;
                }
            }
            else if (Item is StorageFolder folder)
            {
                if (!_folderListingSettings.IsFolderThumbnailEnabled) { return null; }

                var uri = await _thumbnailManager.GetFolderThumbnailAsync(folder);
                if (uri == null) { return null; }
                var image = new BitmapImage(uri);
                image.DecodePixelWidth = 320;
                return image;
            }

            return new BitmapImage();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
