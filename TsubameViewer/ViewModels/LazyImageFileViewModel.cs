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
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.ViewModels.SourceFolders;
using TsubameViewer.Views.Converters;
using TsubameViewer.Views.Helpers;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using StorageItemTypes = TsubameViewer.Core.Models.StorageItemTypes;
#nullable enable
namespace TsubameViewer.ViewModels;


public sealed partial class LazyImageFileViewModel : ObservableObject, IStorageItemViewModel
{
    private readonly IImageCollectionContext _imageCollectionContext;
    public int Index { get; }
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

    [ObservableProperty]
    string? _duration;

    public bool IsSourceStorageItem => _sourceStorageItemsRepository?.IsSourceStorageItem(Path) ?? false;


    public LazyImageFileViewModel(
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
        Index = itemIndex;
        _fileSortType = fileSortType;
        _messenger = messenger;

        _type = Core.Models.StorageItemTypes.Image;

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

    async ValueTask EnsureStorageItemAsync(CancellationToken ct)
    {
        try
        {
            if (Item == null)
            {
                Item = await _imageCollectionContext.GetImageFileAtAsync(Index, _fileSortType, ct);
                Name = Item.Name;
                Path = Item.Path;
                DateCreated = Item.DateCreated;
                Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(Item);
                UpdateLastReadPosition();
                IsFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, Item.Path);
            }
        }
        catch (DirectoryNotFoundException)
        {
            Status = LoadingStatus.LoadFailed;
            _messenger.Send(new StorageItemNotFoundMessage(Path));
        }
        catch (FileNotFoundException)
        {
            Status = LoadingStatus.LoadFailed;
            _messenger.Send(new StorageItemNotFoundMessage(Path));
        }
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
                await EnsureStorageItemAsync(ct);

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
            Status = LoadingStatus.NowLoading;
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
        //var parcentage = _bookmarkManager.GetBookmarkLastReadPositionInNormalized(Path);
        //ReadParcentage = parcentage >= 0.90f ? 1.0 : parcentage;
    }

    public void RestoreThumbnailLoadingTask(CancellationToken ct)
    {
        IsFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, Path);

        if (Status is LoadingStatus.NowLoading)
        {
            Status = LoadingStatus.PendingLoad;
            InitializeAsync(ct).FireAndForgetSafe();
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

    public async ValueTask EnsureImageSizeRatioAsync(CancellationToken ct)
    {
        await EnsureStorageItemAsync(ct);
        ImageAspectRatioWH ??= (await _thumbnailImageService.GetEnsureThumbnailSizeAsync(Item, ct)).RatioWH;
    }
    bool _disposed;
}



public sealed partial class LazyCacheImageFileViewModel : ObservableObject, IStorageItemViewModel
{
    private readonly FolderImageCollectionContext _imageCollectionContext;
    private readonly FileSortType _fileSortType;
    private readonly FolderStructureFileEntry _cacheEntry;
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

    public LazyCacheImageFileViewModel(
        FolderImageCollectionContext imageCollectionContext,
        FileSortType fileSortType,
        FolderStructureFileEntry cacheEntry,
        IImageSource? imageSource,
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
        _fileSortType = fileSortType;
        _cacheEntry = cacheEntry;
        _messenger = messenger;
        if (imageSource != null)
        {
            _item = imageSource;
            Name = Item.Name;
            Path = Item.Path;
            DateCreated = Item.DateCreated;
            Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(Item);
            UpdateLastReadPosition();
            IsFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, Item.Path);
        }
        else
        {
            Name = _cacheEntry.GetFileName();
            Path = _cacheEntry.Path;
            DateCreated = _cacheEntry.DateCreated;
            Type = StorageItemTypes.Image;
            UpdateLastReadPosition();
            IsFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, _cacheEntry.Path);
        }
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

    async ValueTask EnsureStorageItemAsync(CancellationToken ct)
    {        
        if (Item == null)
        {
            StorageFile file;
            try
            {
                file = await _imageCollectionContext.Folder.GetFileAsync(_cacheEntry.GetFileName()).AsTask(ct);
            }
            catch (DirectoryNotFoundException)
            {
                _messenger.Send(new StorageItemNotFoundMessage(Path));
                throw;
            }
            catch (FileNotFoundException)
            {
                _messenger.Send(new StorageItemNotFoundMessage(Path));
                throw;
            }            
            Item = new StorageItemImageSource(file);
            Name = Item.Name;
            Path = Item.Path;
            DateCreated = Item.DateCreated;
            Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(Item);
            UpdateLastReadPosition();
            IsFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, Item.Path);
        }

        if (Type == StorageItemTypes.Movie
            && Duration == null
            &&  Item.StorageItem is StorageFile movieFile)
        {
            var videoProps = await movieFile.Properties.GetVideoPropertiesAsync();
            Duration = TimeSpanHelper.FormatTimeSpan(videoProps?.Duration ?? TimeSpan.Zero);
        }
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
                await EnsureStorageItemAsync(ct);

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
            Status = LoadingStatus.NowLoading;
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
        //var parcentage = _bookmarkManager.GetBookmarkLastReadPositionInNormalized(Path);
        //ReadParcentage = parcentage >= 0.90f ? 1.0 : parcentage;
    }

    public void RestoreThumbnailLoadingTask(CancellationToken ct)
    {
        IsFavorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, Path);

        if (Status is LoadingStatus.NowLoading)
        {
            Status = LoadingStatus.PendingLoad;
            InitializeAsync(ct).FireAndForgetSafe();
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

    public async ValueTask EnsureImageSizeRatioAsync(CancellationToken ct)
    {
        await EnsureStorageItemAsync(ct);
        ImageAspectRatioWH ??= (await _thumbnailImageService.GetEnsureThumbnailSizeAsync(Item, ct)).RatioWH;
    }
    bool _disposed;
}
