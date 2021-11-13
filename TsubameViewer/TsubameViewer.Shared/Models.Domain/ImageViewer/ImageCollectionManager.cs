using Microsoft.IO;
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
using System.IO;
//using System.IO.Compression;
using System.Linq;
using System.Reactive.Disposables;
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

        Task<bool> IsExistImageFileAsync(CancellationToken ct);
        Task<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct);

        Task<List<IImageSource>> GetImageFilesAsync(CancellationToken ct);

        Task<List<IImageSource>> GetFolderOrArchiveFilesAsync(CancellationToken ct);

        bool IsSupportedFolderContentsChanged { get; }

        IObservable<object> CreateFolderContentChangedObserver();
    }


    public sealed class ImageCollectionManager
    {

        public sealed class ArchiveImageCollectionContext : IImageCollectionContext, IDisposable
        {
            private readonly ArchiveImageCollection _archiveImageCollection;
            private readonly ArchiveDirectoryToken _archiveDirectoryToken;

            public string Name => _archiveImageCollection.Name;

            public ArchiveImageCollectionContext(ArchiveImageCollection archiveImageCollection, ArchiveDirectoryToken archiveDirectoryToken)
            {
                _archiveImageCollection = archiveImageCollection;
                _archiveDirectoryToken = archiveDirectoryToken;
            }

            public Task<List<IImageSource>> GetFolderOrArchiveFilesAsync(CancellationToken ct)
            {
                // アーカイブファイルは内部にフォルダ構造を持っている可能性がある
                // アーカイブ内のアーカイブは対応しない
                return Task.FromResult(_archiveImageCollection.GetDirectoryPaths().Where(x => Path.GetDirectoryName(x.Key) == _archiveDirectoryToken.Key)
                    .Select(x => (IImageSource)new ArchiveDirectoryImageSource())
                    .ToList()
                    );
            }

            public Task<List<IImageSource>> GetImageFilesAsync(CancellationToken ct)
            {
                return Task.FromResult(_archiveImageCollection.GetImagesFromDirectory(_archiveDirectoryToken));
            }

            public Task<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
            {
                return Task.FromResult(_archiveImageCollection.GetDirectoryPaths().Any());
            }

            public Task<bool> IsExistImageFileAsync(CancellationToken ct)
            {
                return Task.FromResult(_archiveImageCollection.GetImagesFromDirectory(_archiveDirectoryToken).Any());
            }

            public bool IsSupportedFolderContentsChanged => false;

            public IObservable<object> CreateFolderContentChangedObserver() => throw new NotSupportedException();

            public void Dispose()
            {
                ((IDisposable)_archiveImageCollection).Dispose();
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

            public IObservable<object> CreateFolderContentChangedObserver()
            {
                throw new NotSupportedException();
            }

            public Task<List<IImageSource>> GetFolderOrArchiveFilesAsync(CancellationToken ct)
            {
                throw new NotSupportedException();
            }

            public Task<List<IImageSource>> GetImageFilesAsync(CancellationToken ct)
            {
                return Task.FromResult(_pdfImageCollection.GetImagesFromRootDirectory());
            }

            public Task<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
            {
                return Task.FromResult(false);
            }

            public Task<bool> IsExistImageFileAsync(CancellationToken ct)
            {
                return Task.FromResult(_pdfImageCollection.GetImagesFromRootDirectory().Any());
            }
        }

        public sealed class FolderImageCollectionContext : IImageCollectionContext
        {
            private readonly ThumbnailManager _thumbnailManager;
            private StorageItemQueryResult _folderAndArchiveFileSearchQuery;
            private StorageItemQueryResult FolderAndArchiveFileSearchQuery => _folderAndArchiveFileSearchQuery ??= Folder.CreateItemQueryWithOptions(ImageCollectionManager.FoldersAndArchiveFileSearchQueryOptions);

            private StorageFileQueryResult _imageFileSearchQuery;
            private StorageFileQueryResult ImageFileSearchQuery => _imageFileSearchQuery ??= Folder.CreateFileQueryWithOptions(ImageCollectionManager.ImageFileSearchQueryOptions);

            public string Name => Folder.Name;

            public FolderImageCollectionContext(StorageFolder storageFolder, ThumbnailManager thumbnailManager)
            {
                Folder = storageFolder;
                _thumbnailManager = thumbnailManager;
            }

            public StorageFolder Folder { get; }

            public async Task<List<IImageSource>> GetFolderOrArchiveFilesAsync(CancellationToken ct)
            {
                var items = await FolderAndArchiveFileSearchQuery.GetItemsAsync().AsTask(ct);
                return items.Select(x => new StorageItemImageSource(x, _thumbnailManager) as IImageSource).ToList();
            }


            public async Task<List<IImageSource>> GetImageFilesAsync(CancellationToken ct)
            {
                var items = await ImageFileSearchQuery.GetFilesAsync().AsTask(ct);
                return items.Select(x => new StorageItemImageSource(x, _thumbnailManager) as IImageSource).ToList();
            }

            public async Task<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
            {
                var count = await FolderAndArchiveFileSearchQuery.GetItemCountAsync().AsTask(ct);
                return count > 0;
            }

            public async Task<bool> IsExistImageFileAsync(CancellationToken ct)
            {
                var count = await ImageFileSearchQuery.GetItemCountAsync().AsTask(ct);
                return count > 0;
            }

            public bool IsSupportedFolderContentsChanged => true;

            public IObservable<object> CreateFolderContentChangedObserver() => throw new NotImplementedException();
        }

        private readonly ThumbnailManager _thumbnailManager;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;

        public ImageCollectionManager(
            ThumbnailManager thumbnailManager,
            RecyclableMemoryStreamManager recyclableMemoryStreamManager
            )
        {
            _thumbnailManager = thumbnailManager;
            _recyclableMemoryStreamManager = recyclableMemoryStreamManager;
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

            var imageCollection = await GetImagesFromArchiveFileAsync(file, ct);
            if (imageCollection is ArchiveImageCollection aic)
            {
                var directoryToken = aic.GetDirectoryTokenFromPath(archiveDirectoryPath);
                if (archiveDirectoryPath is not null && directoryToken is null)
                {
                    throw new ArgumentException("not found directory in Archive file : " + archiveDirectoryPath);
                }
                return new ArchiveImageCollectionContext(aic, directoryToken as ArchiveDirectoryToken);
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
            return Task.FromResult(new FolderImageCollectionContext(folder, _thumbnailManager));
        }

        private async Task<ImageCollectionResult> GetParentFolderImagesAsync(StorageFile file, CancellationToken ct)
        {
            // Note: 親フォルダへのアクセス権限無い場合が想定されるが
            // 　　　アプリとしてユーザーに選択可能としているのはフォルダのみなので無視できる？

            // TODO: 外部からアプリに画像が渡された時、親フォルダへのアクセス権が無いケースに対応する
            var parentFolder = await file.GetParentAsync();

            // 画像ファイルが選ばれた時、そのファイルの所属フォルダをコレクションとして表示する
            var result = await GetFolderImagesAsync(parentFolder, ct);
            try
            {
                List<IImageSource> images = new List<IImageSource>();
                int firstSelectedIndex = 0;
                if (result.Images != null)
                {
                    int index = 0;
                    await foreach (var item in result.Images?.WithCancellation(ct))
                    {
                        images.Add(item);
                        if (item.Name == file.Name)
                        {
                            firstSelectedIndex = index;
                        }
                        index++;
                    }

                    images.Sort(ImageSourceNameInterporatedComparer.Default);
                }
                else
                {
                    images = new List<IImageSource>() { new StorageItemImageSource(file, _thumbnailManager) };
                }

                return new ImageCollectionResult()
                {
                    Images = images.ToArray(),
                    ItemsEnumeratorDisposer = Disposable.Empty,
                    FirstSelectedIndex = firstSelectedIndex,
                    ParentFolderOrArchiveName = parentFolder?.Name
                };
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }


        private class ImageSourceNameInterporatedComparer : IComparer<IImageSource>
        {
            public static readonly ImageSourceNameInterporatedComparer Default = new ImageSourceNameInterporatedComparer();
            private ImageSourceNameInterporatedComparer() { }
            public int Compare(IImageSource x, IImageSource y)
            {
                var xDictPath = Path.GetDirectoryName(x.Name);
                var yDictPath = Path.GetDirectoryName(y.Name);

                if (xDictPath != yDictPath)
                {
                    return String.CompareOrdinal(x.Name, y.Name);
                }

                static bool TryGetPageNumber(string name, out int pageNumber)
                {
                    int keta = 1;
                    int number = 0;
                    foreach (var i in name.Reverse().SkipWhile(c => !char.IsDigit(c)).TakeWhile(c => char.IsDigit(c)))
                    {
                        number += i * keta;
                        keta *= 10;
                    }

                    pageNumber = number;
                    return number > 0;
                }

                var xName = Path.GetFileNameWithoutExtension(x.Name);
                if (!TryGetPageNumber(xName, out int xPageNumber)) { return String.CompareOrdinal(x.Name, y.Name); }

                var yName = Path.GetFileNameWithoutExtension(y.Name);
                if (!TryGetPageNumber(yName, out int yPageNumber)) { return String.CompareOrdinal(x.Name, y.Name); }

                return xPageNumber - yPageNumber;
            }
        }

        public static readonly QueryOptions ImageFileSearchQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.SupportedImageFileExtensions);

#if WINDOWS_UWP
        private async IAsyncEnumerable<IImageSource> AsyncEnumerableItems(uint count, StorageItemQueryResult queryResult, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var item in FolderHelper.GetEnumerator(queryResult, count, ct))
            {
                yield return new StorageItemImageSource(item, _thumbnailManager);
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
            await foreach (var item in FolderHelper.GetEnumerator(queryResult, count, ct))
            {
                yield return new StorageItemImageSource(item as StorageFile, _thumbnailManager);
            }
        }
#else
                
#endif



        private async Task<IImageCollection> GetImagesFromArchiveFileAsync(StorageFile file, CancellationToken ct)
        {
            var fileType = file.FileType.ToLower();
            IImageCollection result = fileType switch
            {
                SupportedFileTypesHelper.ZipFileType => await GetImagesFromZipFileAsync(file),
                SupportedFileTypesHelper.RarFileType => await GetImagesFromRarFileAsync(file),
                SupportedFileTypesHelper.PdfFileType => await GetImagesFromPdfFileAsync(file),
                SupportedFileTypesHelper.CbzFileType => await GetImagesFromZipFileAsync(file),
                SupportedFileTypesHelper.CbrFileType => await GetImagesFromRarFileAsync(file),
                SupportedFileTypesHelper.SevenZipFileType => await GetImagesFromSevenZipFileAsync(file),
                SupportedFileTypesHelper.Cb7FileType => await GetImagesFromSevenZipFileAsync(file),
                SupportedFileTypesHelper.TarFileType => await GetImagesFromTarFileAsync(file),
                _ => throw new NotSupportedException("not supported file type: " + file.FileType),
            };

            return result;
        }

        


        private async Task<ArchiveImageCollection> GetImagesFromZipFileAsync(StorageFile file)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync()
                .AddTo(disposables);
            var zipArchive = ZipArchive.Open(stream)
                .AddTo(disposables);
            return new ArchiveImageCollection(file, zipArchive, disposables, _recyclableMemoryStreamManager);
        }

        private async Task<PdfImageCollection> GetImagesFromPdfFileAsync(StorageFile file)
        {
            var pdfDocument = await PdfDocument.LoadFromFileAsync(file);
            return new PdfImageCollection(file, pdfDocument, _recyclableMemoryStreamManager);
        }


        private async Task<ArchiveImageCollection> GetImagesFromRarFileAsync(StorageFile file)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync()
                .AddTo(disposables);
            var rarArchive = RarArchive.Open(stream)
                .AddTo(disposables);

            return new ArchiveImageCollection(file, rarArchive, disposables, _recyclableMemoryStreamManager);
        }


        private async Task<ArchiveImageCollection> GetImagesFromSevenZipFileAsync(StorageFile file)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync()
                .AddTo(disposables);
            var szArchive = SevenZipArchive.Open(stream)
                .AddTo(disposables);

            return new ArchiveImageCollection(file, szArchive, disposables, _recyclableMemoryStreamManager);
        }

        private async Task<ArchiveImageCollection> GetImagesFromTarFileAsync(StorageFile file)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync()
                .AddTo(disposables);
            var tarArchive = TarArchive.Open(stream)
                .AddTo(disposables);

            return new ArchiveImageCollection(file, tarArchive, disposables, _recyclableMemoryStreamManager);
        }
    }    

    public interface IImageCollection
    {
        string Name { get; }
        List<IImageSource> GetImagesFromRootDirectory();
    }

    public interface IImageCollectionWithDirectory : IImageCollection
    {
        IImageCollectionDirectoryToken GetDirectoryTokenFromPath(string path);
        IEnumerable<IImageCollectionDirectoryToken> GetDirectoryPaths();
        List<IImageSource> GetImagesFromDirectory(IImageCollectionDirectoryToken token);
    }

    public interface IImageCollectionDirectoryToken
    {
        string Key { get; }
    }
    public record ArchiveDirectoryToken(IArchive Archive, IArchiveEntry Entry) : IImageCollectionDirectoryToken
    {
        public static readonly ArchiveDirectoryToken RootDirectoryToken = new ArchiveDirectoryToken(null, null);
        public string Key => Entry.Key;
    }

    public sealed class PdfImageCollection : IImageCollection
    {
        private readonly PdfDocument _pdfDocument;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;

        public PdfImageCollection(StorageFile file, PdfDocument pdfDocument, RecyclableMemoryStreamManager recyclableMemoryStreamManager)
        {
            _pdfDocument = pdfDocument;
            File = file;
            _recyclableMemoryStreamManager = recyclableMemoryStreamManager;
        }
        public string Name => throw new NotImplementedException();

        public StorageFile File { get; }

        public List<IImageSource> GetImagesFromRootDirectory()
        {
            return Enumerable.Range(0, (int)_pdfDocument.PageCount)
              .Select(x => _pdfDocument.GetPage((uint)x))
              .Select(x => (IImageSource)new PdfPageImageSource(x, File, _recyclableMemoryStreamManager))
              .ToList();
        }
    }

    public sealed class ArchiveImageCollection : IImageCollectionWithDirectory, IDisposable
    {

        private readonly StorageFile _file;
        private readonly IArchive _archive;
        private readonly CompositeDisposable _disposables;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;
        private readonly ImmutableList<ArchiveDirectoryToken> _directories;

        private readonly Dictionary<IImageCollectionDirectoryToken, List<IImageSource>> _entriesCacheByDirectory = new ();
        public ArchiveImageCollection(StorageFile file, IArchive archive, CompositeDisposable disposables, RecyclableMemoryStreamManager recyclableMemoryStreamManager)
        {
            _file = file;
            _archive = archive;
            _disposables = disposables;
            _recyclableMemoryStreamManager = recyclableMemoryStreamManager;

            _directories = _archive.Entries.Where(x => x.IsDirectory).Select(x => new ArchiveDirectoryToken(_archive, x)).ToImmutableList();

        }

        public string Name => _file.Name;

        public IImageCollectionDirectoryToken GetDirectoryTokenFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) { return ArchiveDirectoryToken.RootDirectoryToken; }

            return _directories.FirstOrDefault(x => x.Entry.Key == path);
        }

        public IEnumerable<IImageCollectionDirectoryToken> GetDirectoryPaths()
        {
            return _directories;
        }

        public List<IImageSource> GetImagesFromDirectory(IImageCollectionDirectoryToken token)
        {
            if (_entriesCacheByDirectory.TryGetValue(token, out var entries)) { return entries; }
            if (token is ArchiveDirectoryToken adt && adt == ArchiveDirectoryToken.RootDirectoryToken) { return GetImagesFromRootDirectory(); }
            if (_directories.Contains(token) is false) { throw new InvalidOperationException(); }

            var imageSourceItems =_archive.Entries
                .Where(x => Path.GetDirectoryName(x.Key) == token.Key)
                .Select(x => (IImageSource)new ArchiveEntryImageSource(x, _file, _recyclableMemoryStreamManager))
                .ToList();

            _entriesCacheByDirectory.Add(token, imageSourceItems);
            return imageSourceItems;
        }

        public void Dispose()
        {
            ((IDisposable)_disposables).Dispose();
        }

        public List<IImageSource> GetImagesFromRootDirectory()
        {
            if (_entriesCacheByDirectory.TryGetValue(ArchiveDirectoryToken.RootDirectoryToken, out var entries)) { return entries; }

            var imageSourceItems = _archive.Entries
                .Where(x => x.IsDirectory is false && x.Key.Contains(Path.DirectorySeparatorChar) is false)
                .Select(x => (IImageSource)new ArchiveEntryImageSource(x, _file, _recyclableMemoryStreamManager))
                .ToList();

            _entriesCacheByDirectory.Add(ArchiveDirectoryToken.RootDirectoryToken, imageSourceItems);
            return imageSourceItems;
        }
    }

}
