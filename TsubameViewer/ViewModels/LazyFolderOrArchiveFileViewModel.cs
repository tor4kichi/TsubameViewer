using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.ViewModels.SourceFolders;
using TsubameViewer.Views.Converters;
using Windows.UI.Xaml.Media.Imaging;

#nullable enable
namespace TsubameViewer.ViewModels;

public enum LoadingStatus
{
    None,
    PendingLoad,
    NowLoading,
    Laoded,

    LoadFailed,
}

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
    private float? _imageAspectRatioWH;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    StorageItemTypes _type;

    [ObservableProperty]
    private double _readParcentage;

    public bool IsSourceStorageItem => _sourceStorageItemsRepository?.IsSourceStorageItem(Path) ?? false;

    [ObservableProperty]
    string? _duration;

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

    public bool IsRequestImageLoading => Status == LoadingStatus.NowLoading;
    LoadingStatus _status = LoadingStatus.None;
    public LoadingStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsInitialized));
                OnPropertyChanged(nameof(IsRequestImageLoading));
            }
        }
    }

    public void StopImageLoading()
    {
        if (Status is LoadingStatus.PendingLoad)
        {
            Status = LoadingStatus.None;
        }
    }

    private readonly static Core.AsyncLock _asyncLock = new(Math.Max(1, Environment.ProcessorCount * 4));

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

    public bool IsInitialized => _status == LoadingStatus.Laoded;
    public async ValueTask InitializeAsync(CancellationToken ct)
    {
        // ItemsRepeaterの読み込み順序が対応するためキャンセルが必要
        // ItemsRepeaterは表示しない先の方まで一度サイズを確認するために読み込みを掛けようとする
        var lastStatus = _status;
        if (lastStatus is not LoadingStatus.None and not LoadingStatus.PendingLoad and not LoadingStatus.NowLoading) { return; }

        _status = LoadingStatus.PendingLoad;
        try
        {
            using (await _asyncLock.LockAsync(ct))
            {
                if (IsInitialized) { return; }
                if (_disposed) { return; }
                if (_status is not LoadingStatus.PendingLoad) { return; }

                _status = LoadingStatus.NowLoading;
                if (Item == null)
                {
                    Item = await _imageCollectionContext.GetFolderOrArchiveFileAtAsync(_itemIndex, _fileSortType, ct);
                    Name = Item.Name;
                    Path = Item.Path;
                    DateCreated = Item.DateCreated;
                    Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(Item);
                    UpdateLastReadPosition();
                    IsFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, Item.Path);
                    if (Type == StorageItemTypes.Movie
                        && Item.StorageItem is Windows.Storage.StorageFile file)
                    {
                        var movieProps = await file.Properties.GetVideoPropertiesAsync();
                        Duration = TimeSpanHelper.FormatTimeSpan(movieProps?.Duration ?? TimeSpan.Zero);
                    }
                }

                using (var stream = await Task.Run(async () => await _thumbnailImageService.GetThumbnailImageStreamAsync(Item, ct: ct)))
                {
                    if (stream is null || stream.Length == 0) { return; }
                    if (_status is not LoadingStatus.NowLoading) { return; }

                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    var bitmapImage = new BitmapImage();
                    bitmapImage.AutoPlay = false;
                    await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream()).AsTask(ct);
                    Image = bitmapImage;
                }

                //ImageAspectRatioWH ??= _thumbnailImageService.GetCachedThumbnailSize(Item)?.RatioWH;
                Status = LoadingStatus.Laoded;
            }
        }
        catch (OperationCanceledException)
        {
            Status =  LoadingStatus.NowLoading;
        }
        catch (NotSupportedImageFormatException ex)
        {
            // 0xC00D5212
            // "コンテンツをエンコードまたはデコードするための適切な変換が見つかりませんでした。"
            Status = LoadingStatus.LoadFailed;
            _messenger.Send<RequireInstallImageCodecExtensionMessage>(new(ex.FileType));
        }
        catch (NotSupportedException)
        {
            Status = LoadingStatus.LoadFailed;
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

        if (Status is LoadingStatus.NowLoading)
        {
            Status = LoadingStatus.PendingLoad;
            _ = InitializeAsync(ct);
        }
    }

    public void ThumbnailChanged()
    {
        Image = null;
        Status = LoadingStatus.None;
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        (Item as IDisposable)?.Dispose();
        Image = null;
    }

    public ValueTask EnsureImageSizeRatioAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    bool _disposed;
}
