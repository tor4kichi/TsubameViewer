using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Infrastructure;
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

using StorageItemTypes = TsubameViewer.Core.Models.StorageItemTypes;

public sealed class StorageItemSettings : FlagsRepositoryBase
{
    public StorageItemSettings()
    {
        _isDisplayFolderItemsCount = Read(false, nameof(IsDisplayFolderItemsCount));
        _descriptionTextFontSize = Read(12, nameof(DescriptionTextFontSize));
        _readingFinishedThresholdForImageViewer = Read(0.85, nameof(ReadingFinishedThresholdForImageViewer));
        _readingFinishedThresholdForEBookViewer = Read(0.9, nameof(ReadingFinishedThresholdForEBookViewer));
        _readingFinishedThresholdForMovieViewer = Read(0.9, nameof(ReadingFinishedThresholdForMovieViewer));
    }

    bool _isDisplayFolderItemsCount = false;
    public bool IsDisplayFolderItemsCount
    {
        get => _isDisplayFolderItemsCount;
        set => SetProperty(ref _isDisplayFolderItemsCount, value);
    }

    int _descriptionTextFontSize;
    public int DescriptionTextFontSize
    {
        get => _descriptionTextFontSize;
        set => SetProperty(ref _descriptionTextFontSize, value);
    }


    double _readingFinishedThresholdForImageViewer;
    public double ReadingFinishedThresholdForImageViewer
    {
        get => _readingFinishedThresholdForImageViewer;
        set => SetProperty(ref _readingFinishedThresholdForImageViewer, value);
    }
    double _readingFinishedThresholdForEBookViewer;
    public double ReadingFinishedThresholdForEBookViewer
    {
        get => _readingFinishedThresholdForEBookViewer;
        set => SetProperty(ref _readingFinishedThresholdForEBookViewer, value);
    }
    double _readingFinishedThresholdForMovieViewer;
    public double ReadingFinishedThresholdForMovieViewer
    {
        get => _readingFinishedThresholdForMovieViewer;
        set => SetProperty(ref _readingFinishedThresholdForMovieViewer, value);
    }
}


public sealed partial class StorageItemViewModel : ObservableObject, IDisposable, IStorageItemViewModel
{
    readonly IMessenger _messenger;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    readonly LocalBookmarkRepository _bookmarkManager;
    readonly ThumbnailImageManager _thumbnailImageService;
    readonly AlbamRepository _albamRepository;

    public IImageSource Item { get; }
    public SelectionContext? Selection { get; }
    public string Name { get; }


    public string Path { get; }

    public DateTimeOffset DateCreated { get; }
    public StorageItemSettings Settings { get; }

    [ObservableProperty]
    BitmapImage? _image;

    [ObservableProperty]
    float? _imageAspectRatioWH;


    [ObservableProperty]
    bool _isSelected;

    [ObservableProperty]
    bool _isFavorite;
    
    public StorageItemTypes Type { get; }
    public bool StorageItemTypesIsFolder => Type is StorageItemTypes.Folder;

    [ObservableProperty]
    double _readParcentage;
    
    public bool IsSourceStorageItem => _sourceStorageItemsRepository?.IsSourceStorageItem(Path) ?? false;


    [ObservableProperty]
    string? _duration;

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。'required' 修飾子を追加するか、Null 許容として宣言することを検討してください。
    public StorageItemViewModel(string name, StorageItemTypes storageItemTypes)
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。'required' 修飾子を追加するか、Null 許容として宣言することを検討してください。
    {
        Name = name;
        Type = storageItemTypes;
        Settings = Ioc.Default.GetRequiredService<StorageItemSettings>();
    }

    public StorageItemViewModel(
        IImageSource item,
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
        Item = item;
        _messenger = messenger;
        DateCreated = Item.DateCreated;
        Settings = settings ?? Ioc.Default.GetRequiredService<StorageItemSettings>();

        Name = Item.Name;
        Type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(item);
        Path = item.Path;

        _imageAspectRatioWH = _thumbnailImageService.GetCachedThumbnailSize(Item)?.RatioWH;

        UpdateLastReadPosition();
        _isFavorite = _albamRepository.IsExistAlbamItem(item.Path);
    }

    public bool IsRequestImageLoading { get; private set; } = false;
    private bool _isRequireLoadImageWhenRestored = false;
    public void StopImageLoading()
    {
        IsRequestImageLoading = false;
        _initializeCts?.Cancel();
    }

    CancellationTokenSource? _initializeCts;

    readonly static Core.AsyncLock _asyncLock = new(Math.Max(1, Environment.ProcessorCount / 2));
    readonly static Core.AsyncLock _imageLoadingLock = new();

    public async ValueTask EnsureImageSizeRatioAsync(CancellationToken ct)
    {
        ImageAspectRatioWH ??= _thumbnailImageService.GetCachedThumbnailSize(Item)?.RatioWH;
    }

    public bool IsInitialized { get; private set; } = false;
    public async ValueTask InitializeAsync(CancellationToken ct)
    {
        // ItemsRepeaterの読み込み順序が対応するためキャンセルが必要
        // ItemsRepeaterは表示しない先の方まで一度サイズを確認するために読み込みを掛けようとする
        IsRequestImageLoading = true;
        
        try
        {
            if (IsInitialized) { return; }
            if (_disposed) { return; }
            if (Item == null) { return; }
            if (IsRequestImageLoading is false) { return; }

            using var d = await _asyncLock.LockAsync(ct);

            if (Type == StorageItemTypes.Movie
                && Item.StorageItem is Windows.Storage.StorageFile file)
            {
                var movieProps = await file.Properties.GetVideoPropertiesAsync();
                Duration = TimeSpanHelper.FormatTimeSpan(movieProps?.Duration ?? TimeSpan.Zero);
            }

            using (var stream = await Task.Run(async () => await _thumbnailImageService.GetThumbnailImageStreamAsync(Item, ct: ct), ct))
            {
                if (stream is null || stream.Length == 0) { return; }
                ImageAspectRatioWH ??= _thumbnailImageService.GetCachedThumbnailSize(Item)?.RatioWH ?? 1;
                if (IsRequestImageLoading is false) { return; }

                {
                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    var bitmapImage = new BitmapImage();
                    bitmapImage.AutoPlay = false;
                    using (var l = await _imageLoadingLock.LockAsync(ct))
                    {
                        await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream()).AsTask(ct);
                        Image = bitmapImage;
                    }
                }
            }

            _isRequireLoadImageWhenRestored = false;
            IsInitialized = true;

        }
        catch (OperationCanceledException)
        {
            _isRequireLoadImageWhenRestored = true;
            IsInitialized = false;

            Debug.WriteLine("ImageLoading Canceled");
        }
        catch (NotSupportedImageFormatException ex)
        {
            // 0xC00D5212
            // "コンテンツをエンコードまたはデコードするための適切な変換が見つかりませんでした。"
            _isRequireLoadImageWhenRestored = true;
            IsInitialized = false;
            _messenger.Send<RequireInstallImageCodecExtensionMessage>(new(ex.FileType));
        }
        catch (DirectoryNotFoundException)
        {
            IsInitialized = true;
            _messenger.Send(new StorageItemNotFoundMessage(Path));
        }
        catch (FileNotFoundException)
        {
            IsInitialized = true;
            _messenger.Send(new StorageItemNotFoundMessage(Path));
        }
        finally
        {
            _initializeCts = null;
        }
    }

    public void UpdateLastReadPosition()
    {
        if (Type is StorageItemTypes.Archive or StorageItemTypes.EBook or StorageItemTypes.Movie)
        {
            var facade = _bookmarkManager.GetBookmarkFacade(Path);
            ReadParcentage = facade.IsFinishedReading ? 1d : facade.ReadPosition.Value;
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

        if (_isRequireLoadImageWhenRestored && Image == null)
        {
            InitializeAsync(ct).FireAndForgetSafe();
        }
    }

    public void ThumbnailChanged()
    {
        Image = null;
        IsInitialized = false;
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
