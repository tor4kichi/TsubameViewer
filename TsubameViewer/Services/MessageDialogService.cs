using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Services;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;

namespace TsubameViewer.Services;


public sealed class MessageDialogService : IMessageDialogService
{
    public async Task<bool> ShowMessageDialogAsync(
        string message, 
        string primaryButtonText, 
        string cancelButtonText,
        bool isDefaultAsCancel = false,
        string title = "")
    {
        var dialog = new ContentDialog()
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = cancelButtonText,
            DefaultButton = isDefaultAsCancel ? ContentDialogButton.Secondary : ContentDialogButton.Primary
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}

