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
using TsubameViewer.Models.Domain.Bookmark;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Domain.SourceFolders;
using Uno.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

    public sealed class StorageItemViewModel : BindableBase, IDisposable
    {
        #region Navigation Parameters

        public static async ValueTask<INavigationParameters> CreatePageParameterAsync(StorageItemViewModel vm)
        {
            var item = await vm.GetTokenStorageItem();
            if (item is IStorageFolder folder)
            {
                var escapedPath = Uri.EscapeDataString(GetSubtractPath(folder, vm.Item.StorageItem));
                if (vm.Type == StorageItemTypes.Image)
                {
                    return new NavigationParameters(("token", vm.Token), ("path", escapedPath), ("pageName", Uri.EscapeDataString(vm.Name)));
                }
                else
                {
                    return new NavigationParameters(("token", vm.Token), ("path", escapedPath));
                }
            }
            else if (item is IStorageFile file)
            {
                return new NavigationParameters(("token", vm.Token), ("path", Uri.EscapeDataString(file.Name)));
            }

            throw new NotSupportedException();
        }

        public static async Task<string> GetRawSubtractPath(StorageItemViewModel vm)
        {
            var item = await vm.GetTokenStorageItem();
            if (item is IStorageFolder folder)
            {
                if (vm.Item is StorageItemImageSource storageItemImageSource)
                {
                    return GetSubtractPath(folder, storageItemImageSource.StorageItem);
                }
            }

            throw new NotSupportedException();
        }

        public static string GetSubtractPath(IStorageFolder lt, IStorageItem rt)
        {
            if (!rt.Path.StartsWith(lt.Path))
            {
                throw new ArgumentException("差分パスの取得には親子関係にあるフォルダとアイテムが必要です。");
            }

            return rt.Path.Substring(lt.Path.Length);
        }


        internal async Task<IStorageItem> GetTokenStorageItem()
        {
            return await _sourceStorageItemsRepository.GetItemAsync(Token);
        }

        #endregion


        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly BookmarkManager _bookmarkManager;

        public IImageSource Item { get; }



        public string Token { get; }

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
        private static SemaphoreSlim _loadinLock = new SemaphoreSlim(3, 3);


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

        public StorageItemViewModel(IImageSource item, string token, SourceStorageItemsRepository sourceStorageItemsRepository, FolderListingSettings folderListingSettings, BookmarkManager bookmarkManager)
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
            _cts?.Cancel();
        }

        private bool _isAppearingRequestButLoadingCancelled;

        bool _isInitialized = false;
        public async void Initialize()
        {
            if (Item == null) { return; }
            if (_isInitialized) { return; }

            if (Type == StorageItemTypes.Image && !_folderListingSettings.IsImageFileThumbnailEnabled) { return; }
            if (Type == StorageItemTypes.Archive && !_folderListingSettings.IsArchiveFileThumbnailEnabled) { return; }
            if (Type == StorageItemTypes.Folder && !_folderListingSettings.IsFolderThumbnailEnabled) { return; }

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
                    Image = await Item.GenerateThumbnailBitmapImageAsync(ct);

                    await Task.Delay(10);
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

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
