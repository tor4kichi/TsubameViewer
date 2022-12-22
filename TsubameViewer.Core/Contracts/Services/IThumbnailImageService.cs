using SharpCompress.Archives;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Core.Contracts.Services;

public interface IThumbnailImageService
{
    long ComputeUsingSize();
    Task DeleteAllThumbnailUnderPathAsync(string path);
    Task DeleteAllThumbnailsAsync();
    Task DeleteThumbnailFromPathAsync(string path);
    Task FolderChangedAsync(string oldPath, string newPath);
    Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(IStorageItem storageItem, string tileId, CancellationToken ct);
    Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(StorageFolder folder, string tileId, CancellationToken ct);
    ValueTask<IRandomAccessStream> GetArchiveEntryThumbnailImageFileAsync(StorageFile sourceFile, IArchiveEntry archiveEntry, CancellationToken ct);
    Task<IRandomAccessStream> GetArchiveEntryThumbnailImageStreamAsync(StorageFile sourceFile, IArchiveEntry archiveEntry, CancellationToken ct);
    ValueTask<IRandomAccessStream> GetFileThumbnailImageFileAsync(StorageFile file, CancellationToken ct);
    Task<IRandomAccessStream> GetFileThumbnailImageStreamAsync(StorageFile file, CancellationToken ct);
    ValueTask<IRandomAccessStream> GetFolderThumbnailImageFileAsync(StorageFolder folder, CancellationToken ct);
    Task<IRandomAccessStream> GetFolderThumbnailImageStreamAsync(StorageFolder folder, CancellationToken ct);
    ValueTask<IRandomAccessStream> GetPdfPageThumbnailImageFileAsync(StorageFile sourceFile, PdfPage pdfPage, CancellationToken ct);
    Task<IRandomAccessStream> GetPdfPageThumbnailImageStreamAsync(StorageFile sourceFile, PdfPage pdfPage, CancellationToken ct);
    ValueTask<IRandomAccessStream> GetThumbnailAsync(IStorageItem storageItem, CancellationToken ct);
    Task<IRandomAccessStream> GetThumbnailImageFromPathAsync(string path, CancellationToken ct);
    ThumbnailSize? GetThumbnailOriginalSize(IStorageItem file);
    ThumbnailSize? GetThumbnailOriginalSize(StorageFile file, IArchiveEntry archiveEntry);
    ThumbnailSize? GetThumbnailOriginalSize(StorageFile file, PdfPage pdfPage);
    ThumbnailSize? GetThumbnailOriginalSize(string path);
    Task SecondaryThumbnailDeleteNotExist(IEnumerable<string> tileIdList);
    Task SetArchiveEntryThumbnailAsync(StorageFile targetItem, IArchiveEntry entry, IRandomAccessStream bitmapImage, bool requireTrancode, CancellationToken ct);
    Task SetThumbnailAsync(IStorageItem targetItem, IRandomAccessStream bitmapImage, bool requireTrancode, CancellationToken ct);
    ThumbnailSize SetThumbnailSize(string path, BitmapImage image);
}


public sealed class GenerateSecondaryTileThumbnailResult
{
    public StorageFile Wide310x150Logo { get; set; }
    public StorageFile Square310x310Logo { get; set; }
    public StorageFile Square150x150Logo { get; set; }
}


public struct ThumbnailSize
{
    public uint Width { get; set; }
    public uint Height { get; set; }
    public float RatioWH { get; set; }
}