using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.ImageViewer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace TsubameViewer.ViewModels.SourceFolders.Commands;

public sealed class SaveImageCommand : ImageSourceCommandBase
{
    protected override async void Execute(IImageSource imageSource)
    {
        var formats = new List<(string Name, string Extension, SKEncodedImageFormat Format)>
        {
            ("JPEG", ".jpg", SKEncodedImageFormat.Jpeg),
            ("PNG", ".png", SKEncodedImageFormat.Png),
            ("WebP", ".webp", SKEncodedImageFormat.Webp),
        };

        var suggestedFileName = imageSource.Name;
        var extension = Path.GetExtension(suggestedFileName);
        if (!formats.Any(format =>
            string.Equals(format.Extension, extension, StringComparison.OrdinalIgnoreCase)))
        {
            suggestedFileName = Path.ChangeExtension(suggestedFileName, ".jpg");
            extension = Path.GetExtension(suggestedFileName);
        }

        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
        };

        if (formats.FirstOrDefault(x => x.Extension == extension)is { } primaryFileType)
        {
            picker.FileTypeChoices.Add(primaryFileType.Name, new List<string> { primaryFileType.Extension });
        }

        foreach (var format in formats.Where(x => x.Extension != extension))
        {
            picker.FileTypeChoices.Add(format.Name, new List<string> { format.Extension });
        }

        if (await picker.PickSaveFileAsync() is not { } file)
        {
            return;
        }

        var selectedFormat = formats.FirstOrDefault(format =>
            string.Equals(format.Extension, file.FileType, StringComparison.OrdinalIgnoreCase));

        if (selectedFormat.Extension is null)
        {
            return;
        }

        using var ras = await imageSource.GetImageRandomAccessStreamAsync(default);
        if (ras is null)
        {
            return;
        }

        ras.Seek(0);
        using var input = ras.AsStreamForRead();
        using var output = await file.OpenAsync(FileAccessMode.ReadWrite);
        using var outputStream = output.AsStreamForWrite();
        outputStream.SetLength(0);
        outputStream.Seek(0, SeekOrigin.Begin);

        if (selectedFormat.Extension != extension)
        {
            using var bitmap = SKBitmap.Decode(input)
                ?? throw new InvalidDataException("画像をデコードできませんでした。");            
            if (!bitmap.Encode(outputStream, selectedFormat.Format, quality: 100))
            {
                throw new InvalidOperationException("画像をエンコードできませんでした。");
            }

            await outputStream.FlushAsync();
        }
        else
        {
            await input.CopyToAsync(outputStream);
        }
    }
}
