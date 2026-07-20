using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using StorageItemTypes = TsubameViewer.Core.Models.StorageItemTypes;

#nullable enable
namespace TsubameViewer.ViewModels;

public enum LoadingStatus
{
    None,
    PendingLoad,
    NowLoading,
    Loaded,

    LoadFailed,
}

public sealed partial class LazyFolderOrArchiveFileViewModel : ObservableObject, IStorageItemViewModel, IEquatable<IStorageItemViewModel>
{
    readonly IImageCollectionContext _imageCollectionContext;
    readonly int _itemIndex;
    readonly FileSortType _fileSortType;
    readonly IMessenger _messenger;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    readonly LocalBookmarkRepository _bookmarkManager;
    readonly ThumbnailImageManager _thumbnailImageService;
    readonly AlbamRepository _albamRepository;
    public SelectionContext? Selection { get; }
    public StorageItemSettings Settings { get; }

    [ObservableProperty]
    IImageSource? _item;

    [ObservableProperty]
    string? _name;

    [ObservableProperty]
    string? _path;

    [ObservableProperty]
    DateTimeOffset _dateCreated;

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
    
    [ObservableProperty]
    BitmapImage? _image;

    public LazyFolderOrArchiveFileViewModel(
        IImageCollectionContext imageCollectionContext,
        int itemIndex,
        FileSortType fileSortType,
        IMessenger messenger,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        LocalBookmarkRepository bookmarkManager,
        ThumbnailImageManager thumbnailImageService,
        AlbamRepository albamRepository,
        SelectionContext? selectionContext = null,
        StorageItemSettings? settings = null
        )
    {
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _bookmarkManager = bookmarkManager;
        _thumbnailImageService = thumbnailImageService;
        _albamRepository = albamRepository;
        Selection = selectionContext;
        Settings = settings ?? Ioc.Default.GetRequiredService<StorageItemSettings>();
        _imageCollectionContext = imageCollectionContext;
        _itemIndex = itemIndex;
        _fileSortType = fileSortType;
        _messenger = messenger;

        _type = StorageItemTypes.Archive;

    }

    BookmarkFacade? _bookmark;
    BookmarkFacade Bookmark => _bookmark ??= _bookmarkManager.GetBookmarkFacade(Path);

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
        Status = LoadingStatus.None;
        Item = null;
        Image = null;
    }

    readonly static Core.AsyncLock _asyncLock = new(Math.Max(1, Environment.ProcessorCount));
    readonly static Core.AsyncLock _imageLoadingLock = new(2);

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
    public bool IsThumbanilImageCached => Item == null ? false : _thumbnailImageService.GetCachedThumbnailSize(Item) != null;

    public bool IsInitialized => _status == LoadingStatus.Loaded;
    public async ValueTask InitializeAsync(CancellationToken ct)
    {
        // ItemsRepeaterの読み込み順序が対応するためキャンセルが必要
        // ItemsRepeaterは表示しない先の方まで一度サイズを確認するために読み込みを掛けようとする
        var lastStatus = _status;
        if (lastStatus is not LoadingStatus.None and not LoadingStatus.PendingLoad and not LoadingStatus.NowLoading) { return; }

        try
        {
            if (IsInitialized) { return; }
            _status = LoadingStatus.PendingLoad;

            await EnsureStorageItemAsync(ct);
            Guard.IsNotNull(Item);

            _status = LoadingStatus.NowLoading;
            using (await _asyncLock.LockAsync(ct))
            {
                if (_status is not LoadingStatus.NowLoading) { return; }
                if (Item == null) { return; }
                using (var stream = await Task.Run(async () => await _thumbnailImageService.GetThumbnailImageStreamAsync(Item, ct: ct), ct))
                {
                    if (stream is null || stream.Length == 0) { return; }
                    if (_status is not LoadingStatus.NowLoading) { return; }

                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    using (await _imageLoadingLock.LockAsync(ct))
                    {
                        if (_status is not LoadingStatus.NowLoading) { return; }
                        var image = Image ?? new BitmapImage() { AutoPlay = false };
                        await image.SetSourceAsync(stream.AsRandomAccessStream()).AsTask(ct);
                        Image = image;
                    }
                }

                Status = LoadingStatus.Loaded;
            }
            UpdateLastReadPosition();
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
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }
    }

    public async ValueTask EnsureStorageItemAsync(CancellationToken ct)
    {
        if (Item == null)
        {
            Item = await _imageCollectionContext.GetFolderOrArchiveFileAtAsync(_itemIndex, _fileSortType, ct);
            Name = Item.Name;
            Path = Item.Path;
            DateCreated = Item.DateCreated;
            Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(Item);
            IsFavorite = _albamRepository.IsExistAlbamItem(Item.Path);
            if (Type == StorageItemTypes.Movie
                && Item.StorageItem is Windows.Storage.StorageFile file)
            {
                if (Bookmark.PageName is string duration
                    && duration != null)
                {
                    Duration = duration;
                }
                else
                {
                    var movieProps = await file.Properties.GetVideoPropertiesAsync();
                    if (movieProps?.Duration is { } d && d != TimeSpan.Zero)
                    {
                        Duration = TimeSpanHelper.FormatTimeSpan(d);
                        Bookmark.PageName = Duration;
                    }
                    else
                    {
                        Bookmark.PageName = "";
                    }
                }
            }
        }
    }

    public void UpdateLastReadPosition()
    {
        if (Type is StorageItemTypes.Archive or StorageItemTypes.EBook or StorageItemTypes.Movie)
        {
            // ビューアから戻った際にこのメソッドが呼ばれる前提でBookmarkFacadeを再取得させる
            _bookmark = null;
            ReadParcentage = Bookmark.IsFinishedReading ? 1.0 : Bookmark.ReadPosition.Value;
        }
        else if (Type is StorageItemTypes.Folder && Settings.IsDisplayFolderItemsCount)
        {
            var (finished, total) = _bookmarkManager.GetItemsCountForFolder(Path);
            if (total != 0)
            {
                Duration = $"{finished}/{total}";
            }
        }
    }

    public void RestoreThumbnailLoadingTask(CancellationToken ct)
    {
        IsFavorite = _albamRepository.IsExistAlbamItem(Path);

        if (Status is not LoadingStatus.LoadFailed and not LoadingStatus.Loaded)
        {
            Status = LoadingStatus.PendingLoad;
            InitializeAsync(ct).FireAndForgetSafe();
        }
    }

    public void ThumbnailChanged()
    {
        Status = LoadingStatus.None;
        InitializeAsync(default).FireAndForgetSafe();
    }

    public ValueTask EnsureImageSizeRatioAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public bool Equals(IStorageItemViewModel other)
    {
        return this.Path?.Equals(other.Path) ?? false;
    }
}




public sealed partial class LazyCacheFolderOrArchiveFileViewModel : ObservableObject, IStorageItemViewModel
{
    readonly FolderImageCollectionContext _imageCollectionContext;
    private readonly FolderStructureFileEntry _cacheEntry;
    readonly FileSortType _fileSortType;
    readonly IMessenger _messenger;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    readonly LocalBookmarkRepository _bookmarkManager;
    readonly ThumbnailImageManager _thumbnailImageService;
    readonly AlbamRepository _albamRepository;
    public SelectionContext? Selection { get; }
    public StorageItemSettings Settings { get; }

    [ObservableProperty]
    IImageSource? _item;

    [ObservableProperty]
    string? _name;

    [ObservableProperty]
    string? _path;

    [ObservableProperty]
    DateTimeOffset _dateCreated;

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

    [ObservableProperty]
    BitmapImage? _image;

    public LazyCacheFolderOrArchiveFileViewModel(
        FolderImageCollectionContext imageCollectionContext,
        FolderStructureFileEntry cacheEntry,
        FileSortType fileSortType,        
        IMessenger messenger,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        LocalBookmarkRepository bookmarkManager,
        ThumbnailImageManager thumbnailImageService,
        AlbamRepository albamRepository,
        SelectionContext? selectionContext = null,
        StorageItemSettings? settings = null
        )
    {
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _bookmarkManager = bookmarkManager;
        _thumbnailImageService = thumbnailImageService;
        _albamRepository = albamRepository;
        Selection = selectionContext;
        Settings = settings ?? Ioc.Default.GetRequiredService<StorageItemSettings>();
        _imageCollectionContext = imageCollectionContext;
        _cacheEntry = cacheEntry;
        _fileSortType = fileSortType;        
        _messenger = messenger;
        _name = _cacheEntry.Name;
        _path = _cacheEntry.Path;
        _dateCreated = _cacheEntry.DateCreated;
        _type = SupportedFileTypesHelper.FileExtensionToStorageItemType(_cacheEntry.Path);
        if (_type == StorageItemTypes.None)
        {
            _type = StorageItemTypes.Folder;
        }
        _isFavorite = _albamRepository.IsExistAlbamItem(_cacheEntry.Path);
    }

    BookmarkFacade? _bookmark;
    BookmarkFacade Bookmark => _bookmark ??= _bookmarkManager.GetBookmarkFacade(Path);

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
        Status = LoadingStatus.None;        
        Item = null;
        Image = null;
    }

    readonly static Core.AsyncLock _asyncLock = new(Math.Max(1, Environment.ProcessorCount));
    readonly static Core.AsyncLock _imageLoadingLock = new(1);
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

    public bool IsInitialized => _status == LoadingStatus.Loaded;
    public async ValueTask InitializeAsync(CancellationToken ct)
    {
        // ItemsRepeaterの読み込み順序が対応するためキャンセルが必要
        // ItemsRepeaterは表示しない先の方まで一度サイズを確認するために読み込みを掛けようとする
        var lastStatus = _status;
        if (lastStatus is not LoadingStatus.None and not LoadingStatus.PendingLoad and not LoadingStatus.NowLoading) { return; }

        _status = LoadingStatus.PendingLoad;
        try
        {
            if (IsInitialized) { return; }

            await EnsureStorageItemAsync(ct);
            ct.ThrowIfCancellationRequested();
            Guard.IsNotNull(Item);
            _status = LoadingStatus.NowLoading;
            using (await _asyncLock.LockAsync(ct))
            {
                if (_status is not LoadingStatus.NowLoading) { return; }
                if (Item == null) { return; }

                using (var outputStream = new MemoryStream())
                using (var stream = await Task.Run(async () => await _thumbnailImageService.GetThumbnailImageStreamAsync(Item, outputStream, ct: ct), ct))
                {
                    if (stream is null || stream.Length == 0) { return; }
                    if (_status is not LoadingStatus.NowLoading) { return; }

                    stream.Seek(0, System.IO.SeekOrigin.Begin);

                    // BitmapImageを使い回すため、並列処理のワーストケースでは同一BtmapImageに対して同時操作が発生しうる
                    var image = Image ?? new BitmapImage() { AutoPlay = false };
                    Image = image;
                    using (await _imageLoadingLock.LockAsync(ct))
                    {
                        if (_status is not LoadingStatus.NowLoading) { return; }
                        using (var ras = stream.AsRandomAccessStream())
                        {
                            await image.SetSourceAsync(ras).AsTask(ct);
                        }
                    }
                }

                // Note: 20msぐらい掛かるのでInitializeで実行
                UpdateLastReadPosition();

                //ImageAspectRatioWH ??= _thumbnailImageService.GetCachedThumbnailSize(Item)?.RatioWH;
                Status = LoadingStatus.Loaded;
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
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }
    }

    public async ValueTask EnsureStorageItemAsync(CancellationToken ct)
    {
        if (Item == null)
        {
            IStorageItem storageItem;
            try
            {
                storageItem = await _imageCollectionContext.Folder.GetItemAsync(_cacheEntry.Name);
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

            Item = new StorageItemImageSource(storageItem);
            //Name = Item.Name;
            //Path = Item.Path;
            //DateCreated = Item.DateCreated;
            //Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(Item);
            //IsFavorite = _albamRepository.IsExistAlbamItem(Item.Path);
            if (Type == StorageItemTypes.Movie
                && Item.StorageItem is Windows.Storage.StorageFile file)
            {
                if (Bookmark.PageName is string duration
                    && duration != null)
                {
                    Duration = duration;
                }
                else
                {
                    var movieProps = await file.Properties.GetVideoPropertiesAsync();
                    if (movieProps?.Duration is { } d && d != TimeSpan.Zero)
                    {
                        Duration = TimeSpanHelper.FormatTimeSpan(d);
                        Bookmark.PageName = Duration;
                    }
                    else
                    {
                        Bookmark.PageName = "";
                    }
                }
            }
        }
    }

    public void UpdateLastReadPosition()
    {
        if (Type is StorageItemTypes.Archive or StorageItemTypes.EBook or StorageItemTypes.Movie)
        {
            // ビューアから戻った際にこのメソッドが呼ばれる前提でBookmarkFacadeを再取得させる
            _bookmark = null;
            ReadParcentage = Bookmark.IsFinishedReading ? 1.0 : Bookmark.ReadPosition.Value;
        }
        else if (Type is StorageItemTypes.Folder && Settings.IsDisplayFolderItemsCount)
        {
            var (finished, total) = _bookmarkManager.GetItemsCountForFolder(Path);
            if (total != 0)
            {
                Duration = $"{finished}/{total}";
            }
        }
    }

    public void RestoreThumbnailLoadingTask(CancellationToken ct)
    {
        IsFavorite = _albamRepository.IsExistAlbamItem(Path);
        if (Item == null || Image == null)
        {
            Status = LoadingStatus.PendingLoad;
            InitializeAsync(ct).FireAndForgetSafe();
        }
    }

    public void ThumbnailChanged()
    {
        Status = LoadingStatus.None;
        InitializeAsync(default).FireAndForgetSafe();
    }

    public ValueTask EnsureImageSizeRatioAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
