using System.Threading.Tasks;
using Windows.Storage;

namespace TsubameViewer.Contracts.Services;

public interface IFileControlDialogService
{
    Task<(bool IsConfirm, bool IsAskTwiceDenied)> ConfirmFileDeletionAsync(IStorageItem storageItem);
}