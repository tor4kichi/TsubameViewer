using Microsoft.Toolkit.Diagnostics;
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
using TsubameViewer.Models.Domain.Navigation;
using Windows.Data.Pdf;
using Windows.Storage;
using static TsubameViewer.Models.Domain.ImageViewer.ArchiveFileInnerStructureCache;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    public interface IImageCollection
    {
        string Name { get; }
        IEnumerable<IImageSource> GetAllImages();
        
        ValueTask<int> GetImageCountAsync(CancellationToken ct);
        ValueTask<IImageSource> GetImageAtAsync(int index, FileSortType sort, CancellationToken ct);
        ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct);
    }

    public interface IImageCollectionDirectoryToken
    {
        string Key { get; }
    }

    public record ArchiveDirectoryToken(string DirectoryPath, IArchive Archive, IArchiveEntry Entry, bool IsRoot) : IImageCollectionDirectoryToken
    {
        public string Key => DirectoryPath;

        public string Label => DirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);        
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

        private readonly ArchiveFileInnerSturcture _archiveFileInnerStructure;
        private readonly CompositeDisposable _disposables;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly ImmutableList<ArchiveDirectoryToken> _directories;

        private readonly Dictionary<IImageCollectionDirectoryToken, List<IImageSource>> _entriesCacheByDirectory = new();

        private readonly IImageSource[] _imageSourcesCache;
        private readonly ImmutableSortedDictionary<string, int> _KeyToIndex;

        private readonly ImmutableArray<int> _imageEntryIndexSortWithDateTime;
        private readonly ImmutableArray<int> _imageEntryIndexSortWithTitle;
        private readonly ImmutableSortedDictionary<string, int[]> _FileByFolder;
        public ArchiveImageCollection(
            StorageFile file, 
            IArchive archive,
            ArchiveFileInnerSturcture archiveFileInnerStructure,
            CompositeDisposable disposables, 
            FolderListingSettings folderListingSettings, 
            ThumbnailManager thumbnailManager            
            )
        {
            File = file;
            Archive = archive;
            _archiveFileInnerStructure = archiveFileInnerStructure;
            _disposables = disposables;
            _folderListingSettings = folderListingSettings;
            _thumbnailManager = thumbnailManager;
            
            // アーカイブのフォルダ構造を見つける
            var structure = _archiveFileInnerStructure;
            _KeyToIndex = structure.Items.Select((x, i) => (Key: x, Index: i)).ToImmutableSortedDictionary(x => x.Key, x => x.Index);
            //_IndexToKey = structure.Items.ToImmutableArray();
            
            HashSet<int> supportedFileIndexies = new();
            int imagesCount = 0;
            foreach (var index in structure.FileIndexies)
            {
                var key = structure.Items[index];
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(key))
                {
                    supportedFileIndexies.Add(index);

                    if (DirectoryPathHelper.GetDirectoryDepth(key) >= 1)
                    {
                        var directoryName = Path.GetDirectoryName(key);                        
                    }

                    imagesCount++;
                }
            }



            _imageEntryIndexSortWithDateTime = structure.FileIndexiesSortWithDateTime.Where(supportedFileIndexies.Contains).ToImmutableArray();
            _imageEntryIndexSortWithTitle = structure.FileIndexies.Where(supportedFileIndexies.Contains).Select(x => structure.Items[x]).OrderBy(x => x).Select(x => _KeyToIndex[x]).ToImmutableArray();
            _FileByFolder = structure.FilesByFolder.Select(x => (x.Key, x.Value.Where(x => supportedFileIndexies.Contains(x)))).Where(x => x.Item2.Any()).ToImmutableSortedDictionary(x => x.Key, x => x.Item2.ToArray());
            if (string.IsNullOrEmpty(structure.RootDirectoryPath))
            {
                if (_FileByFolder.Count == 1)
                {
                    RootDirectoryToken = new ArchiveDirectoryToken(_FileByFolder.First().Key, Archive, null, true); ;
                }
                else
                {
                    RootDirectoryToken = new ArchiveDirectoryToken(string.Empty, Archive, null, true);
                }
            }
            else
            {
                RootDirectoryToken = new ArchiveDirectoryToken(structure.RootDirectoryPath, Archive, GetEntryFromKey(structure.RootDirectoryPath), true);
            }

            _imagesCount = imagesCount;
            _imageSourcesCache = new IImageSource[structure.Items.Length];
            if (_FileByFolder.Any() is false)
            {
                var entry = Archive.Entries.First();
                RootDirectoryToken ??= new ArchiveDirectoryToken(entry.GetDirectoryPath(), Archive, entry, true);
                _directories = new[] { RootDirectoryToken }.ToImmutableList();
            }
            else if (_FileByFolder.Count == 1)
            {
                var entry = GetEntryFromKey(_FileByFolder.Keys.ElementAt(0));
                RootDirectoryToken ??= new ArchiveDirectoryToken(entry.GetDirectoryPath(), Archive, entry, true);
                _directories = new[] { RootDirectoryToken }.ToImmutableList();
            }
            else
            {
                _directories = _FileByFolder.Where(x => x.Value.Any()).Select(x => x.Key).Select(x => 
                {
                    if (x.EndsWith(Path.DirectorySeparatorChar) is false && x.EndsWith(Path.AltDirectorySeparatorChar) is false)
                    {
                        return x + structure.FolderPathSeparator;
                    }
                    else
                    {
                        return x;
                    }
                })
                    .SelectMany(x => 
                    {
                        var sepChar = structure.FolderPathSeparator;
                        var dirNames = _archiveFileInnerStructure.ReplaceSeparateCharIfAltPathSeparateChar(x).Split(sepChar, StringSplitOptions.RemoveEmptyEntries);
                        for (var i = 1; i < dirNames.Length; i++)
                        {                            
                            dirNames[i] = $"{dirNames[i - 1]}{sepChar}{dirNames[i]}";
                        }

                        return dirNames;
                    })                    
                    .Select(GetEntryFromKey)
                    .Select(x => new ArchiveDirectoryToken(
                        _archiveFileInnerStructure.ReplaceSeparateCharIfAltPathSeparateChar(x.GetDirectoryPath()), Archive, x, false)
                        )
                    .Distinct()
                    .OrderBy(x => x.Key).ToImmutableList();

                RootDirectoryToken ??= new ArchiveDirectoryToken(string.Empty, Archive, null, true);
            }
        }

        public int GetImageCount() => _imagesCount;


        private IArchiveEntry GetEntryFromKey(string key)
        {
            return Archive.Entries.ElementAt(GetIndexFromKey(key));
        }

        private int GetIndexFromKey(string key)
        {
            if (_KeyToIndex.TryGetValue(key, out int index))
            {
                return index;
            }
            else
            {
                var find = _KeyToIndex.FirstOrDefault(x => x.Key.StartsWith(key));
                Guard.IsNotNullOrEmpty(find.Key, nameof(find.Key));
                return find.Value;
            }
        }

        public ArchiveDirectoryToken RootDirectoryToken { get; }

        public string Name => File.Name;

        public ArchiveDirectoryToken GetDirectoryTokenFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) { return RootDirectoryToken; }
            if (RootDirectoryToken.Key == path) { return RootDirectoryToken; }

            return _directories.FirstOrDefault(x => x.Key == path)
                ?? _directories.FirstOrDefault(x => x.Key?.StartsWith(path) ?? false);
        }


        public IEnumerable<ArchiveDirectoryToken> GetSubDirectories(ArchiveDirectoryToken token)
        {
            token ??= RootDirectoryToken;
            return _directories
                .Where(x => token.DirectoryPath != x.DirectoryPath)
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

        public bool IsExistImageFromDirectory(ArchiveDirectoryToken token)
        {            
            return _FileByFolder.TryGetValue(token.DirectoryPath, out var indexies)
                ? indexies.Any()
                : false;
        }

        public List<IImageSource> GetImagesFromDirectory(ArchiveDirectoryToken token)
        {
            token ??= RootDirectoryToken;

            if (_entriesCacheByDirectory.TryGetValue(token, out var entries)) { return entries; }
            if (token != RootDirectoryToken && _directories.Contains(token) is false) { throw new InvalidOperationException(); }

            //if (token?.Key is not null && _FileByFolder.Keys.FirstOrDefault(x => token.Key.StartsWith(x)) is not null and var folderKey)
            //{
            //    var filesIndexies = _FileByFolder[folderKey];
            //    var imageSourceItems = filesIndexies.Select(x => GetImageAt(x, FileSortType.None, token)).ToList();
            //    _entriesCacheByDirectory.Add(token, imageSourceItems);
            //    return imageSourceItems;
            //}
            //else
            {
                var imageSourceItems = (token.IsRoot
                    ? Archive.Entries.Where(x => x.IsRootDirectoryEntry() || x.IsSameDirectoryPath(token.Entry))
                    : Archive.Entries.Where(x => x.IsSameDirectoryPath(token.Entry))
                    )
                    .Where(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key))
                    .Select(x =>
                    {
                        var index = GetIndexFromKey(x.Key);
                        if (_imageSourcesCache[index] is not null and var image)
                        {
                            return image;
                        }

                        var dirToken = GetDirectoryTokenFromPath(_archiveFileInnerStructure.ReplaceSeparateCharIfAltPathSeparateChar(Path.GetDirectoryName(x.Key)));
                        return _imageSourcesCache[index] = new ArchiveEntryImageSource(x, dirToken ?? token, this, _folderListingSettings, _thumbnailManager);
                    })
                    .ToList();

                _entriesCacheByDirectory.Add(token, imageSourceItems);
                return imageSourceItems;
            }
        }


        public void Dispose()
        {
            ((IDisposable)_disposables).Dispose();
        }

        public IEnumerable<IImageSource> GetAllImages()
        {
            if (_directories.Count == 0)
            {
                return GetImagesFromDirectory(RootDirectoryToken);
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

        private readonly int _imagesCount;
        public ValueTask<int> GetImageCountAsync(CancellationToken ct)
        {
            return new(_imagesCount);
        }

        public ValueTask<IImageSource> GetImageAtAsync(int index, FileSortType sort, CancellationToken ct)
        {
            return new(GetImageAt(index, sort));
        }

        private IImageSource GetImageAt(int index, FileSortType sort, ArchiveDirectoryToken token = null)
        {
            var sortedIndex = ToSortedIndex(index, sort);
            if (_imageSourcesCache[sortedIndex] is not null and var image)
            {
                return image;
            }

            var imageEntry = Archive.Entries.ElementAt(sortedIndex);

            Guard.IsFalse(imageEntry.IsDirectory, nameof(imageEntry.IsDirectory));

            token ??= GetDirectoryTokenFromPath(_archiveFileInnerStructure.ReplaceSeparateCharIfAltPathSeparateChar(Path.GetDirectoryName(imageEntry.Key)));
            return _imageSourcesCache[sortedIndex] = new ArchiveEntryImageSource(imageEntry, token, this, _folderListingSettings, _thumbnailManager);            
        }
       
        // Note: FileImagesに対するIndexであってArchive.Entries全体に対するIndexではない
        public ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
        {
            foreach (var i in Enumerable.Range(0, _imagesCount))
            {
                var sortedIndex = ToSortedIndex(i, sort);
                var imageEntry = Archive.Entries.ElementAt(sortedIndex);
                if (key == imageEntry.Key || imageEntry.Key.StartsWith(key))
                {
                    return new (i);
                }
            }

            throw new InvalidOperationException();
        }

        private int ToSortedIndex(int index, FileSortType sort)
        {
            return sort switch
            {
                FileSortType.None => index,
                FileSortType.TitleAscending => _imageEntryIndexSortWithTitle[index],
                FileSortType.TitleDecending => _imageEntryIndexSortWithTitle[_imageEntryIndexSortWithTitle.Length - index - 1],
                FileSortType.UpdateTimeAscending => _imageEntryIndexSortWithDateTime[index],
                FileSortType.UpdateTimeDecending => _imageEntryIndexSortWithDateTime[_imageEntryIndexSortWithDateTime.Length - index - 1],
                _ => throw new NotSupportedException(sort.ToString()),
            };
        }
    }


    public sealed class PdfImageCollection : IImageCollection
    {
        private readonly PdfDocument _pdfDocument;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly ImageViewerSettings _imageViewerSettings;
        private readonly ThumbnailManager _thumbnailManager;

        private readonly IImageSource[] _imageSourcesCache;
        public PdfImageCollection(StorageFile file, PdfDocument pdfDocument, FolderListingSettings folderListingSettings, ImageViewerSettings imageViewerSettings, ThumbnailManager thumbnailManager)
        {
            _pdfDocument = pdfDocument;
            _folderListingSettings = folderListingSettings;
            _imageViewerSettings = imageViewerSettings;
            File = file;
            _thumbnailManager = thumbnailManager;

            _imageSourcesCache = new IImageSource[_pdfDocument.PageCount];
        }
        public string Name => File.Name;

        public StorageFile File { get; }

        public IEnumerable<IImageSource> GetAllImages()
        {
            return Enumerable.Range(0, (int)_pdfDocument.PageCount)
              .Select(x => _pdfDocument.GetPage((uint)x))
              .Select(x => (IImageSource)new PdfPageImageSource(x, File, _folderListingSettings, _imageViewerSettings, _thumbnailManager));
        }


        public ValueTask<int> GetImageCountAsync(CancellationToken ct)
        {
            return new ((int)_pdfDocument.PageCount);
        }
        
        public ValueTask<IImageSource> GetImageAtAsync(int index, FileSortType sort, CancellationToken ct)
        {
            return new(GetImageAt(index));
        }

        private IImageSource GetImageAt(int index)
        {
            var page = _pdfDocument.GetPage((uint)index);
            return _imageSourcesCache[index] = new PdfPageImageSource(page, File, _folderListingSettings, _imageViewerSettings, _thumbnailManager);
        }

        public ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
        {
            var index = int.Parse(key);
            return new (sort switch
            {
                FileSortType.None => index,
                FileSortType.TitleAscending => index,
                FileSortType.TitleDecending => (int)_pdfDocument.PageCount - index - 1,
                FileSortType.UpdateTimeAscending => index,
                FileSortType.UpdateTimeDecending => (int)_pdfDocument.PageCount - index - 1,
                _ => throw new NotSupportedException(sort.ToString())
            });
        }
    }
}
