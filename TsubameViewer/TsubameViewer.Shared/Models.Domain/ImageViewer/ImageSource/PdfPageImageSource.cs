﻿using Microsoft.IO;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uno.Disposables;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class PdfPageImageSource : IImageSource, IDisposable
    {
        private readonly PdfPage _pdfPage;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;

        public PdfPageImageSource(PdfPage pdfPage, IStorageItem storageItem, RecyclableMemoryStreamManager recyclableMemoryStreamManager)
        {
            _pdfPage = pdfPage;
            Name = (_pdfPage.Index + 1).ToString();
            DateCreated = storageItem.DateCreated.DateTime;
            StorageItem = storageItem;
            _recyclableMemoryStreamManager = recyclableMemoryStreamManager;
        }

        public string Name { get; }
        public DateTime DateCreated { get; }
        public IStorageItem StorageItem { get; }

        public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct)
        {
            var memoryStream = _recyclableMemoryStreamManager.GetStream();
            using (var streamWrite = new StreamWriter(memoryStream))
            {
                await _pdfPage.RenderToStreamAsync(memoryStream.AsRandomAccessStream()).AsTask(ct);

                ct.ThrowIfCancellationRequested();

                await memoryStream.FlushAsync();
                memoryStream.Seek(0, SeekOrigin.Begin);

                ct.ThrowIfCancellationRequested();
            }

            return memoryStream.AsRandomAccessStream();
        }

        public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct)
        {
            var memoryStream = _recyclableMemoryStreamManager.GetStream();
            using (var streamWrite = new StreamWriter(memoryStream))
            {
                await _pdfPage.RenderToStreamAsync(memoryStream.AsRandomAccessStream()).AsTask(ct);

                ct.ThrowIfCancellationRequested();

                await memoryStream.FlushAsync();
                memoryStream.Seek(0, SeekOrigin.Begin);

                ct.ThrowIfCancellationRequested();
            }

            return memoryStream.AsRandomAccessStream();
        }

        public void Dispose()
        {
            ((IDisposable)_pdfPage).Dispose();
        }

    }
}
