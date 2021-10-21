using Microsoft.IO;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
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

    public sealed class ImageCollectionManager
    {
        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;

        public ImageCollectionManager(
            ThumbnailManager thumbnailManager,
            FolderContainerTypeManager folderContainerTypeManager,
            RecyclableMemoryStreamManager recyclableMemoryStreamManager
            )
        {
            _thumbnailManager = thumbnailManager;
            _folderContainerTypeManager = folderContainerTypeManager;
            _recyclableMemoryStreamManager = recyclableMemoryStreamManager;
        }

        public Task<ImageCollectionResult> GetImageSourcesForImageViewerAsync(IStorageItem storageItem, CancellationToken ct = default)
        {
            return GetImageSourcesAsync(storageItem, folderReturnOnlyImages: true, ct);
        }

        public Task<ImageCollectionResult> GetImageSourcesForFolderItemsListingAsync(IStorageItem storageItem, CancellationToken ct = default)
        {
            return GetImageSourcesAsync(storageItem, folderReturnOnlyImages: false, ct);
        }



        private async Task<ImageCollectionResult> GetImageSourcesAsync(IStorageItem storageItem, bool folderReturnOnlyImages, CancellationToken ct = default)
        {
            if (storageItem is StorageFile file)
            {
                // 画像ファイルを指定された場合だけ特殊対応として、その親フォルダの内容を列挙して返す
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType))
                {
                    // Note: 親フォルダへのアクセス権限無い場合が想定されるが
                    // 　　　アプリとしてユーザーに選択可能としているのはフォルダのみなので無視できる？
                    
                    // TODO: 外部からアプリに画像が渡された時、親フォルダへのアクセス権が無いケースに対応する
                    var parentFolder = await file.GetParentAsync();
                    
                    // 画像ファイルが選ばれた時、そのファイルの所属フォルダをコレクションとして表示する
                    var result = await Task.Run(async () => await GetFolderImagesAsync(parentFolder, ct));
                    try
                    {
                        List<IImageSource> images = new List<IImageSource>((int)result.ItemsCount);
                        int firstSelectedIndex = 0;
                        if (result.Images != null)
                        { 
                            int index = 0;
                            await foreach (var item in result.Images?.WithCancellation(ct))
                            {
                                images[index] = item;
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
                // 圧縮ファイルを展開した中身を列挙して返す
                else if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType))
                {
                    try
                    {
                        var result = await Task.Run(async () => await GetImagesFromArchiveFileAsync(file, ct));

                        result.Images.Sort(ImageSourceNameInterporatedComparer.Default);

                        return new ImageCollectionResult()
                        {
                            Images = result.Images.ToArray(),
                            ItemsEnumeratorDisposer = result.Disposer,
                            FirstSelectedIndex = 0,
                            ParentFolderOrArchiveName = file.Name
                        };
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }

                }
            }
            // フォルダ内のフォルダ、画像ファイル、圧縮ファイルを列挙して返す
            else if (storageItem is StorageFolder folder)
            {
                var result = folderReturnOnlyImages
                    ? await Task.Run(async () => await GetFolderImagesAsync(folder, ct))
                    : await Task.Run(async () => await GetFolderItemsAsync(folder, ct))
                    ;
                try
                {
                    var images = new IImageSource[result.ItemsCount];
                    int index = 0;

                    bool isAllImageFile = true;
                    await foreach (var item in result.Images.WithCancellation(ct))
                    {
                        images[index] = item;
                        index++;

                        isAllImageFile &= (item as StorageItemImageSource)?.ItemTypes == StorageItemTypes.Image;
                    }

                    // フォルダが画像のみを保持しているかどうかをローカルDBに設定する
                    _folderContainerTypeManager.SetContainerType(folder, isAllImageFile ? FolderContainerType.OnlyImages : FolderContainerType.Other);

                    return new ImageCollectionResult()
                    {
                        Images = images,
                        ItemsEnumeratorDisposer = Disposable.Empty,
                        FirstSelectedIndex = 0,
                        ParentFolderOrArchiveName = String.Empty,
                    };
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }

            throw new NotSupportedException();
        }


        

        public struct GetImagesFromArchiveResult
        {
            public uint ItemsCount { get; set; }
            public IDisposable Disposer { get; set; }
            public List<IImageSource> Images { get; set; }
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


        public async Task<(uint ItemsCount, IAsyncEnumerable<IImageSource> Images)> GetFolderItemsAsync(StorageFolder storageFolder, CancellationToken ct)
        {
#if WINDOWS_UWP
            var query = storageFolder.CreateItemQueryWithOptions(new QueryOptions(CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.GetAllSupportedFileExtensions()));
            var itemsCount = await query.GetItemCountAsync();
            return (itemsCount, AsyncEnumerableItems(itemsCount, query, ct));
#else
            return (itemsCount, AsyncEnumerableImages(
#endif
        }
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


        public async Task<(uint ItemsCount, IAsyncEnumerable<IImageSource> Images)> GetFolderImagesAsync(StorageFolder storageFolder, CancellationToken ct)
        {
#if WINDOWS_UWP
            var query = storageFolder?.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.SupportedImageFileExtensions));
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



        private async Task<GetImagesFromArchiveResult> GetImagesFromArchiveFileAsync(StorageFile file, CancellationToken ct)
        {
            var fileType = file.FileType.ToLower();
            var result = fileType switch
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

        


        public async Task<GetImagesFromArchiveResult> GetImagesFromZipFileAsync(StorageFile file)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync()
                .AddTo(disposables);
            var zipArchive = ZipArchive.Open(stream)
                .AddTo(disposables);
            
            var supportedEntries = zipArchive.Entries
                .Where(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key))
                .Select(x => (IImageSource)new ArchiveEntryImageSource(x, file, _recyclableMemoryStreamManager))
                .ToList();

            return new GetImagesFromArchiveResult()
            {
                ItemsCount = (uint)supportedEntries.Count,
                Disposer = disposables,
                Images = supportedEntries,
            };
        }

        public async Task<GetImagesFromArchiveResult> GetImagesFromPdfFileAsync(StorageFile file)
        {
            var pdfDocument = await PdfDocument.LoadFromFileAsync(file);

            var supportedEntries = Enumerable.Range(0, (int)pdfDocument.PageCount)
                .Select(x => pdfDocument.GetPage((uint)x))
                .Select(x => (IImageSource)new PdfPageImageSource(x, file, _recyclableMemoryStreamManager))
                .ToList();

            return new GetImagesFromArchiveResult()
            {
                ItemsCount = pdfDocument.PageCount,
                Disposer = Disposable.Empty,
                Images = supportedEntries,
            };
        }


        public async Task<GetImagesFromArchiveResult> GetImagesFromRarFileAsync(StorageFile file)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync()
                .AddTo(disposables);
            var rarArchive = RarArchive.Open(stream)
                .AddTo(disposables);


            var supportedEntries = rarArchive.Entries
                .Where(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key))
                .Select(x => (IImageSource)new ArchiveEntryImageSource(x, file, _recyclableMemoryStreamManager))
                .ToList();

            return new GetImagesFromArchiveResult()
            {
                ItemsCount = (uint)supportedEntries.Count,
                Disposer = disposables,
                Images = supportedEntries,
            };
        }


        public async Task<GetImagesFromArchiveResult> GetImagesFromSevenZipFileAsync(StorageFile file)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync()
                .AddTo(disposables);
            var zipArchive = SevenZipArchive.Open(stream)
                .AddTo(disposables);

            var supportedEntries = zipArchive.Entries
                .Where(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key))
                .Select(x => (IImageSource)new ArchiveEntryImageSource(x, file, _recyclableMemoryStreamManager))
                .ToList();

            return new GetImagesFromArchiveResult()
            {
                ItemsCount = (uint)supportedEntries.Count,
                Disposer = disposables,
                Images = supportedEntries,
            };
        }

        public async Task<GetImagesFromArchiveResult> GetImagesFromTarFileAsync(StorageFile file)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync()
                .AddTo(disposables);
            var zipArchive = TarArchive.Open(stream)
                .AddTo(disposables);

            var supportedEntries = zipArchive.Entries
                .Where(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key))
                .Select(x => (IImageSource)new ArchiveEntryImageSource(x, file, _recyclableMemoryStreamManager))
                .ToList();

            return new GetImagesFromArchiveResult()
            {
                ItemsCount = (uint)supportedEntries.Count,
                Disposer = disposables,
                Images = supportedEntries,
            };
        }

    }
}
