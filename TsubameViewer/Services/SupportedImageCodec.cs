using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using Windows.Storage;
using Windows.System;

namespace TsubameViewer.Services
{
    public sealed class ImageCodecExtension
    {
        public string[] FileTypes { get; set; }
        public string Label { get; set; }
        public Uri DownloadUrl { get; set; }
    }

    public interface ISupportedImageCodec
    {         
        Task<bool> OpenImageCodecExtensionStorePageAsync(string fileType);
        Task<IReadOnlyCollection<ImageCodecExtension>> GetSupportedCodecsAsync();
    }

    public sealed class SupportedImageCodec : ISupportedImageCodec
    {
        private IReadOnlyDictionary<string, ImageCodecExtension> _fileTypeToImageCodecExtension;
        private readonly Uri _assetUrl;

        public SupportedImageCodec(Uri assetUrl)
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
                var imageCodecExtensions = await JsonSerializer.DeserializeAsync<ImageCodecExtension[]>(fileStream.AsStream());
                _fileTypeToImageCodecExtension = imageCodecExtensions.SelectMany(x => x.FileTypes.Select(y => (FileType: y, Item: x))).ToDictionary(x => x.FileType, x => x.Item);
            }
        }


        public async Task<IReadOnlyCollection<ImageCodecExtension>> GetSupportedCodecsAsync()
        {
            await EnsureInitialzie();

            return _fileTypeToImageCodecExtension.Select(x => x.Value).Distinct().ToList();
        }

        public async Task<bool> OpenImageCodecExtensionStorePageAsync(string fileType)
        {
            await EnsureInitialzie();

            if (_fileTypeToImageCodecExtension.TryGetValue(fileType, out ImageCodecExtension extension))
            {
                return await Launcher.LaunchUriAsync(extension.DownloadUrl);
            }
            else
            {
                return false;
            }
        }
    }
}
