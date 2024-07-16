using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.ViewModels.SourceFolders;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.ViewModels;

using StorageItemTypes = TsubameViewer.Core.Models.StorageItemTypes;

public sealed class StorageItemViewModel : ObservableObject, IDisposable, IStorageItemViewModel
{
    private readonly IMessenger _messenger;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly LocalBookmarkRepository _bookmarkManager;
    private readonly ThumbnailImageManager _thumbnailImageService;
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
    public bool StorageItemTypesIsFolder => Type is StorageItemTypes.Folder;

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

    public StorageItemViewModel(
        IImageSource item,
        IMessenger messenger,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        LocalBookmarkRepository bookmarkManager,
        ThumbnailImageManager thumbnailImageService,
        AlbamRepository albamRepository,
        SelectionContext selectionContext = null
        )
    {
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _bookmarkManager = bookmarkManager;
        _thumbnailImageService = thumbnailImageService;
        _albamRepository = albamRepository;
        Selection = selectionContext;
        Item = item;
        _messenger = messenger;
        DateCreated = Item.DateCreated;

        Name = Item.Name;
        Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(item);
        Path = item.Path;

        _ImageAspectRatioWH = _thumbnailImageService.GetCachedThumbnailSize(Item)?.RatioWH;

        UpdateLastReadPosition();
        _isFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, item.Path);
    }


    private bool _isRequestImageLoading = false;
    private bool _isRequireLoadImageWhenRestored = false;
    public void StopImageLoading()
    {
        _isRequestImageLoading = false;
    }

    private readonly static Core.AsyncLock _asyncLock = new(Math.Max(1, Environment.ProcessorCount / 2));

    public async ValueTask PrepareImageSizeAsync(CancellationToken ct)
    {
        if (ImageAspectRatioWH == null)
        {
            var size = await _thumbnailImageService.GetEnsureThumbnailSizeAsync(Item, ct);
            ImageAspectRatioWH = size.RatioWH;
        }
    }

    bool _isInitialized = false;
    public async ValueTask InitializeAsync(CancellationToken ct)
    {
        // ItemsRepeaterの読み込み順序が対応するためキャンセルが必要
        // ItemsRepeaterは表示しない先の方まで一度サイズを確認するために読み込みを掛けようとする
        _isRequestImageLoading = true;

        try
        {
            using var _ = await _asyncLock.LockAsync(ct);

            if (_isInitialized) { return; }
            if (_disposed) { return; }
            if (Item == null) { return; }
            if (_isRequestImageLoading is false) { return; }

            // Note: Task.Run() で包まないと一部環境でハングアップする可能性あり
            using (var stream = await Task.Run(async () => await _thumbnailImageService.GetThumbnailImageStreamAsync(Item, ct: ct), ct))
            {
                if (stream is null || stream.Size == 0) { return; }

                stream.Seek(0);
                var bitmapImage = new BitmapImage();
                bitmapImage.AutoPlay = false;
                await bitmapImage.SetSourceAsync(stream).AsTask(ct);
                Image = bitmapImage;
            }

            ImageAspectRatioWH ??= _thumbnailImageService.GetCachedThumbnailSize(Item)?.RatioWH;

            _isRequireLoadImageWhenRestored = false;
            _isInitialized = true;
        }
        catch (OperationCanceledException)
        {
            _isRequireLoadImageWhenRestored = true;
            _isInitialized = false;
        }
        catch (NotSupportedImageFormatException ex)
        {
            // 0xC00D5212
            // "コンテンツをエンコードまたはデコードするための適切な変換が見つかりませんでした。"
            _isRequireLoadImageWhenRestored = true;
            _isInitialized = false;
            _messenger.Send<RequireInstallImageCodecExtensionMessage>(new(ex.FileType));
        }
    }

    public void UpdateLastReadPosition()
    {
        var parcentage = _bookmarkManager.GetBookmarkLastReadPositionInNormalized(Path);
        ReadParcentage = parcentage >= 0.90f ? 1.0 : parcentage;
    }

    public void RestoreThumbnailLoadingTask(CancellationToken ct)
    {
        IsFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, Path);

        if (_isRequireLoadImageWhenRestored && Image == null)
        {
            InitializeAsync(ct);
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
        (Item as IDisposable)?.Dispose();
        _image = null;
    }
    bool _disposed;
}
