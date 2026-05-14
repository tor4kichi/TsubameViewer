using System.Threading.Tasks;

namespace TsubameViewer.Contracts.Services;

public interface IMessageDialogService
{
    Task<bool> ShowMessageDialogAsync(
        string message,
        string primaryButtonText,
        string cancelButtonText,
        bool isDefaultAsCancel = false,
        string title = "");
}
