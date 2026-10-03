using I18NPortable;
using SharpCompress.Common;
using SharpCompress.Writers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TsubameViewer.ViewModels.SourceFolders.Commands;

public sealed class SaveImagesToArchiveCommand : ImageSourceCommandBase
{
    protected override void Execute(IImageSource imageSource)
    {
        throw new NotSupportedException();
    }

    protected override async void Execute(IEnumerable<IImageSource> imageSources)
    {
        await SaveImagesAsync(imageSources);
    }

    private static async Task SaveImagesAsync(IEnumerable<IImageSource> imageSources)
    {
        var sources = imageSources?
            .Where(source => source is not null)
            .ToList();

        if (sources is null || sources.Count == 0)
        {
            return;
        }

        var representative = sources[0];
        var pathSegments = representative.Path
            .Replace('\\', '/')
            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        var folderName = pathSegments.Length > 1
            ? pathSegments[^2]
            : Path.GetFileNameWithoutExtension(representative.Name);

        if (string.IsNullOrWhiteSpace(folderName) || folderName.EndsWith(":", StringComparison.Ordinal))
        {
            folderName = Path.GetFileNameWithoutExtension(representative.Name);
        }

        if (string.IsNullOrWhiteSpace(folderName))
        {
            folderName = "Images";
        }

        var picker = new FileSavePicker
        {
            SuggestedFileName = folderName,
            DefaultFileExtension = SupportedFileTypesHelper.ZipFileType,
        };

        picker.FileTypeChoices.Add(
            "ZIPファイル",
            new List<string> { SupportedFileTypesHelper.ZipFileType });

        var destination = await picker.PickSaveFileAsync();
        if (destination is null)
        {
            return;
        }

        using var outputStream = await destination.OpenStreamForWriteAsync();
        using var writer = WriterFactory.OpenWriter(
            outputStream,
            ArchiveType.Zip,
            new WriterOptions(CompressionType.None));
        var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var imageSource in sources)
        {
            var entryName = GetUniqueEntryName(imageSource.Name, usedEntryNames);
            using var imageStream = await imageSource.GetImageStreamAsync();
            writer.Write(entryName, imageStream);
        }
    }

    private static string GetUniqueEntryName(string name, HashSet<string> usedNames)
    {
        var entryName = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(entryName))
        {
            entryName = "image";
        }

        if (usedNames.Add(entryName))
        {
            return entryName;
        }

        var baseName = Path.GetFileNameWithoutExtension(entryName);
        var extension = Path.GetExtension(entryName);

        for (var index = 2; ; index++)
        {
            var candidate = $"{baseName} ({index}){extension}";
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }
}
