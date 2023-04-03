using System;
using System.Collections.Generic;
using System.Linq;
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

    public bool IsContainFileType(string fileType)
    {
        string trimedFileType = fileType.TrimStart('.');
        return FileTypes.Any(x => x.TrimStart('.') == trimedFileType);
    }
}
