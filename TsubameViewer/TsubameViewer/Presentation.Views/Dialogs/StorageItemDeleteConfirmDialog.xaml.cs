using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TsubameViewer.Presentation.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// コンテンツ ダイアログの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace TsubameViewer.Presentation.Views.Dialogs
{
    public sealed partial class StorageItemDeleteConfirmDialog : ContentDialog, IStorageItemDeleteConfirmation
    {
        public StorageItemDeleteConfirmDialog()
        {
            this.InitializeComponent();
        }

        public async Task<(bool IsDeleteRequested, bool IsDoNotDisplayNextTimeRequested)> DeleteConfirmAsync(string title)
        {
            this.Title = title;
            var result = await this.ShowAsync();
            return (result is ContentDialogResult.Primary, this.DoNotDisplayFromNextTimeToggleButton.IsChecked is true);
        }
    }
}
