using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Services;
using Windows.UI.Popups;

namespace TsubameViewer.Services;


public sealed class MessageDialogService : IMessageDialogService
{
    public async Task<MessageDialogResult> ShowMessageDialogAsync(string message, string[] CommandLabels, uint cancelCommandIndex = 0, uint defaultCommandIndex = 0)
    {
        var dialog = new MessageDialog(message)
        {
            CancelCommandIndex = cancelCommandIndex,
            DefaultCommandIndex = defaultCommandIndex,
        };

        foreach (var command in CommandLabels.Select(x => new UICommand(x)))
        {
            dialog.Commands.Add(command);
        }

        if (await dialog.ShowAsync() is IUICommand resultCommand)
        {
            return new MessageDialogResult(true, (uint)dialog.Commands.IndexOf(resultCommand));
        }
        else
        {
            return new MessageDialogResult(false, 0);
        }
    }
}

