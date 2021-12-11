using Microsoft.Toolkit.Diagnostics;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
//using System.IO.Compression;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    public class ImageCollectionResult
    {
        public IImageSource[] Images { get; set; }
        public int FirstSelectedIndex { get; set; }
        public string ParentFolderOrArchiveName { get; set; }
        public IDisposable ItemsEnumeratorDisposer { get; set; }

    }

    public interface IImageCollectionContext
    {
        string Name { get; }

        ValueTask<bool> IsExistImageFileAsync(CancellationToken ct);
        ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct);
        IAsyncEnumerable<IImageSource> GetAllImageFilesAsync(CancellationToken ct);

        IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct);

        IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct);
        IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct);

        bool IsSupportedFolderContentsChanged { get; }

        IObservable<Unit> CreateFolderAndArchiveFileChangedObserver();
        IObservable<Unit> CreateImageFileChangedObserver();
    }


    public sealed class ImageCollectionManager
    {

        public sealed class ArchiveImageCollectionContext : IImageCollectionContext, IDisposable
        {
            public ArchiveImageCollection ArchiveImageCollection { get; }
            public ArchiveDirectoryToken ArchiveDirectoryToken { get; }

            private readonly FolderListingSettings _folderListingSettings;
            private readonly ThumbnailManager _thumbnailManager;

            public string Name => ArchiveImageCollection.Name;


            public ArchiveImageCollectionContext(ArchiveImageCollection archiveImageCollection, ArchiveDirectoryToken archiveDirectoryToken, FolderListingSettings folderListingSettings, ThumbnailManager thumbnailManager)
            {
                ArchiveImageCollection = archiveImageCollection;
                ArchiveDirectoryToken = archiveDirectoryToken;
                _folderListingSettings = folderListingSettings;
                _thumbnailManager = thumbnailManager;
            }

            public IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct)
            {
                // アーカイブファイルは内部にフォルダ構造を持っている可能性がある
                // アーカイブ内のアーカイブは対応しない
                return ArchiveImageCollection.GetSubDirectories(ArchiveDirectoryToken)
                    .Select(x => (IImageSource)new ArchiveDirectoryImageSource(ArchiveImageCollection, x, _folderListingSettings, _thumbnailManager))
                    .ToAsyncEnumerable()                    
                    ;
            }

            public IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct)
            {
                return ArchiveImageCollection.GetLeafFolders()
                    .Select(x => (IImageSource)new ArchiveDirectoryImageSource(ArchiveImageCollection, x, _folderListingSettings, _thumbnailManager))
                    .ToAsyncEnumerable();
            }

            public IAsyncEnumerable<IImageSource> GetAllImageFilesAsync(CancellationToken ct)
            {
                return ArchiveImageCollection.GetAllImages().ToAsyncEnumerable();
            }

            public IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct)
            {
                return ArchiveImageCollection.GetImagesFromDirectory(ArchiveDirectoryToken).ToAsyncEnumerable();
            }

            public ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
            {
                return new (ArchiveImageCollection.GetSubDirectories(ArchiveDirectoryToken).Any());
            }

            public ValueTask<bool> IsExistImageFileAsync(CancellationToken ct)
            {
                return new (ArchiveImageCollection.GetImagesFromDirectory(ArchiveDirectoryToken).Any());
            }

            public bool IsSupportedFolderContentsChanged => false;

            public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver() => throw new NotSupportedException();

            public IObservable<Unit> CreateImageFileChangedObserver() => throw new NotSupportedException();

            public void Dispose()
            {
                ((IDisposable)ArchiveImageCollection).Dispose();
            }
        }

        public sealed class PdfImageCollectionContext : IImageCollectionContext
        {
            private readonly PdfImageCollection _pdfImageCollection;

            public PdfImageCollectionContext(PdfImageCollection pdfImageCollection)
            {
                _pdfImageCollection = pdfImageCollection;
            }

            public string Name => _pdfImageCollection.Name;

            public bool IsSupportedFolderContentsChanged => false;
            public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver() => throw new NotSupportedException();
            public IObservable<Unit> CreateImageFileChangedObserver() => throw new NotSupportedException();

            public IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct)
            {
                return AsyncEnumerable.Empty<IImageSource>();
            }

            public IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct)
            {
                return AsyncEnumerable.Empty<IImageSource>();
            }

            public IAsyncEnumerable<IImageSource> GetAllImageFilesAsync(CancellationToken ct)
            {
                return _pdfImageCollection.GetAllImages().ToAsyncEnumerable();
            }

            public IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct)
            {
                return _pdfImageCollection.GetAllImages().ToAsyncEnumerable();
            }

            public ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
            {
                return new (false);
            }

            public ValueTask<bool> IsExistImageFileAsync(CancellationToken ct)
            {
                return new (_pdfImageCollection.GetAllImages().Any());
            }
        }

        public sealed class FolderImageCollectionContext : IImageCollectionContext
        {
            private readonly FolderListingSettings _folderListingSettings;
            private readonly ThumbnailManager _thumbnailManager;
            private StorageItemQueryResult _folderAndArchiveFileSearchQuery;
            private StorageItemQueryResult FolderAndArchiveFileSearchQuery => _folderAndArchiveFileSearchQuery ??= Folder.CreateItemQueryWithOptions(ImageCollectionManager.FoldersAndArchiveFileSearchQueryOptions);

            private StorageFileQueryResult _imageFileSearchQuery;
            private StorageFileQueryResult ImageFileSearchQuery => _imageFileSearchQuery ??= Folder.CreateFileQueryWithOptions(ImageCollectionManager.ImageFileSearchQueryOptions);

            public string Name => Folder.Name;

            public FolderImageCollectionContext(StorageFolder storageFolder, FolderListingSettings folderListingSettings, ThumbnailManager thumbnailManager)
            {
                Folder = storageFolder;
                _folderListingSettings = folderListingSettings;
                _thumbnailManager = thumbnailManager;
            }

            public StorageFolder Folder { get; }

            public IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct)
            {
                return FolderAndArchiveFileSearchQuery.ToAsyncEnumerable(ct)
                    .Select(x => new StorageItemImageSource(x, _folderListingSettings, _thumbnailManager) as IImageSource);
            }

            public IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct)
            {
                return AsyncEnumerable.Empty<IImageSource>();
            }

            public IAsyncEnumerable<IImageSource> GetAllImageFilesAsync(CancellationToken ct)
            {
                return GetImageFilesAsync(ct);
            }

            public IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct)
            {
                return ImageFileSearchQuery.ToAsyncEnumerable(ct)
                    .Select(x => new StorageItemImageSource(x, _folderListingSettings, _thumbnailManager) as IImageSource);
            }

            public async ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
            {
                var count = await FolderAndArchiveFileSearchQuery.GetItemCountAsync().AsTask(ct);
                return count > 0;
            }

            public async ValueTask<bool> IsExistImageFileAsync(CancellationToken ct)
            {
                var count = await ImageFileSearchQuery.GetItemCountAsync().AsTask(ct);
                return count > 0;
            }

            public bool IsSupportedFolderContentsChanged => true;


            public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver()
            {
                return WindowsObservable.FromEventPattern<IStorageQueryResultBase, object>(h => FolderAndArchiveFileSearchQuery.ContentsChanged += h, h => FolderAndArchiveFileSearchQuery.ContentsChanged -= h).ToUnit();
            }

            public IObservable<Unit> CreateImageFileChangedObserver()
            {
                return WindowsObservable.FromEventPattern<IStorageQueryResultBase, object>(h => ImageFileSearchQuery.ContentsChanged += h, h => ImageFileSearchQuery.ContentsChanged -= h).ToUnit();
            }

        }

        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderListingSettings _folderListingSettings;

        public ImageCollectionManager(
            ThumbnailManager thumbnailManager, 
            FolderListingSettings folderListingSettings
            )
        {
            _thumbnailManager = thumbnailManager;
            _folderListingSettings = folderListingSettings;
        }

        public bool IsSupportGetArchiveImageCollectionContext(IStorageItem storageItem, CancellationToken ct)
        {
            if (storageItem is StorageFile file)
            {
                return file.IsSupportedMangaFile();
            }
            else
            {
                return false;
            }
        }

        public bool IsSupportGetImageCollectionContext(IStorageItem storageItem, CancellationToken ct)
        {
            if (storageItem is StorageFile file)
            {
                return file.IsSupportedMangaFile();
            }
            else if (storageItem is StorageFolder folder)
            {
                return true;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public async Task<IImageCollectionContext> GetArchiveImageCollectionContextAsync(StorageFile file, string? archiveDirectoryPath, CancellationToken ct)
        {
            Guard.IsTrue(file.IsSupportedMangaFile(), "file.IsSupportedMangaFile");

            // Task.Runで包まないとUIが固まる
            var imageCollection = await Task.Run(async () => await GetImagesFromArchiveFileAsync(file, ct), ct);
            if (imageCollection is ArchiveImageCollection aic)
            {
                var directoryToken = archiveDirectoryPath is not null ? aic.GetDirectoryTokenFromPath(archiveDirectoryPath) : null;
                if (archiveDirectoryPath is not null && directoryToken is null)
                {
                    throw new ArgumentException("not found directory in Archive file : " + archiveDirectoryPath);
                }
                return new ArchiveImageCollectionContext(aic, directoryToken, _folderListingSettings, _thumbnailManager);
            }
            else if (imageCollection is PdfImageCollection pdfImageCollection)
            {
                return new PdfImageCollectionContext(pdfImageCollection);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public Task<FolderImageCollectionContext> GetFolderImageCollectionContextAsync(StorageFolder folder, CancellationToken ct)
        {
            return Task.FromResult(new FolderImageCollectionContext(folder, _folderListingSettings, _thumbnailManager));
        }


        

        public static readonly QueryOptions ImageFileSearchQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.SupportedImageFileExtensions);

#if WINDOWS_UWP
        private async IAsyncEnumerable<IImageSource> AsyncEnumerableItems(uint count, StorageItemQueryResult queryResult, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var item in FolderHelper.ToAsyncEnumerable(queryResult, ct))
            {
                yield return new StorageItemImageSource(item, _folderListingSettings, _thumbnailManager);
            }
        }
#else
                
#endif

        public static readonly QueryOptions FoldersAndArchiveFileSearchQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, Enumerable.Concat(SupportedFileTypesHelper.SupportedArchiveFileExtensions, SupportedFileTypesHelper.SupportedEBookFileExtensions));


        private StorageFileQueryResult MakeImageFileSearchQueryResult(StorageFolder storageFolder)
        {
            return storageFolder.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.SupportedImageFileExtensions));
        }

        private async Task<(uint ItemsCount, IAsyncEnumerable<IImageSource> Images)> GetFolderImagesAsync(StorageFolder storageFolder, CancellationToken ct)
        {
#if WINDOWS_UWP
            var query = MakeImageFileSearchQueryResult(storageFolder);
            if (query == null) { return (0, null); }
            var itemsCount = await query.GetItemCountAsync();
            return (itemsCount, AsyncEnumerableImages(itemsCount, query, ct));
#else
            return (itemsCount, AsyncEnumerableImages(
#endif
        }
#if WINDOWS_UWP
        private async IAsyncEnumerable<IImageSource> AsyncEnumerableImages(uint count, StorageFileQueryResult queryResult, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var item in FolderHelper.ToAsyncEnumerable(queryResult, ct))
            {
                yield return new StorageItemImageSource(item as StorageFile, _folderListingSettings, _thumbnailManager);
            }
        }
#else
                
#endif



        private async Task<IImageCollection> GetImagesFromArchiveFileAsync(StorageFile file, CancellationToken ct)
        {
            var fileType = file.FileType.ToLower();
            IImageCollection result = fileType switch
            {
                SupportedFileTypesHelper.ZipFileType => await GetImagesFromZipFileAsync(file, ct),
                SupportedFileTypesHelper.RarFileType => await GetImagesFromRarFileAsync(file, ct),
                SupportedFileTypesHelper.PdfFileType => await GetImagesFromPdfFileAsync(file, ct),
                SupportedFileTypesHelper.CbzFileType => await GetImagesFromZipFileAsync(file, ct),
                SupportedFileTypesHelper.CbrFileType => await GetImagesFromRarFileAsync(file, ct),
                SupportedFileTypesHelper.SevenZipFileType => await GetImagesFromSevenZipFileAsync(file, ct),
                SupportedFileTypesHelper.Cb7FileType => await GetImagesFromSevenZipFileAsync(file, ct),
                SupportedFileTypesHelper.TarFileType => await GetImagesFromTarFileAsync(file, ct),
                _ => throw new NotSupportedException("not supported file type: " + file.FileType),
            };

            return result;
        }

        


        private async Task<ArchiveImageCollection> GetImagesFromZipFileAsync(StorageFile file, CancellationToken ct)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync();
            disposables.Add(stream);

            try
            {
                ct.ThrowIfCancellationRequested();
                var zipArchive = ZipArchive.Open(stream)
                    .AddTo(disposables);
                foreach (var _ in zipArchive.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return new ArchiveImageCollection(file, zipArchive, disposables, _folderListingSettings, _thumbnailManager);
            }
            catch
            {
                disposables.Dispose();
                throw;
            }
        }

        private async Task<PdfImageCollection> GetImagesFromPdfFileAsync(StorageFile file, CancellationToken ct)
        {
            var pdfDocument = await PdfDocument.LoadFromFileAsync(file).AsTask(ct);
            return new PdfImageCollection(file, pdfDocument, _folderListingSettings, _thumbnailManager);
        }


        private async Task<ArchiveImageCollection> GetImagesFromRarFileAsync(StorageFile file, CancellationToken ct)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync();
            disposables.Add(stream);

            try
            {
                var rarArchive = RarArchive.Open(stream)
                    .AddTo(disposables);
                foreach (var _ in rarArchive.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return new ArchiveImageCollection(file, rarArchive, disposables, _folderListingSettings, _thumbnailManager);
            }
            catch
            {
                disposables.Dispose();
                throw;
            }
        }


        private async Task<ArchiveImageCollection> GetImagesFromSevenZipFileAsync(StorageFile file, CancellationToken ct)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync();
            disposables.Add(stream);

            try
            {
                var szArchive = SevenZipArchive.Open(stream)
                    .AddTo(disposables);
                foreach (var _ in szArchive.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return new ArchiveImageCollection(file, szArchive, disposables, _folderListingSettings, _thumbnailManager);
            }
            catch
            {
                disposables.Dispose();
                throw;
            }
        }

        private async Task<ArchiveImageCollection> GetImagesFromTarFileAsync(StorageFile file, CancellationToken ct)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync();
            disposables.Add(stream);

            try
            {
                var tarArchive = TarArchive.Open(stream)
                    .AddTo(disposables);
                foreach (var _ in tarArchive.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return new ArchiveImageCollection(file, tarArchive, disposables, _folderListingSettings, _thumbnailManager);
            }
            catch
            {
                disposables.Dispose();
                throw;
            }
        }
    }    

    public interface IImageCollection
    {
        string Name { get; }
        IEnumerable<IImageSource> GetAllImages();
    }

    public interface IImageCollectionWithDirectory : IImageCollection
    {
        ArchiveDirectoryToken GetDirectoryTokenFromPath(string path);
        IEnumerable<ArchiveDirectoryToken> GetDirectoryPaths();
        List<IImageSource> GetImagesFromDirectory(ArchiveDirectoryToken token);
    }

    public interface IImageCollectionDirectoryToken
    {
        string Key { get; }
    }
    public record ArchiveDirectoryToken(IArchive Archive, IArchiveEntry Entry) : IImageCollectionDirectoryToken
    {
        private string _key;
        public string Key => _key ??= ( Entry?.Key is not null ? (Entry.IsDirectory ? Entry.Key : Path.GetDirectoryName(Entry.Key)) : null);
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

    public static class DirectoryPathHelper
    {
        public static bool IsSameDirectoryPath(string pathA, string pathB)
        {
            if (pathA == pathB) { return true; }

            bool pathAEmpty = string.IsNullOrEmpty(pathA);
            bool pathBEmpty = string.IsNullOrEmpty(pathB);
            if (pathAEmpty && pathBEmpty) { return true; }
            else if (pathAEmpty ^ pathBEmpty) { return false; }

            bool isSkipALastChar = pathA.EndsWith(Path.DirectorySeparatorChar) || pathA.EndsWith(Path.AltDirectorySeparatorChar);
            bool isSkipBLastChar = pathB.EndsWith(Path.DirectorySeparatorChar) || pathB.EndsWith(Path.AltDirectorySeparatorChar);
            if (isSkipALastChar && isSkipBLastChar)
            {
                if (Enumerable.SequenceEqual(pathA.SkipLast(1), pathB.SkipLast(1))) { return true; }
            }
            else if (isSkipALastChar)
            {
                if (Enumerable.SequenceEqual(pathA.SkipLast(1), pathB)) { return true; }
            }
            else if (isSkipBLastChar)
            {
                if (Enumerable.SequenceEqual(pathA, pathB.SkipLast(1))) { return true; }
            }

            return false;
        }

        public static bool IsSameDirectoryPath(IArchiveEntry x, IArchiveEntry y)
        {
            if (x == null && y == null) { throw new NotSupportedException(); }

            if (x == null)
            {
                return IsRootDirectoryEntry(y);
            }
            else if (y == null)
            {
                return IsRootDirectoryEntry(x);
            }
            else
            {
                var pathX = x.IsDirectory ? x.Key : Path.GetDirectoryName(x.Key);
                var pathY = y.IsDirectory ? y.Key : Path.GetDirectoryName(y.Key);

                //ReadOnlySpan<char> 
                return IsSameDirectoryPath(pathX, pathY);
            }
        }

        public static bool IsSameDirectoryPath(ArchiveDirectoryToken x, ArchiveDirectoryToken y)
        {
            if (x == null && y == null) { throw new NotSupportedException(); }

            if (x.Key == null)
            {
                return IsRootDirectoryEntry(y);
            }
            else if (x.Key == null)
            {
                return IsRootDirectoryEntry(x);
            }
            else
            {
                return IsSameDirectoryPath(x.Key, y.Key);
            }
        }

        public static bool IsChildDirectoryPath(ArchiveDirectoryToken parent, ArchiveDirectoryToken target)
        {
            if (parent.Key == null)
            {
                return IsRootDirectoryEntry(target);
            }

            return IsSameDirectoryPath(parent.Key, Path.GetDirectoryName(target.Key));
        }

        public static bool IsChildDirectoryPath(string parent, string target)
        {
            return IsSameDirectoryPath(parent, Path.GetDirectoryName(target));
        }

        public static bool IsRootDirectoryEntry(ArchiveDirectoryToken token)
        {
            if (token.Key == null) { return true; }
            else { return IsRootDirectoryEntry(token.Entry); }
        }

        public static bool IsRootDirectoryEntry(IArchiveEntry entry)
        {
            //return IsRootDirectoryPath(entry.IsDirectory ? entry.Key : Path.GetDirectoryName(entry.Key));
            return IsRootDirectoryPath(Path.GetDirectoryName(entry.Key));
        }

        public static bool IsRootDirectoryPath(string path)
        {
            if (path == String.Empty) 
            {
                return true; 
            }
            else if (path.EndsWith(Path.DirectorySeparatorChar)
                    || path.EndsWith(Path.AltDirectorySeparatorChar)
                    )
            {
                return true;
            }
            else
            {
                return false;
            }
        }



        public static int GetDirectoryDepth(string path)
        {
            return path.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
        }

    }

    public class ArchiveDirectoryEqualityComparer : IEqualityComparer<IArchiveEntry>
    {
        public static readonly ArchiveDirectoryEqualityComparer Default = new ArchiveDirectoryEqualityComparer();
        private ArchiveDirectoryEqualityComparer() { }

        public bool Equals(IArchiveEntry x, IArchiveEntry y)
        {
            return DirectoryPathHelper.IsSameDirectoryPath(x, y);
        }

        public int GetHashCode(IArchiveEntry obj)
        {
            var pathX = obj.IsDirectory ? obj.Key : Path.GetDirectoryName(obj.Key);
            if (pathX.EndsWith(Path.DirectorySeparatorChar))
            {
                return pathX.GetHashCode();
            }
            else if (pathX.EndsWith(Path.AltDirectorySeparatorChar))
            {
                return (pathX.Remove(pathX.Length - 1) + Path.DirectorySeparatorChar).GetHashCode();
            }
            else
            {
                return (pathX + Path.DirectorySeparatorChar).GetHashCode();
            }
        }
    }

    public sealed class ArchiveImageCollection : IImageCollectionWithDirectory, IDisposable
    {

        public StorageFile File { get; }
        public IArchive Archive { get; }

        private readonly CompositeDisposable _disposables;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly ImmutableList<ArchiveDirectoryToken> _directories;

        private readonly Dictionary<IImageCollectionDirectoryToken, List<IImageSource>> _entriesCacheByDirectory = new ();
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
            _directories = dir.Select(x => new ArchiveDirectoryToken(Archive, x)).OrderBy(x => x.Key).ToImmutableList();
            if (_rootDirectoryToken == null || 
                (_directories.Count == 1 && DirectoryPathHelper.IsRootDirectoryEntry(_directories[0].Entry))
                )
            {
                _rootDirectoryToken = new ArchiveDirectoryToken(Archive, null);
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
                .Where(x => DirectoryPathHelper.IsChildDirectoryPath(token, x));
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
                ? Archive.Entries.Where(x => DirectoryPathHelper.IsRootDirectoryEntry(x))
                : Archive.Entries.Where(x => DirectoryPathHelper.IsSameDirectoryPath(x, token.Entry))
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

}
