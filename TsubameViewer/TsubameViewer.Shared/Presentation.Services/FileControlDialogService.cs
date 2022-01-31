using I18NPortable;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Presentation.Views.Dialogs;
using Windows.Storage;

namespace TsubameViewer.Presentation.Services
{
    public interface IStorageItemDeleteConfirmation
    {
        Task<(bool IsDeleteRequested, bool IsDoNotDisplayNextTimeRequested)> DeleteConfirmAsync(string itemName);
    }

    public sealed class FileControlDialogService
    {
        private readonly Lazy<IStorageItemDeleteConfirmation> _lazyStorageItemDeleteConfirmDialog;

        public FileControlDialogService(Lazy<IStorageItemDeleteConfirmation> lazyStorageItemDeleteConfirmDialog)
        {
            _lazyStorageItemDeleteConfirmDialog = lazyStorageItemDeleteConfirmDialog;
        }

        public async Task<(bool IsConfirm, bool IsAskTwiceDenied)> ConfirmFileDeletionAsync(IStorageItem storageItem)
        {
            var dialog = _lazyStorageItemDeleteConfirmDialog.Value;
            return await dialog.DeleteConfirmAsync("StorageItemDeleteConfirmTitleWithName".Translate(storageItem.Name));
        }
    }
}
