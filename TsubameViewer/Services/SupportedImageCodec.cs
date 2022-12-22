using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Services;
using Windows.Storage;
using Windows.System;

namespace TsubameViewer.Services;

public sealed class ImageCodecService : IImageCodecService
{
    private IReadOnlyDictionary<string, ImageCodecExtensionInfo> _fileTypeToImageCodecExtension;
    private readonly Uri _assetUrl;

    public ImageCodecService(Uri assetUrl)
    {
        _assetUrl = assetUrl;
    }

    bool _isInitialize;
    private async Task EnsureInitialzie()
    {
        if (_isInitialize) { return; }

        var file = await StorageFile.GetFileFromApplicationUriAsync(_assetUrl);
        using (var fileStream = await file.OpenReadAsync())
        {
            _isInitialize = true;
            var imageCodecExtensions = await JsonSerializer.DeserializeAsync<ImageCodecExtensionInfo[]>(fileStream.AsStream());
            _fileTypeToImageCodecExtension = imageCodecExtensions.SelectMany(x => x.FileTypes.Select(y => (FileType: y, Item: x))).ToDictionary(x => x.FileType, x => x.Item);
        }
    }


    public async Task<IReadOnlyCollection<ImageCodecExtensionInfo>> GetSupportedCodecsAsync()
    {
        await EnsureInitialzie();

        return _fileTypeToImageCodecExtension.Select(x => x.Value).Distinct().ToList();
    }

    public async Task<bool> OpenImageCodecExtensionStorePageAsync(string fileType)
    {
        await EnsureInitialzie();

        if (_fileTypeToImageCodecExtension.TryGetValue(fileType, out ImageCodecExtensionInfo extension))
        {
            return await Launcher.LaunchUriAsync(extension.DownloadUrl);
        }
        else
        {
            return false;
        }
    }
}
