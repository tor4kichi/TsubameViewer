using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Core.Models;
using TsubameViewer.ViewModels.SourceFolders;
using Windows.UI.Xaml.Media.Imaging;
using System.Diagnostics;

#nullable enable
namespace TsubameViewer.ViewModels;

public sealed partial class LazyFolderOrArchiveFileViewModel : ObservableObject, IStorageItemViewModel
{
    private readonly IImageCollectionContext _imageCollectionContext;
    private readonly int _itemIndex;
    private readonly FileSortType _fileSortType;
    private readonly IMessenger _messenger;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly LocalBookmarkRepository _bookmarkManager;
    private readonly ThumbnailImageManager _thumbnailImageService;
    private readonly AlbamRepository _albamRepository;
    public SelectionContext? Selection { get; }

    [ObservableProperty]
    IImageSource? _item;

    [ObservableProperty]
    string? _name;

    [ObservableProperty]
    string? _path;

    [ObservableProperty]
    DateTimeOffset _dateCreated;

    [ObservableProperty]
    private BitmapImage? _image;

    [ObservableProperty]
    private float? _ImageAspectRatioWH;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    StorageItemTypes _type;

    [ObservableProperty]
    private double _ReadParcentage;

    public bool IsSourceStorageItem => _sourceStorageItemsRepository?.IsSourceStorageItem(Path) ?? false;


    public LazyFolderOrArchiveFileViewModel(
        IImageCollectionContext imageCollectionContext,
        int itemIndex,
        FileSortType fileSortType,
        IMessenger messenger,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        LocalBookmarkRepository bookmarkManager,
        ThumbnailImageManager thumbnailImageService,
        AlbamRepository albamRepository,
        SelectionContext? selectionContext = null
        )
    {
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _bookmarkManager = bookmarkManager;
        _thumbnailImageService = thumbnailImageService;
        _albamRepository = albamRepository;
        Selection = selectionContext;
        _imageCollectionContext = imageCollectionContext;
        _itemIndex = itemIndex;
        _fileSortType = fileSortType;
        _messenger = messenger;

        _type = StorageItemTypes.Archive;
    }


    private bool _isRequestImageLoading = false;
    private bool _isRequireLoadImageWhenRestored = false;
    public void StopImageLoading()
    {
        _isRequestImageLoading = false;
    }

    private readonly static Core.AsyncLock _asyncLock = new(Math.Max(1, Environment.ProcessorCount / 2));

    public ValueTask PrepareImageSizeAsync(CancellationToken ct)
    {
        return new ValueTask();
        //if (Item == null) { return; }

        //if (ImageAspectRatioWH == null)
        //{
        //    var size = await _thumbnailImageService.GetEnsureThumbnailSizeAsync(Item, ct);
        //    ImageAspectRatioWH = size.RatioWH;
        //}
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
            if (_isRequestImageLoading is false) { return; }

            if (Item == null)
            {
                Item = await _imageCollectionContext.GetFolderOrArchiveFileAtAsync(_itemIndex, _fileSortType, ct);
                Name = Item.Name;
                Path = Item.Path;
                DateCreated = Item.DateCreated;
                Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(Item);
                UpdateLastReadPosition();
                IsFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, Item.Path);
            }

            using (var stream = await _thumbnailImageService.GetThumbnailImageStreamAsync(Item, ct: ct))
            {
                if (stream is null || stream.Size == 0) { return; }

                stream.Seek(0);
                var bitmapImage = new BitmapImage();
                bitmapImage.AutoPlay = false;
                await bitmapImage.SetSourceAsync(stream).AsTask(ct);
                Image = bitmapImage;
            }

            //ImageAspectRatioWH ??= _thumbnailImageService.GetCachedThumbnailSize(Item)?.RatioWH;

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
        catch (NotSupportedException)
        {

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
            _ = InitializeAsync(ct);
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
        Image = null;
    }
    bool _disposed;
}
