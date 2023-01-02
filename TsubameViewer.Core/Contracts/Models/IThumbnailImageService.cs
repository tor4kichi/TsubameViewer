using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.ImageViewer;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Core.Contracts.Models;

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

