using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

namespace TsubameViewer.Presentation.Views.Dialogs
{
    public sealed partial class TextInputDialog : ContentDialog
    {
        public TextInputDialog(string title, string placeholder, string confirmButtonText, string defaultInputText = null)
        {
            this.InitializeComponent();
            Title = title;
            MyTextBox.Text = defaultInputText ?? string.Empty;
            MyTextBox.PlaceholderText = placeholder;
            PrimaryButtonText = confirmButtonText;

            CloseButtonClick += TextInputDialog_CloseButtonClick;
        }

        private void TextInputDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            MyTextBox.Text = String.Empty;
        }

        public string GetInputText()
        {
            return MyTextBox.Text;
        }
        
        private void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            this.Hide();
        }
    }
}
