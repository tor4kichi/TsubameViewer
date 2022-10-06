using I18NPortable;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels.Notification;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Windows.Storage.Streams;

namespace TsubameViewer.Presentation.ViewModels.SourceFolders.Commands
{
    public sealed class ChangeStorageItemThumbnailImageCommand : CommandBase
    {
        private readonly IMessenger _messenger;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;


        public bool IsArchiveThumbnailSetToFile { get; set; }

        public ChangeStorageItemThumbnailImageCommand(
            IMessenger messenger,
            ThumbnailManager thumbnailManager,
            SourceStorageItemsRepository sourceStorageItemsRepository
            ) 
        {
            _messenger = messenger;
            _thumbnailManager = thumbnailManager;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
        }

        protected override bool CanExecute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            return parameter is IImageSource;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            if (parameter is IImageSource imageSource)
            {
                using (var imageMemoryStream = new InMemoryRandomAccessStream())
                {
                    // ネイティブコンパイル時かつ画像ビューア上からのサムネイル設定でアプリがハングアップを起こすため
                    // imageSource.GetThumbnailImageStreamAsync() は使用していない
                    using (var stream = await imageSource.GetImageStreamAsync())
                    {
                        await RandomAccessStream.CopyAsync(stream, imageMemoryStream);
                        imageMemoryStream.Seek(0);
                    }

                    bool requireTranscode = true;

                    if (imageSource is ArchiveEntryImageSource archiveEntry)
                    {
                        var parentDirectoryArchiveEntry = archiveEntry.GetParentDirectoryEntry();
                        if (IsArchiveThumbnailSetToFile || parentDirectoryArchiveEntry == null)
                        {
                            await _thumbnailManager.SetThumbnailAsync(archiveEntry.StorageItem, imageMemoryStream, requireTrancode: requireTranscode, default);
                        }
                        else
                        {
                            await _thumbnailManager.SetArchiveEntryThumbnailAsync(archiveEntry.StorageItem, parentDirectoryArchiveEntry, imageMemoryStream, requireTrancode: requireTranscode, default);
                        }
                    }
                    else if (imageSource is PdfPageImageSource pdf)
                    {
                        await _thumbnailManager.SetThumbnailAsync(pdf.StorageItem, imageMemoryStream, requireTrancode: requireTranscode, default);
                    }
                    else if (imageSource is StorageItemImageSource folderItem)
                    {
                        var folder = await _sourceStorageItemsRepository.TryGetStorageItemFromPath(Path.GetDirectoryName(folderItem.Path));
                        if (folder == null) { throw new InvalidOperationException(); }
                        await _thumbnailManager.SetThumbnailAsync(folder, imageMemoryStream, requireTrancode: requireTranscode, default);
                    }
                    else if (imageSource is ArchiveDirectoryImageSource archiveDirectoryItem)
                    {
                        var parentDirectoryArchiveEntry = archiveDirectoryItem.GetParentDirectoryEntry();
                        if (IsArchiveThumbnailSetToFile || parentDirectoryArchiveEntry == null)
                        {
                            await _thumbnailManager.SetThumbnailAsync(archiveDirectoryItem.StorageItem, imageMemoryStream, requireTrancode: requireTranscode, default);
                        }
                        else
                        {
                            await _thumbnailManager.SetArchiveEntryThumbnailAsync(archiveDirectoryItem.StorageItem, parentDirectoryArchiveEntry, imageMemoryStream, requireTrancode: requireTranscode, default);
                        }
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }

                    _messenger.SendShowTextNotificationMessage("ThumbnailImageChanged".Translate());
                }
            }
        }
    }
}
