using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.ImageViewer;
using Windows.Storage;
using Windows.Storage.Streams;
#nullable enable
namespace TsubameViewer.Core.Contracts.Models;

public interface ISecondaryTileThumbnailImageService
{
    Task SecondaryThumbnailDeleteNotExist(IEnumerable<string> tileIdList);
    Task<GenerateSecondaryTileThumbnailResult?> GenerateSecondaryThumbnailImageAsync(IStorageItem storageItem, string tileId, CancellationToken ct);
    Task<GenerateSecondaryTileThumbnailResult?> GenerateSecondaryThumbnailImageAsync(StorageFolder folder, string tileId, CancellationToken ct);
}

public sealed record GenerateSecondaryTileThumbnailResult(StorageFile Wide310x150Logo, StorageFile Square310x310Logo, StorageFile Square150x150Logo);
