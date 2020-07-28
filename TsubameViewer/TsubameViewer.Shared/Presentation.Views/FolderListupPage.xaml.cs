using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FolderListupPage : Page
    {
        public FolderListupPage()
        {
            this.InitializeComponent();
        }

        private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if ((args.Element as FrameworkElement)?.DataContext is StorageItemViewModel itemVM)
            {
                itemVM.Initialize();
                Debug.WriteLine("OnElementPrepared" + itemVM.Name);
            }
        }

        private void OnElementIndexChanged(ItemsRepeater sender, ItemsRepeaterElementIndexChangedEventArgs args)
        {
            if ((args.Element as FrameworkElement)?.DataContext is StorageItemViewModel itemVM)
            {
                Debug.WriteLine("OnElementIndexChanged" + itemVM.Name);
            }
        }

        private void OnElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
        {
            if ((args.Element as FrameworkElement)?.DataContext is StorageItemViewModel itemVM)
            {
                Debug.WriteLine("OnElementClearing : " + itemVM.Name);
            }
        }
    }
}
