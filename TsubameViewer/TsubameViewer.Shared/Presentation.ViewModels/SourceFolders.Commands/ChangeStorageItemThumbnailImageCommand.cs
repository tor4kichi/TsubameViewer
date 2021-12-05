using Prism.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels.PageNavigation;

namespace TsubameViewer.Presentation.ViewModels.SourceFolders.Commands
{
    public sealed class ChangeStorageItemThumbnailImageCommand : DelegateCommandBase
    {
        private readonly ThumbnailManager _thumbnailManager;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;

        public ChangeStorageItemThumbnailImageCommand(
            ThumbnailManager thumbnailManager,
            SourceStorageItemsRepository sourceStorageItemsRepository
            ) 
        {
            _thumbnailManager = thumbnailManager;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel item)
            {
                using (var stream = await item.Item.GetThumbnailImageStreamAsync())
                {
                    if (item.Item is ArchiveEntryImageSource archiveEntry)
                    {
                        var parentDirectoryArchiveEntry = archiveEntry.GetParentDirectoryEntry();
                        if (parentDirectoryArchiveEntry == null)
                        {
                            await _thumbnailManager.SetThumbnailAsync(archiveEntry.StorageItem, stream, default);
                        }
                        else
                        {
                            await _thumbnailManager.SetArchiveEntryThumbnailAsync(archiveEntry.StorageItem, parentDirectoryArchiveEntry, stream, default);
                        }
                    }
                    else if (item.Item is PdfPageImageSource pdf)
                    {
                        await _thumbnailManager.SetThumbnailAsync(pdf.StorageItem, stream, default);
                    }
                    else if (item.Item is StorageItemImageSource folderItem)
                    {
                        var folder = await _sourceStorageItemsRepository.GetStorageItemFromPath(Path.GetDirectoryName(folderItem.Path));
                        if (folder == null) { throw new InvalidOperationException(); }
                        await _thumbnailManager.SetThumbnailAsync(folder, stream, default);
                    }
                    else if (item.Item is ArchiveDirectoryImageSource archiveDirectoryItem)
                    {
                        var parentDirectoryArchiveEntry = archiveDirectoryItem.GetParentDirectoryEntry();
                        if (parentDirectoryArchiveEntry == null)
                        {
                            await _thumbnailManager.SetThumbnailAsync(archiveDirectoryItem.StorageItem, stream, default);
                        }
                        else
                        {
                            await _thumbnailManager.SetArchiveEntryThumbnailAsync(archiveDirectoryItem.StorageItem, parentDirectoryArchiveEntry, stream, default);
                        }
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
            }
        }
    }
}
