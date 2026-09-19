using I18NPortable;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Services;
using Windows.Storage;

namespace TsubameViewer.Services;

public interface IStorageItemDeleteConfirmation
{
    Task<(bool IsDeleteRequested, bool IsDeletePermanet)> DeleteConfirmAsync(string itemName, bool isCheckedDeletePermanentAsDefault);
}

public sealed class FileControlDialogService : IFileControlDialogService
{
    readonly Lazy<IStorageItemDeleteConfirmation> _lazyStorageItemDeleteConfirmDialog;

    public FileControlDialogService(Lazy<IStorageItemDeleteConfirmation> lazyStorageItemDeleteConfirmDialog)
    {
        _lazyStorageItemDeleteConfirmDialog = lazyStorageItemDeleteConfirmDialog;
    }

    public async Task<(bool IsConfirm, bool IsDeletePermanet)> ConfirmFileDeletionAsync(IStorageItem storageItem, bool isCheckedDeletePermanentAsDefault)
    {
        var dialog = _lazyStorageItemDeleteConfirmDialog.Value;        
        return await dialog.DeleteConfirmAsync("StorageItemDeleteConfirmTitleWithName".Translate(storageItem.Name), isCheckedDeletePermanentAsDefault);
    }
}
