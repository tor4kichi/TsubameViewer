using I18NPortable;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Services;
using TsubameViewer.Core.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
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
    public sealed partial class SplitImageInputDialog : ContentDialog
    {
        public SplitImageInputDialog()
        {
            this.InitializeComponent();
        }

        BookBindingDirection[] _bookBindingDirections = new[]
        {
            BookBindingDirection.Left,
            BookBindingDirection.Right,
        };

        private new IAsyncOperation<ContentDialogResult> ShowAsync()
        {
            return base.ShowAsync();
        }

        private new IAsyncOperation<ContentDialogResult> ShowAsync(ContentDialogPlacement placement)
        {
            return base.ShowAsync();
        }

        public async Task<SplitImageInputDialogResult> GetSplitImageInputAsync()
        {
            BindingDirectionComboBox.SelectedItem = _bookBindingDirections[0];
            if (EncoderTypeComboBox.ItemsSource == null)
            {
                var items = Enumerable.Concat(new[] { new EncoderData { DisplayName = "SplitImageInput_NoSelected".Translate() } }, GetAvairableEncoders()).ToList();
                EncoderTypeComboBox.ItemsSource = items;
                EncoderTypeComboBox.SelectedItem = items[0];
            }
            var result = await base.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                return new SplitImageInputDialogResult(true, NumberBox.Value == 0 ? null : NumberBox.Value, (BookBindingDirection)BindingDirectionComboBox.SelectedItem, ((EncoderData)EncoderTypeComboBox.SelectedItem).EncoderId);
            }
            else
            {
                return default;
            }
        }

        public static IList<EncoderData> GetAvairableEncoders()
        {
            var encoders = BitmapEncoder.GetEncoderInformationEnumerator();
            foreach (var encoder in encoders)
            {
                System.Diagnostics.Debug.WriteLine(String.Join('/', encoder.FileExtensions));
            }
            
            return encoders.SelectMany(x => x.FileExtensions
                .Where(ext => SupportedFileTypesHelper.IsSupportedImageFileExtension(ext))
                .Select(ext => new EncoderData { EncoderId = x.CodecId, DisplayName = $"{ext} ({x.FriendlyName})" })
            ).ToList();
        }
    }

    public struct EncoderData
    {
        public Guid? EncoderId;
        public string DisplayName;
    }

    
}
