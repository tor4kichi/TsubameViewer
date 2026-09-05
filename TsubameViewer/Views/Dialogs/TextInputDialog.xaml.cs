using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace TsubameViewer.Views.Dialogs;

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

    void TextInputDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        MyTextBox.Text = String.Empty;
    }

    public string GetInputText()
    {
        return MyTextBox.Text;
    }
    
    void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        this.Hide();
    }
}
