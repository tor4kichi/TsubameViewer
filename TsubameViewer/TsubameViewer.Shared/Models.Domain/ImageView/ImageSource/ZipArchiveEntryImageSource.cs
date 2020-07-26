﻿using Prism.Mvvm;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uno.Extensions;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageView.ImageSource
{
    public sealed class ZipArchiveEntryImageSource : BindableBase, IImageSource, IDisposable
    {
        public static async Task<ImageCollectionManager.GetImagesFromArchiveResult>
            GetImagesFromZipFileAsync(StorageFile file)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync()
                .AddTo(disposables);
            var zipArchive = new ZipArchive(stream)
                .AddTo(disposables);

            var supportedEntries = zipArchive.Entries
                .Where(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Name))
                .Select(x => (IImageSource)new ZipArchiveEntryImageSource(x))
                .OrderBy(x => x.Name)
                .ToArray();


            return new ImageCollectionManager.GetImagesFromArchiveResult()
            {
                ItemsCount = (uint)supportedEntries.Length,
                Disposer = disposables,
                Images = supportedEntries,
            };
        }

        private readonly ZipArchiveEntry _entry;
        public ZipArchiveEntryImageSource(ZipArchiveEntry entry)
        {
            _entry = entry;
        }

        void IDisposable.Dispose()
        {
            _cts.Dispose();
        }

        public string Name => _entry.Name;
        public bool IsImageGenerated => _image != null;


        private BitmapImage _image;
        public BitmapImage Image
        {
            get { return _image; }
            private set { SetProperty(ref _image, value); }
        }

        public void ClearImage()
        {
            Image = null;
        }

        CancellationTokenSource _cts = new CancellationTokenSource();
        public async Task<BitmapImage> GenerateBitmapImageAsync(int canvasWidth, int canvasHeight)
        {
            var ct = _cts.Token;
            {
                using (var entryStream = _entry.Open())
                using (var memoryStream = entryStream.ToMemoryStream())
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.SetSource(memoryStream.AsRandomAccessStream());
                    if (bitmapImage.PixelHeight > bitmapImage.PixelWidth)
                    {
                        bitmapImage.DecodePixelHeight = canvasHeight;
                    }
                    else
                    {
                        bitmapImage.DecodePixelWidth = canvasWidth;
                    }
                    return Image = bitmapImage;
                }
            }
        }
    }
}
