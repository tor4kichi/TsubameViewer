using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TsubameViewer.Contracts.Services;

public interface IImageCodecService
{         
    Task<bool> OpenImageCodecExtensionStorePageAsync(string fileType);
    Task<IReadOnlyCollection<ImageCodecExtensionInfo>> GetSupportedCodecsAsync();
}

public sealed class ImageCodecExtensionInfo
{
    public string[] FileTypes { get; set; }
    public string Label { get; set; }
    public Uri DownloadUrl { get; set; }
}
