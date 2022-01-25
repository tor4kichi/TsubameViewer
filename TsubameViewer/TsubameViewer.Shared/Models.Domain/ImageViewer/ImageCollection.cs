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

    public record ArchiveDirectoryToken(IArchive Archive, IArchiveEntry Entry, bool IsRoot) : IImageCollectionDirectoryToken
    {
        private string _key;
        public string Key => _key ??= Entry?.Key;

        private string _label;
        public string Label => _label ??= (Entry?.Key is not null ? (Entry.IsDirectory ? Entry.Key : Path.GetDirectoryName(Entry.Key)) : null);
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
            HashSet<string> directories = new ();
            foreach (var index in structure.FolderIndexies)
            {
                directories.Add(structure.Items[index]);
            }

            HashSet<int> supportedFileIndexies = new();
            int imagesCount = 0;
            char? pathDirectorySeparator = null;
            Dictionary<string, int> fileCountByDirectory = new();
            foreach (var index in structure.FileIndexies)
            {
                var key = structure.Items[index];
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(key))
                {
                    supportedFileIndexies.Add(index);

                    if (DirectoryPathHelper.GetDirectoryDepth(key) >= 1)
                    {
                        var directoryName = Path.GetDirectoryName(key);                        
                        if (directories.Contains(directoryName) is false)
                        {                            
                            directories.Add(directoryName);
                        }

                        if (fileCountByDirectory.ContainsKey(directoryName) is false)
                        {
                            fileCountByDirectory.Add(directoryName, 0);
                        }
                        fileCountByDirectory[directoryName]++;
                    }


                    imagesCount++;

                    if (pathDirectorySeparator is null)
                    {
                        if (key.Any(c => c == Path.DirectorySeparatorChar))
                        {
                            pathDirectorySeparator = Path.DirectorySeparatorChar;
                        }
                        else if (key.Any(c => c == Path.AltDirectorySeparatorChar))
                        {
                            pathDirectorySeparator = Path.AltDirectorySeparatorChar;
                        }
                    }
                }
            }

            var rootDirectory = directories.FirstOrDefault(x => !fileCountByDirectory.ContainsKey(x));
            if (rootDirectory != null)
            {
                _rootDirectoryToken = new ArchiveDirectoryToken(Archive, GetEntryFromKey(rootDirectory), true);
            }

            _imageEntryIndexSortWithDateTime = structure.FileIndexiesSortWithDateTime.Where(supportedFileIndexies.Contains).ToImmutableArray();
            _imageEntryIndexSortWithTitle = structure.FileIndexies.Where(supportedFileIndexies.Contains).Select(x => structure.Items[x]).OrderBy(x => x).Select(x => _KeyToIndex[x]).ToImmutableArray();

            ImagesCount = imagesCount;
            _imageSourcesCache = new IImageSource[structure.Items.Length];
            // もしディレクトリベースのフォルダ構造が無い場合はファイル構造から見つける
            if (fileCountByDirectory.Any() is false)
            {
                _rootDirectoryToken ??= new ArchiveDirectoryToken(Archive, Archive.Entries.First(), true);
                _directories = new[] { _rootDirectoryToken }.ToImmutableList();
            }
            else if (fileCountByDirectory.Count == 1)
            {
                _rootDirectoryToken ??= new ArchiveDirectoryToken(Archive, GetEntryFromKey(fileCountByDirectory.Keys.ElementAt(0)), true);
                _directories = new[] { _rootDirectoryToken }.ToImmutableList();
            }
            else
            {
                _directories = fileCountByDirectory.Where(x => x.Value > 0).Select(x => x.Key).Select(x => 
                {
                    if (pathDirectorySeparator != null)
                    {
                        // セパレータがわかってる場合はKeyToIndexそのままで取れるであろう形でKeyを補完
                        if (x.EndsWith(Path.DirectorySeparatorChar) is false && x.EndsWith(Path.AltDirectorySeparatorChar) is false)
                        {
                            return x + pathDirectorySeparator.Value;
                        }
                        else
                        {
                            return x;
                        }
                    }
                    else
                    {
                        // セパレータ不明の場合はとにかくKeyとして引っかかるよう最後尾のセパレータを削る
                        if (x.EndsWith(Path.DirectorySeparatorChar) || x.EndsWith(Path.AltDirectorySeparatorChar))
                        {
                            return x.Substring(0, x.Length - 1);
                        }
                        else
                        {
                            return x;
                        }
                    }
                })
                    .Distinct()
                    .Select(x => new ArchiveDirectoryToken(Archive, GetEntryFromKey(x), false)).OrderBy(x => x.Key).ToImmutableList();

                _rootDirectoryToken ??= new ArchiveDirectoryToken(Archive, Archive.Entries.First(), true);
            }
        }




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

        private readonly ArchiveDirectoryToken _rootDirectoryToken;

        public string Name => File.Name;

        public ArchiveDirectoryToken GetDirectoryTokenFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) { return _rootDirectoryToken; }

            return _directories.FirstOrDefault(x => x.Key == path)
                ?? _directories.FirstOrDefault(x => x.Key?.StartsWith(path) ?? false);
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
                .Select(x =>
                {
                    var index = GetIndexFromKey(x.Key);
                    if (_imageSourcesCache[index] is not null and var image)
                    {
                        return image;
                    }

                    var dirToken = GetDirectoryTokenFromPath(Path.GetDirectoryName(x.Key));
                    return _imageSourcesCache[index] = new ArchiveEntryImageSource(x, dirToken ?? token, this, _folderListingSettings, _thumbnailManager);
                })
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

        private readonly int ImagesCount;
        public ValueTask<int> GetImageCountAsync(CancellationToken ct)
        {
            return new(ImagesCount);
        }

        public ValueTask<IImageSource> GetImageAtAsync(int index, FileSortType sort, CancellationToken ct)
        {
            return new(GetImageAt(index, sort));
        }

        private IImageSource GetImageAt(int index, FileSortType sort)
        {
            var sortedIndex = ToSortedIndex(index, sort);
            if (_imageSourcesCache[sortedIndex] is not null and var image)
            {
                return image;
            }

            var imageEntry = Archive.Entries.ElementAt(sortedIndex);

            Guard.IsFalse(imageEntry.IsDirectory, nameof(imageEntry.IsDirectory));

            var token = GetDirectoryTokenFromPath(Path.GetDirectoryName(imageEntry.Key));
            return _imageSourcesCache[sortedIndex] = new ArchiveEntryImageSource(imageEntry, token, this, _folderListingSettings, _thumbnailManager);            
        }
       
        // Note: FileImagesに対するIndexであってArchive.Entries全体に対するIndexではない
        public ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
        {
            foreach (var i in Enumerable.Range(0, ImagesCount))
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
        private readonly ThumbnailManager _thumbnailManager;

        private readonly IImageSource[] _imageSourcesCache;
        public PdfImageCollection(StorageFile file, PdfDocument pdfDocument, FolderListingSettings folderListingSettings, ThumbnailManager thumbnailManager)
        {
            _pdfDocument = pdfDocument;
            _folderListingSettings = folderListingSettings;
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
              .Select(x => (IImageSource)new PdfPageImageSource(x, File, _folderListingSettings, _thumbnailManager));
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
            return _imageSourcesCache[index] = new PdfPageImageSource(page, File, _folderListingSettings, _thumbnailManager);
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
