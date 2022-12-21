using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TsubameViewer.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// コンテンツ ダイアログの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace TsubameViewer.Views.Dialogs
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
