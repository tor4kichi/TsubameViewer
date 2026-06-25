using CommunityToolkit.Diagnostics;
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
    readonly IImageCollectionContext _imageCollectionContext;
    public int Index { get; }
    readonly FileSortType _fileSortType;
    readonly IMessenger _messenger;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    readonly LocalBookmarkRepository _bookmarkManager;
    readonly ThumbnailImageManager _thumbnailImageService;
    readonly AlbamRepository _albamRepository;
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

    public bool IsSourceStorageItem => Path != null && (_sourceStorageItemsRepository?.IsSourceStorageItem(Path) ?? false);


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

    readonly static Core.AsyncLock _asyncLock = new(Math.Max(1, Environment.ProcessorCount / 2));
    readonly static Core.AsyncLock _imageLoadingLock = new();

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
                IsFavorite = _albamRepository.IsExistAlbamItem(Item.Path);
            }
        }
        catch (DirectoryNotFoundException)
        {
            Status = LoadingStatus.LoadFailed;
            _messenger.Send(new StorageItemNotFoundMessage(Path ?? ""));
        }
        catch (FileNotFoundException)
        {
            Status = LoadingStatus.LoadFailed;
            _messenger.Send(new StorageItemNotFoundMessage(Path ?? ""));
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
                Guard.IsNotNull(Item);

                using (var stream = await _thumbnailImageService.GetThumbnailImageStreamAsync(Item, ct: ct))
                {
                    if (stream is null || stream.Length == 0) { return; }
                    if (_status is not LoadingStatus.NowLoading) { return; }

                    ImageAspectRatioWH ??= _thumbnailImageService.GetCachedThumbnailSize(Item)?.RatioWH;

                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    var bitmapImage = new BitmapImage();
                    bitmapImage.AutoPlay = false;
                    using (var l = await _imageLoadingLock.LockAsync(ct))
                    {
                        await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream()).AsTask(ct);
                        Image = bitmapImage;
                    }
                }
                
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
        IsFavorite = _albamRepository.IsExistAlbamItem(Path);

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
        Guard.IsNotNull(Item);
        ImageAspectRatioWH ??= _thumbnailImageService.GetCachedThumbnailSize(Item)?.RatioWH;
    }
    bool _disposed;
}



public sealed partial class LazyCacheImageFileViewModel : ObservableObject, IStorageItemViewModel
{
    readonly FolderImageCollectionContext _imageCollectionContext;
    readonly FileSortType _fileSortType;
    readonly FolderStructureFileEntry _cacheEntry;
    readonly IMessenger _messenger;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    readonly LocalBookmarkRepository _bookmarkManager;
    readonly ThumbnailImageManager _thumbnailImageService;
    readonly AlbamRepository _albamRepository;
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

    public bool IsSourceStorageItem => Path != null && (_sourceStorageItemsRepository?.IsSourceStorageItem(Path) ?? false);

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
            _name = _item.Name;
            _path = _item.Path;
            _dateCreated = _item.DateCreated;
            _type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(_item);

            // Note: ItemVM生成とコレクションへの詰め込み処理がボトルネックになってる
            // 表示対象のみ必要な情報を引き出すようにして応答性を改善したい
            //_isFavorite = _albamRepository.IsExistAlbamItem(_item.Path);
            //_imageAspectRatioWH = _thumbnailImageService.GetCachedThumbnailSize(_item.Path)?.RatioWH;
        }
        else
        {
            _name = _cacheEntry.Name;
            _path = _cacheEntry.Path;
            _dateCreated = _cacheEntry.DateCreated;
            _type = StorageItemTypes.Image;
            //_isFavorite = _albamRepository.IsExistAlbamItem(_cacheEntry.Path);
            //_imageAspectRatioWH = _thumbnailImageService.GetCachedThumbnailSize(_cacheEntry.Path)?.RatioWH;
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

    readonly static Core.AsyncLock _asyncLock = new(Math.Max(1, Environment.ProcessorCount / 2));

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
                file = await _imageCollectionContext.Folder.GetFileAsync(_cacheEntry.Name).AsTask(ct);
            }
            catch (DirectoryNotFoundException)
            {
                _messenger.Send(new StorageItemNotFoundMessage(Path ?? ""));
                throw;
            }
            catch (FileNotFoundException)
            {
                _messenger.Send(new StorageItemNotFoundMessage(Path ?? ""));
                throw;
            }            
            Item = new StorageItemImageSource(file);

            // コンストラクタで初期化済みの項目はOnPropertyChanged発火も回避したいので更新しない
            //Name = Item.Name;
            //Path = Item.Path;
            //DateCreated = Item.DateCreated;
            //Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(Item);
            //UpdateLastReadPosition();
            IsFavorite = _albamRepository.IsExistAlbamItem(Item.Path);
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
            // ImageListupPageの読み込みを順列化しているためロック不要
            //using (await _asyncLock.LockAsync(ct))
            {
                if (IsInitialized) { return; }
                if (_disposed) { return; }
                if (_status is not LoadingStatus.PendingLoad) { return; }

                _status = LoadingStatus.NowLoading;
                await EnsureStorageItemAsync(ct);
                Guard.IsNotNull(Item);
                using (var stream = await _thumbnailImageService.GetThumbnailImageStreamAsync(Item, ct: ct))
                {
                    if (stream is null || stream.Length == 0) { return; }
                    if (_status is not LoadingStatus.NowLoading) { return; }

                    ImageAspectRatioWH ??= _thumbnailImageService.GetCachedThumbnailSize(Item)?.RatioWH;

                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    var bitmapImage = new BitmapImage();
                    bitmapImage.AutoPlay = false;
                    bitmapImage.SetSourceAsync(stream.AsRandomAccessStream()).AsTask(ct).FireAndForgetSafe();
                    Image = bitmapImage;
                }

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
        IsFavorite = _albamRepository.IsExistAlbamItem(Path);

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
        if (ImageAspectRatioWH == null)
        {
            IsFavorite = _albamRepository.IsExistAlbamItem(_cacheEntry.Path);
            ImageAspectRatioWH = _thumbnailImageService.GetCachedThumbnailSize(_cacheEntry.Path)?.RatioWH;
        }
    }
    bool _disposed;
}
