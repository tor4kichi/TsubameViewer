using Microsoft.Toolkit.Diagnostics;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Contracts.Services;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Core.Models.ImageViewer.ImageSource;

public sealed class ArchiveDirectoryImageSource : IArchiveEntryImageSource, IImageSource
{
    private readonly ArchiveImageCollection _imageCollection;
    private readonly ArchiveDirectoryToken _directoryToken;
    private readonly FolderListingSettings _folderListingSettings;
    private readonly ThumbnailManager _thumbnailManager;

    public ArchiveDirectoryImageSource(ArchiveImageCollection archiveImageCollection, ArchiveDirectoryToken directoryToken, FolderListingSettings folderListingSettings, ThumbnailManager thumbnailManager)
    {
        _imageCollection = archiveImageCollection;
        _directoryToken = directoryToken;
        _folderListingSettings = folderListingSettings;
        _thumbnailManager = thumbnailManager;
        Name = _directoryToken.Label is not null ? new string(_directoryToken.Label.Reverse().TakeWhile(c => c != System.IO.Path.DirectorySeparatorChar && c != System.IO.Path.AltDirectorySeparatorChar).Reverse().ToArray()) : _imageCollection.Name;
        Path = PageNavigationConstants.MakeStorageItemIdWithPage(archiveImageCollection.File.Path, directoryToken.Key);
    }

    public IArchiveEntry ArchiveEntry => _directoryToken?.Entry;

    public StorageFile StorageItem => _imageCollection.File;
    
    IStorageItem IImageSource.StorageItem => _imageCollection.File;

    public string Name { get; }

    public string Path { get; }

    public DateTime DateCreated => _imageCollection.File.DateCreated.DateTime;

    public string EntryKey => _directoryToken.Key;

    public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct = default)
    {
        var imageSource = GetNearestImageFromDirectory(_directoryToken);
        if (imageSource == null) { return null; }

        return await imageSource.GetImageStreamAsync(ct);
    }

    public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct = default)
    {
        if (_folderListingSettings.IsArchiveEntryGenerateThumbnailEnabled)
        {
            var file = await _thumbnailManager.GetArchiveEntryThumbnailImageFileAsync(StorageItem, ArchiveEntry, ct);
            if (file is null)
            {
                var imageSource = GetNearestImageFromDirectory(_directoryToken);
                if (imageSource == null) { return null; }

                return await imageSource.GetThumbnailImageStreamAsync(ct);
            }
            else
            {
                return file;
            }
        }
        else
        {
            var stream =  await _thumbnailManager.GetArchiveEntryThumbnailImageStreamAsync(StorageItem, ArchiveEntry, ct);
            if (stream != null) { return stream; }

            var imageSource = GetNearestImageFromDirectory(_directoryToken);
            if (imageSource == null) { return null; }

            return await imageSource.GetThumbnailImageStreamAsync(ct);

        }
    }

    private IImageSource GetNearestImageFromDirectory(ArchiveDirectoryToken firstToken)
    {
        Stack<ArchiveDirectoryToken> archiveDirectoryTokens = new Stack<ArchiveDirectoryToken>();
        archiveDirectoryTokens.Push(firstToken);
        IImageSource imageSource = null;
        while (archiveDirectoryTokens.TryPop(out var token))
        {
            imageSource = _imageCollection.GetThumbnailImageFromDirectory(token);
            if (imageSource != null) { break; }
            else
            {
                foreach (var subDir in _imageCollection.GetSubDirectories(token).Reverse())
                {
                    archiveDirectoryTokens.Push(subDir);
                }
            }
        }

        return imageSource;
    }

    public bool IsContainsImage()
    {
        return _imageCollection.GetImagesFromDirectory(_directoryToken).Any();
    }

    public bool IsContainsSubDirectory()
    {
        return _imageCollection.GetSubDirectories(_directoryToken).Any();
    }

    public IArchiveEntry GetParentDirectoryEntry()
    {            
        var entry = _imageCollection.GetDirectoryTokenFromPath(System.IO.Path.GetDirectoryName(_directoryToken?.Key))?.Entry;
        if (entry == null
            || !(entry.Key.Contains(System.IO.Path.DirectorySeparatorChar) || entry.Key.Contains(System.IO.Path.AltDirectorySeparatorChar))
            )
        {
            return null;
        }
        else { return entry; }
    }

    public ThumbnailManager.ThumbnailSize? GetThumbnailSize()
    {
        return _thumbnailManager.GetThumbnailOriginalSize(StorageItem, ArchiveEntry);
    }

    public bool Equals(IImageSource other)
    {
        if (other == null) { return false; }            
        return this.Path == other.Path;
    }
    
    public override string ToString()
    {
        return Path;
    }
}
