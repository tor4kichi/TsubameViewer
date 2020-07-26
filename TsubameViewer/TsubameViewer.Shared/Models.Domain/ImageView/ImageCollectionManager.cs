using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using SharpCompress.Archives.Rar;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.ImageView.ImageSource;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageView
{
    public interface IImageSource : INotifyPropertyChanged
    {
        string Name { get; }
        BitmapImage Image { get; }
        void ClearImage();

        Task<BitmapImage> GenerateBitmapImageAsync(int canvasWidth, int canvasHeight);
    }


    public class ImageCollectionResult
    {
        public IImageSource[] Images { get; set; }
        public int FirstSelectedIndex { get; set; }
        public string ParentFolderOrArchiveName { get; set; }
        public IDisposable ItemsEnumeratorDisposer { get; set; }

    }

    public sealed class ImageCollectionManager
    {
        public async Task<ImageCollectionResult> GetImageSources(IStorageItem storageItem, CancellationToken ct = default)
        {
            if (storageItem is StorageFile file)
            {
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType))
                {
                    // Note: 親フォルダへのアクセス権限無い場合が想定されるが
                    // 　　　アプリとしてユーザーに選択可能としているのはフォルダのみなので無視できる？
                    
                    // TODO: 外部からアプリに画像が渡された時、親フォルダへのアクセス権が無いケースに対応する
                    var parentFolder = await file.GetParentAsync();
                    
                    // 画像ファイルが選ばれた時、そのファイルの所属フォルダをコレクションとして表示する
                    var result = await Task.Run(async () => await StorageFileImageSource.GetImagesFromFolderAsync(parentFolder, ct));
                    try
                    {
                        var images = new IImageSource[result.ItemsCount];
                        int index = 0;
                        int firstSelectedIndex = 0;
                        await foreach (var item in result.Images.WithCancellation(ct))
                        {
                            images[index] = item;
                            if (item.Name == file.Name)
                            {
                                firstSelectedIndex = index;
                            }
                            index++;
                        }

                        return new ImageCollectionResult()     
                        {
                            Images = images,
                            ItemsEnumeratorDisposer = Disposable.Empty,
                            FirstSelectedIndex = firstSelectedIndex,
                            ParentFolderOrArchiveName = parentFolder.Name
                        };
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }
                }
                else if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType))
                {
                    try
                    {
                        var result = await Task.Run(async () => await GetImagesFromArchiveFileAsync(file, ct));
                        return new ImageCollectionResult()
                        {
                            Images = result.Images,
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
            else if (storageItem is StorageFolder folder)
            {
                var result = await Task.Run(async () => await StorageFileImageSource.GetImagesFromFolderAsync(folder, ct));
                try
                {
                    var images = new IImageSource[result.ItemsCount];
                    int index = 0;
                    await foreach (var item in result.Images.WithCancellation(ct))
                    {
                        images[index] = item;
                        index++;
                    }

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
            public IImageSource[] Images { get; set; }
        }

        private async Task<GetImagesFromArchiveResult> GetImagesFromArchiveFileAsync(StorageFile file, CancellationToken ct)
        {
            var fileType = file.FileType.ToLower();
            var result = fileType switch
            {
                SupportedFileTypesHelper.ZipFileType => await ZipArchiveEntryImageSource.GetImagesFromZipFileAsync(file),
                SupportedFileTypesHelper.RarFileType => await RarArchiveEntryImageSource.GetImagesFromRarFileAsync(file),
                SupportedFileTypesHelper.PdfFileType => await PdfPageImageSource.GetImagesFromPdfFileAsync(file),
                _ => throw new NotSupportedException("not supported file type: " + file.FileType),
            };

            return result;
        }
    }
}
