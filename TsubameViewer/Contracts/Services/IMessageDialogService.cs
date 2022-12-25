using System.Threading.Tasks;

namespace TsubameViewer.Contracts.Services;

public interface IMessageDialogService
{
    Task<MessageDialogResult> ShowMessageDialogAsync(string message, string[] CommandLabels, uint cancelCommandIndex = 0, uint defaultCommandIndex = 0);
}

public readonly struct MessageDialogResult
{
    public MessageDialogResult(bool isConfirm, uint resultCommandIndex)
    {
        IsConfirm = isConfirm;
        ResultCommandIndex = resultCommandIndex;
    }

    public bool IsConfirm { get; }
    public uint ResultCommandIndex { get; }
}