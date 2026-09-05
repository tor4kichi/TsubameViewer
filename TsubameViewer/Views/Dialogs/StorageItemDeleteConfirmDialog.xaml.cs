using System;
using System.Threading.Tasks;
using TsubameViewer.Services;
using Windows.UI.Xaml.Controls;
namespace TsubameViewer.Views.Dialogs;

public sealed partial class StorageItemDeleteConfirmDialog : ContentDialog, IStorageItemDeleteConfirmation
{
    public StorageItemDeleteConfirmDialog()
    {
        this.InitializeComponent();
    }

    public async Task<(bool IsDeleteRequested, bool IsDeletePermanet)> DeleteConfirmAsync(string title)
    {
        this.Title = title;
        var result = await this.ShowAsync();
        return (result is ContentDialogResult.Primary, this.DeleteWithPermanentToggleButton.IsChecked is true);
    }
}
