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
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml.Media.Imaging;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.UseCase;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    using static TsubameViewer.Models.Domain.FolderItemListing.ThumbnailManager;
    using static TsubameViewer.Presentation.ViewModels.ImageListupPageViewModel;
    using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

    public sealed class StorageItemViewModel : ObservableObject, IDisposable
    {
        


        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly BookmarkManager _bookmarkManager;
        private readonly AlbamRepository _albamRepository;

        public IImageSource Item { get; }
        public SelectionContext Selection { get; }
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


        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set => SetProperty(ref _isFavorite, value);
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

        public StorageItemViewModel(IImageSource item, SourceStorageItemsRepository sourceStorageItemsRepository, BookmarkManager bookmarkManager, AlbamRepository albamRepository, SelectionContext selectionContext = null)
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _bookmarkManager = bookmarkManager;
            _albamRepository = albamRepository;
            Selection = selectionContext;            
            Item = item;
            DateCreated = Item.DateCreated;
            
            Name = Item.Name;
            Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(item);
            Path = item.Path;

            _ImageAspectRatioWH = Item.GetThumbnailSize()?.RatioWH;

            UpdateLastReadPosition();
            _isFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, item.Path);
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
            IsFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, Path);

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
            (Item as IDisposable)?.Dispose();
            Image = null;
        }
        bool _disposed;
    }
}
