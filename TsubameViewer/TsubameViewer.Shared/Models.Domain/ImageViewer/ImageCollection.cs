using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using Windows.Data.Pdf;
using Windows.Storage;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    public interface IImageCollection
    {
        string Name { get; }
        IEnumerable<IImageSource> GetAllImages();
    }

    public interface IImageCollectionDirectoryToken
    {
        string Key { get; }
    }

    public record ArchiveDirectoryToken(IArchive Archive, IArchiveEntry Entry) : IImageCollectionDirectoryToken
    {
        private string _key;
        public string Key => _key ??= (Entry?.Key is not null ? (Entry.IsDirectory ? Entry.Key : Path.GetDirectoryName(Entry.Key)) : null);
    }

    public interface IImageCollectionWithDirectory : IImageCollection
    {
        ArchiveDirectoryToken GetDirectoryTokenFromPath(string path);
        IEnumerable<ArchiveDirectoryToken> GetDirectoryPaths();
        List<IImageSource> GetImagesFromDirectory(ArchiveDirectoryToken token);
    }



    public sealed class ArchiveImageCollection : IImageCollectionWithDirectory, IDisposable
    {

        public StorageFile File { get; }
        public IArchive Archive { get; }

        private readonly CompositeDisposable _disposables;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly ImmutableList<ArchiveDirectoryToken> _directories;

        private readonly Dictionary<IImageCollectionDirectoryToken, List<IImageSource>> _entriesCacheByDirectory = new();
        public ArchiveImageCollection(StorageFile file, IArchive archive, CompositeDisposable disposables, FolderListingSettings folderListingSettings, ThumbnailManager thumbnailManager)
        {
            File = file;
            Archive = archive;
            _disposables = disposables;
            _folderListingSettings = folderListingSettings;
            _thumbnailManager = thumbnailManager;
            _rootDirectoryToken = new ArchiveDirectoryToken(Archive, null);

            // ディレクトリベースでフォルダ構造を見つける
            List<IArchiveEntry> notDirectoryItem = new List<IArchiveEntry>();
            List<IArchiveEntry> directoryItem = new List<IArchiveEntry>();
            foreach (var entry in Archive.Entries)
            {
                if (entry.IsDirectory)
                {
                    directoryItem.Add(entry);
                }
                else if (DirectoryPathHelper.GetDirectoryDepth(entry.Key) >= 1 && SupportedFileTypesHelper.IsSupportedImageFileExtension(entry.Key))
                {
                    notDirectoryItem.Add(entry);
                }
            }

            var dir = Enumerable.Concat(directoryItem, notDirectoryItem).Distinct(ArchiveDirectoryEqualityComparer.Default);

            // もしディレクトリベースのフォルダ構造が無い場合はファイル構造から見つける
            if (dir.Any() is false)
            {
                _directories = new[] { _rootDirectoryToken }.ToImmutableList();
            }
            else
            {
                _directories = dir.Select(x => new ArchiveDirectoryToken(Archive, x)).OrderBy(x => x.Key).ToImmutableList();
                if (_directories.Count == 1 && _directories[0].Entry.IsRootDirectoryEntry())
                {
                    _rootDirectoryToken = new ArchiveDirectoryToken(Archive, null);
                }
            }
        }



        private readonly ArchiveDirectoryToken _rootDirectoryToken;

        public string Name => File.Name;

        public ArchiveDirectoryToken GetDirectoryTokenFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) { return _rootDirectoryToken; }

            return _directories.FirstOrDefault(x => x.Key == path);
        }


        public IEnumerable<ArchiveDirectoryToken> GetSubDirectories(ArchiveDirectoryToken token)
        {
            token ??= _rootDirectoryToken;
            return _directories
                .Where(x => token != x)
                .Where(x => token.IsChildDirectoryPath(x));
        }

        public IEnumerable<ArchiveDirectoryToken> GetLeafFolders()
        {
            return _directories.Where(x => !GetSubDirectories(x).Any());
        }

        public IEnumerable<ArchiveDirectoryToken> GetDirectoryPaths()
        {
            return _directories;
        }

        public IImageSource GetThumbnailImageFromDirectory(ArchiveDirectoryToken token)
        {
            return GetImagesFromDirectory(token).FirstOrDefault();
        }

        public List<IImageSource> GetImagesFromDirectory(ArchiveDirectoryToken token)
        {
            token ??= _rootDirectoryToken;

            if (_entriesCacheByDirectory.TryGetValue(token, out var entries)) { return entries; }
            if (token != _rootDirectoryToken && _directories.Contains(token) is false) { throw new InvalidOperationException(); }

            var imageSourceItems = (token?.Key is null
                ? Archive.Entries.Where(x => x.IsRootDirectoryEntry())
                : Archive.Entries.Where(x => x.IsSameDirectoryPath(token.Entry))
                )
                .Where(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key))
                .Select(x => (IImageSource)new ArchiveEntryImageSource(x, token, this, _folderListingSettings, _thumbnailManager))
                .ToList();

            _entriesCacheByDirectory.Add(token, imageSourceItems);
            return imageSourceItems;
        }


        public void Dispose()
        {
            ((IDisposable)_disposables).Dispose();
        }

        public IEnumerable<IImageSource> GetAllImages()
        {
            if (_directories.Count == 0)
            {
                return GetImagesFromDirectory(_rootDirectoryToken);
            }
            /*
            else if (_directories.Count == 1 && IsRootDirectoryEntry(_directories[0].Entry))
            {
                return GetImagesFromDirectory(_rootDirectoryToken);
            }
            */
            else
            {
                return _directories.SelectMany(x => GetImagesFromDirectory(x)).ToList();
            }
        }
    }


    public sealed class PdfImageCollection : IImageCollection
    {
        private readonly PdfDocument _pdfDocument;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailManager _thumbnailManager;

        public PdfImageCollection(StorageFile file, PdfDocument pdfDocument, FolderListingSettings folderListingSettings, ThumbnailManager thumbnailManager)
        {
            _pdfDocument = pdfDocument;
            _folderListingSettings = folderListingSettings;
            File = file;
            _thumbnailManager = thumbnailManager;
        }
        public string Name => File.Name;

        public StorageFile File { get; }

        public IEnumerable<IImageSource> GetAllImages()
        {
            return Enumerable.Range(0, (int)_pdfDocument.PageCount)
              .Select(x => _pdfDocument.GetPage((uint)x))
              .Select(x => (IImageSource)new PdfPageImageSource(x, File, _folderListingSettings, _thumbnailManager));
        }
    }
}
