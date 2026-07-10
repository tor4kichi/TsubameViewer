using CommunityToolkit.Diagnostics;
using R3;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Helpers;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.Navigation;
using VersOne.Epub;
using Windows.ApplicationModel.Payments;
using Windows.Storage;
using ZLinq;
using static TsubameViewer.Core.Models.ImageViewer.ArchiveFileInnerStructureCache;

namespace TsubameViewer.Core.Models.ImageViewer;

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

public sealed class ArchiveDirectoryToken : IImageCollectionDirectoryToken
{
    public ArchiveDirectoryToken? ParentToken { get; }
    public string DirectoryPath { get; }
    public IArchive Archive { get; }
    public IArchiveEntry? Entry { get; }
    public bool IsRoot { get; }

    public string Key => DirectoryPath;
    public string Label => DirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    // 新規：直下の子ディレクトリ
    public List<ArchiveDirectoryToken> Children { get; } = new();

    // 新規：直下ファイルのインデックス（Archive.Entries のインデックス）
    public int[] FileIndexes { get; set; } = Array.Empty<int>();

    public ArchiveDirectoryToken(string directoryPath, IArchive archive, IArchiveEntry? entry, ArchiveDirectoryToken? parentToken)
    {
        DirectoryPath = directoryPath ?? string.Empty;
        Archive = archive;
        Entry = entry;
        IsRoot = parentToken == null;
        ParentToken = parentToken;
    }
}

public interface IImageCollectionWithDirectory : IImageCollection
{
    ArchiveDirectoryToken GetDirectoryTokenFromPath(string path);
    IEnumerable<ArchiveDirectoryToken> GetDirectoryPaths();
    List<IImageSource> GetImagesFromDirectory(ArchiveDirectoryToken token);        
}


public sealed class ImageCollectionDirectoryTokenEqualityComparar : IEqualityComparer<IImageCollectionDirectoryToken>
{
    public bool Equals(IImageCollectionDirectoryToken x, IImageCollectionDirectoryToken y)
    {
        return x.Key.Equals(y.Key, StringComparison.Ordinal);
    }

    public int GetHashCode(IImageCollectionDirectoryToken obj)
    {
        return (int)HashHelper.CalculateFNV1a64(obj.Key);
    }
}

public sealed class ArchiveImageCollection : IImageCollectionWithDirectory, IDisposable
{
    public StorageFile File { get; }
    public IArchive Archive { get; }

    private readonly ArchiveFileInnerSturcture _archiveFileInnerStructure;
    private readonly CompositeDisposable _disposables;
    private readonly Dictionary<string, ArchiveDirectoryToken> _directoryMap = new(StringComparer.Ordinal);

    private readonly Dictionary<IImageCollectionDirectoryToken, List<IImageSource>> _entriesCacheByDirectory = new(new ImageCollectionDirectoryTokenEqualityComparar());

    private readonly IImageSource[] _imageSourcesCache;
    private readonly ImmutableSortedDictionary<string, int> _keyToIndex;

    private readonly ImmutableArray<int> _imageEntryIndexSortWithDateTime;
    private readonly ImmutableArray<int> _imageEntryIndexSortWithTitle;
    private readonly ImmutableSortedDictionary<string, int[]> _fileByFolder;
    private readonly Dictionary<string, List<ArchiveDirectoryToken>> _childrenMap = new(StringComparer.Ordinal);
    public ArchiveImageCollection(
        StorageFile file, 
        IArchive archive,
        ArchiveFileInnerSturcture archiveFileInnerStructure,
        CompositeDisposable disposables
        )
    {
        File = file;
        Archive = archive;
        _archiveFileInnerStructure = archiveFileInnerStructure;
        _disposables = disposables;
        
        // アーカイブのフォルダ構造を見つける
        var structure = _archiveFileInnerStructure;
        _keyToIndex = structure.Items.Select((x, i) => (Key: x, Index: i)).ToImmutableSortedDictionary(x => x.Key, x => x.Index);
        //_IndexToKey = structure.Items.ToImmutableArray();
        
        HashSet<int> supportedFileIndexies = new();
        int imagesCount = 0;
        foreach (var index in structure.FileIndexies)
        {
            var key = structure.Items[index];
            if (SupportedFileTypesHelper.IsSupportedImageFileExtension(key))
            {
                supportedFileIndexies.Add(index);
                imagesCount++;
            }
        }



        _imageEntryIndexSortWithDateTime = structure.FileIndexiesSortWithDateTime.Where(supportedFileIndexies.Contains).ToImmutableArray();
        _imageEntryIndexSortWithTitle = structure.FileIndexies.Where(supportedFileIndexies.Contains).Select(x => structure.Items[x]).OrderBy(x => x).Select(x => _keyToIndex[x]).ToImmutableArray();
        _fileByFolder = structure.FilesByFolder.Select(x => (x.Key, x.Value.Where(x => supportedFileIndexies.Contains(x)))).Where(x => x.Item2.Any()).ToImmutableSortedDictionary(x => x.Key, x => x.Item2.ToArray());        

        _imagesCount = imagesCount;
        _imageSourcesCache = new IImageSource[structure.Items.Length];
        // コンストラクタ内（抜粋）: トークン作成 -> FileIndexes 設定 -> 親子リンク構築
        var keys = _archiveFileInnerStructure.FilesByFolder.Keys
            .Select(k => _archiveFileInnerStructure.ReplaceSeparateCharIfAltPathSeparateChar(k))
            .OrderBy(k => k);
        
        // 各ディレクトリノードを作る（FileIndexes は fileByFolder から設定）
        foreach (var k in keys)
        {
            int? dirEntryIndex = null;
            if (_archiveFileInnerStructure.DirectoryEntryIndex != null &&
                _archiveFileInnerStructure.DirectoryEntryIndex.TryGetValue(k, out var idx))
                dirEntryIndex = idx;


            var parentToken = !string.IsNullOrEmpty(k) 
                ? (_directoryMap.TryGetValue(Path.GetDirectoryName(k), out var token) 
                    ? token 
                    : null) 
                : null;
            var entry = dirEntryIndex.HasValue ? Archive.Entries.ElementAt(dirEntryIndex.Value) : null;
            var node = new ArchiveDirectoryToken(k, Archive, entry, parentToken);

            if (_fileByFolder.TryGetValue(k, out var fileIdxs))
            {
                node.FileIndexes = fileIdxs;
            }

            _directoryMap[k] = node;
        }

        // Root を作成（従来ロジックに合わせる）
        if (_directoryMap.TryGetValue("", out var rootToken))
        {
            RootDirectoryToken = rootToken;
        }
        else
        {
            RootDirectoryToken = _directoryMap.First().Value;
        }
        

        // 親子リンク作成（親がなければ Root の直下に割り当て）
        foreach (var node in _directoryMap.Values)
        {
            var key = node.DirectoryPath ?? string.Empty;
            var trimmed = key.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string parentKey;
            if (string.IsNullOrEmpty(trimmed))
            {
                parentKey = string.Empty;
            }
            else
            {
                var parent = Path.GetDirectoryName(trimmed);
                parentKey = parent is null ? string.Empty : _archiveFileInnerStructure.ReplaceSeparateCharIfAltPathSeparateChar(parent);
            }

            if (parentKey == key) { continue; }

            if (!_directoryMap.TryGetValue(parentKey, out var parentNode))
            {
                if (RootDirectoryToken == node) { continue; }
                parentNode = RootDirectoryToken;
            }

            parentNode.Children.Add(node);
        }
    }

    public int GetImageCount() => _imagesCount;

    private IArchiveEntry GetEntryFromIndex(int index)
    {
        if (!Archive.IsComplete)
        {
            Archive.ExtractAllEntries();
        }
        return Archive.Entries.ElementAt(index);
    }

    private IArchiveEntry GetEntryFromKey(string key)
    {
        return Archive.Entries.ElementAt(GetIndexFromKey(_archiveFileInnerStructure.ReplaceSeparateCharIfAltPathSeparateChar(key)));
    }    

    private int GetIndexFromKey(string key)
    {
        if (_keyToIndex.TryGetValue(key, out int index))
        {
            return index;
        }
        else
        {
            var find = _keyToIndex.FirstOrDefault(x => x.Key.StartsWith(key, StringComparison.Ordinal));
            Guard.IsNotNullOrEmpty(find.Key, nameof(find.Key));
            return find.Value;
        }
    }

    public ArchiveDirectoryToken RootDirectoryToken { get; }

    public string Name => File.Name;

    public ArchiveDirectoryToken GetDirectoryTokenFromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return RootDirectoryToken;
        if (RootDirectoryToken.Key.Equals(path, StringComparison.Ordinal)) return RootDirectoryToken;

        if (_directoryMap.TryGetValue(path, out var node)) return node;

        // 部分一致で最初に見つかったものを返す（従来のフォールバック）
        var find = _directoryMap.Values.FirstOrDefault(x => x.Key?.StartsWith(path, StringComparison.Ordinal) ?? false);
        return find ?? RootDirectoryToken;
    }


    public IEnumerable<ArchiveDirectoryToken> GetSubDirectories(ArchiveDirectoryToken token)
    {
        token ??= RootDirectoryToken;
        return token.Children;
    }

    public IEnumerable<ArchiveDirectoryToken> GetLeafFolders()
    {
        return _directoryMap.Values.Where(x => x.Children.Count == 0);
    }

    public IEnumerable<ArchiveDirectoryToken> GetDirectoryPaths()
    {
        return _directoryMap.Values.Prepend(RootDirectoryToken);
    }

    public IImageSource GetThumbnailImageFromDirectory(ArchiveDirectoryToken token)
    {
        return GetImagesFromDirectory(token).FirstOrDefault();
    }

    public bool IsExistImageFromDirectory(ArchiveDirectoryToken token)
    {            
        return _fileByFolder.TryGetValue(token.DirectoryPath, out var indexies)
            ? indexies.Any()
            : false;
    }

    object _lock = new();
    public List<IImageSource> GetImagesFromDirectory(ArchiveDirectoryToken token)
    {
        lock (_lock)
        {
            token ??= RootDirectoryToken;
            if (token != RootDirectoryToken && !_directoryMap.ContainsValue(token)) throw new InvalidOperationException();

            if (_entriesCacheByDirectory.TryGetValue(token, out var cached)) return cached;

            var fileIndexes = token.FileIndexes ?? Array.Empty<int>();            
            var imageSourceItems = fileIndexes
                .Select(GetEntryFromIndex)
                .Where(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key))
                .Select(x =>
                {
                    var index = GetIndexFromKey(x.Key);
                    if (_imageSourcesCache[index] is not null and var image) return image;

                    var dirToken = GetDirectoryTokenFromPath(_archiveFileInnerStructure.ReplaceSeparateCharIfAltPathSeparateChar(Path.GetDirectoryName(x.Key)));
                    return _imageSourcesCache[index] = new ArchiveEntryImageSource(x, dirToken ?? token, this);
                })
                .ToList();

            _entriesCacheByDirectory[token] = imageSourceItems;
            return imageSourceItems;

        }
    }


    public void Dispose()
    {
        ((IDisposable)_disposables).Dispose();
    }

    public IEnumerable<IImageSource> GetAllImages()
    {
        if (_directoryMap.Count == 0)
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
            return _directoryMap.SelectMany(x => GetImagesFromDirectory(x.Value)).ToList();
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
        return _imageSourcesCache[sortedIndex] = new ArchiveEntryImageSource(imageEntry, token, this);            
    }
   
    // Note: FileImagesに対するIndexであってArchive.Entries全体に対するIndexではない
    public ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
    {
        foreach (var i in Enumerable.Range(0, _imagesCount))
        {
            var sortedIndex = ToSortedIndex(i, sort);
            var imageEntry = Archive.Entries.ElementAt(sortedIndex);
            if (key == imageEntry.Key || imageEntry.Key.StartsWith(key, StringComparison.Ordinal))
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
    private readonly SizeF[] _sizes;
    private readonly ImageViewerSettings _imageViewerSettings;

    private readonly IImageSource[] _imageSourcesCache;
    public PdfImageCollection(
        StorageFile file,
        SizeF[] sizes, 
        ImageViewerSettings imageViewerSettings 
        )
    {
        File = file;
        _sizes = sizes;
         _imageViewerSettings = imageViewerSettings;

        _imageSourcesCache = new IImageSource[_sizes.Length];
    }
    public string Name => File.Name;

    public StorageFile File { get; }

    public IEnumerable<IImageSource> GetAllImages()
    {
        return Enumerable.Range(0, (int)_sizes.Length)          
          .Select(x => (IImageSource)new PdfPageImageSource(x, _sizes[x], File, _imageViewerSettings));
    }


    public ValueTask<int> GetImageCountAsync(CancellationToken ct)
    {
        return new ((int)_sizes.Length);
    }
    
    public ValueTask<IImageSource> GetImageAtAsync(int index, FileSortType sort, CancellationToken ct)
    {
        return new(GetImageAt(index));
    }

    private IImageSource GetImageAt(int index)
    {
        //var page = _pdfDocument.GetPage((uint)index);
        Guard.IsBetweenOrEqualTo(index, 0, _sizes.Length - 1);
        return _imageSourcesCache[index] = new PdfPageImageSource(index, _sizes[index], File, _imageViewerSettings);
    }

    public ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
    {
        var index = int.Parse(key) - 1;
        return new (sort switch
        {
            FileSortType.None => index,
            FileSortType.TitleAscending => index,
            FileSortType.TitleDecending => (int)_sizes.Length - index - 1,
            FileSortType.UpdateTimeAscending => index,
            FileSortType.UpdateTimeDecending => (int)_sizes.Length - index - 1,
            _ => throw new NotSupportedException(sort.ToString())
        });
    }
}


public sealed class EPubImageCollection : IImageCollection
{
    public EPubImageCollection(StorageFile file, EpubBookRef epubBookRef)
    {
        File = file;
        EpubBookRef = epubBookRef;
    }

    public string Name => File.Name;

    public StorageFile File { get; }
    public EpubBookRef EpubBookRef { get; }

    public IEnumerable<IImageSource> GetAllImages()
    {
        foreach (var image in EpubBookRef.Content.Images.Local)
        {
            yield return new EpubLocalImageSource(File, image);
        }
    }

    public ValueTask<IImageSource> GetImageAtAsync(int index, FileSortType sort, CancellationToken ct)
    {
        return new(new EpubLocalImageSource(File, EpubBookRef.Content.Images.Local.ElementAtOrDefault(index)));
    }

    public ValueTask<int> GetImageCountAsync(CancellationToken ct)
    {
        return new (EpubBookRef.Content.Images.Local.Count);
    }

    public ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
    {
        var item = EpubBookRef.Content.Images.GetLocalFileRefByKey(key);
        return new(EpubBookRef.Content.Images.Local.IndexOf(item));
    }
}

public sealed class EpubLocalImageSource : IImageSource
{
    private readonly EpubLocalByteContentFileRef _imageFileRef;

    public EpubLocalImageSource(StorageFile file, EpubLocalByteContentFileRef imageFileRef)
    {
        File = file;
        _imageFileRef = imageFileRef;
        Path = PageNavigationConstants.MakeStorageItemIdWithPage(File.Path, imageFileRef.Key);
    }

    public IStorageItem StorageItem => File;

    public string Name => System.IO.Path.GetFileName(_imageFileRef.Key);

    public string Path { get; }

    public DateTime DateCreated => DateTime.Today;

    public SizeF? PreCulcuratedSize => null;

    public StorageFile File { get; }

    public bool Equals(IImageSource other)
    {
        return _imageFileRef == (other as EpubLocalImageSource)?._imageFileRef;
    }

    public ValueTask<Stream> GetImageStreamAsync(CancellationToken ct = default)
    {        
        return new (_imageFileRef.GetContentStream());
    }

    public ValueTask<SizeF?> TryGetSizedImageStreamAsync(int requestedSize, Stream imageStream, CancellationToken ct = default)
    {
        return default;
    }
}