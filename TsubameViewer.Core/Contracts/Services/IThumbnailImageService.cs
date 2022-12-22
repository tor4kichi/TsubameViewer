using SharpCompress.Archives;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.ImageViewer;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Core.Contracts.Services;

public interface IThumbnailImageService
{
    Task<IRandomAccessStream> GetFileThumbnailImageStreamAsync(StorageFile file, CancellationToken ct);
    ValueTask<IRandomAccessStream> GetThumbnailImageStreamAsync(IImageSource imageSource, CancellationToken ct = default);
    Task SetParentThumbnailImageAsync(IImageSource childImageSource, bool isArchiveThumbnailSetToFile = false, CancellationToken ct = default);
    ThumbnailSize? GetCachedThumbnailSize(IImageSource imageSource);
    ThumbnailSize SetThumbnailSize(IImageSource imageSource, uint pixelWidth, uint pixelHeight);
}

public interface IThumbnailImageMaintenanceService
{
    long ComputeUsingSize();
    Task DeleteAllThumbnailUnderPathAsync(string path);
    Task DeleteAllThumbnailsAsync();
    Task DeleteThumbnailFromPathAsync(string path);
    Task FolderChangedAsync(string oldPath, string newPath);
}

public interface ISecondaryTileThumbnailImageService
{
    Task SecondaryThumbnailDeleteNotExist(IEnumerable<string> tileIdList);
    Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(IStorageItem storageItem, string tileId, CancellationToken ct);
    Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(StorageFolder folder, string tileId, CancellationToken ct);
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