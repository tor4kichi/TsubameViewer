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
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using Windows.Storage.Pickers;

namespace TsubameViewer.ViewModels.SourceFolders.Commands;

public sealed class SaveArchiveDirectoryToArchiveCommand : ImageSourceCommandBase
{
    protected override async void Execute(IImageSource imageSource)
    {
        await SaveArchiveDirectoriesAsync(new[] { imageSource });
    }

    protected override async void Execute(IEnumerable<IImageSource> imageSources)
    {
        await SaveArchiveDirectoriesAsync(imageSources);
    }

    private static async Task SaveArchiveDirectoriesAsync(IEnumerable<IImageSource> imageSources)
    {
        var directories = imageSources?
            .OfType<ArchiveDirectoryImageSource>()
            .ToList();

        if (directories is null || directories.Count == 0)
        {
            return;
        }

        var suggestedFileName = string.IsNullOrWhiteSpace(directories[0].Name)
            ? "Archive.zip"
            : directories[0].Name;

        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = SupportedFileTypesHelper.ZipFileType,
        };

        picker.FileTypeChoices.Add(
            "ZIP",
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

        foreach (var directory in directories)
        {
            foreach (var entry in EnumerateEntries(directory))
            {
                using var imageStream = await entry.GetImageStreamAsync(default);
                writer.Write(entry.EntryKey, imageStream);
            }
        }
    }

    private static IEnumerable<ArchiveEntryImageSource> EnumerateEntries(
        ArchiveDirectoryImageSource directory)
    {
        foreach (var child in directory.GetChildItems())
        {
            if (child is ArchiveDirectoryImageSource childDirectory)
            {
                foreach (var entry in EnumerateEntries(childDirectory))
                {
                    yield return entry;
                }
            }
            else if (child is ArchiveEntryImageSource entry)
            {
                yield return entry;
            }
        }
    }

    private static string GetRelativeEntryName(string entryKey, string directoryKey)
    {
        var normalizedEntryKey = entryKey.Replace('\\', '/').TrimStart('/');
        var normalizedDirectoryKey = directoryKey.Replace('\\', '/').Trim('/');

        if (!string.IsNullOrEmpty(normalizedDirectoryKey))
        {
            var prefix = normalizedDirectoryKey + "/";
            if (normalizedEntryKey.StartsWith(prefix, StringComparison.Ordinal))
            {
                normalizedEntryKey = normalizedEntryKey.Substring(prefix.Length);
            }
        }

        var safeSegments = normalizedEntryKey
            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != "." && segment != "..");

        var relativeName = string.Join("/", safeSegments);
        return string.IsNullOrWhiteSpace(relativeName) ? "image" : relativeName;
    }

    private static string GetUniqueEntryName(string name, HashSet<string> usedNames)
    {
        if (usedNames.Add(name))
        {
            return name;
        }

        var separatorIndex = name.LastIndexOf('/');
        var directoryPart = separatorIndex >= 0
            ? name.Substring(0, separatorIndex + 1)
            : string.Empty;
        var fileName = separatorIndex >= 0
            ? name.Substring(separatorIndex + 1)
            : name;

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var index = 2; ; index++)
        {
            var candidate = $"{directoryPart}{baseName} ({index}){extension}";
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

}
