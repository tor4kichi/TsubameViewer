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
using Microsoft.Toolkit.Uwp.UI.Animations.Effects;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Xamarin.Essentials;
using Microsoft.Toolkit.Uwp.UI.Extensions;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Reactive.Bindings.Extensions;
using Uno.Extensions;
using Uno.Threading;
using System.Threading.Tasks;
using TsubameViewer.Presentation.ViewModels;

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

            FileItemsRepeater_Small.ElementPrepared += FileItemsRepeater_ElementPrepared;
            FileItemsRepeater_Midium.ElementPrepared += FileItemsRepeater_ElementPrepared;
            FileItemsRepeater_Large.ElementPrepared += FileItemsRepeater_ElementPrepared;
        }

        private void FileItemsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is FrameworkElement fe
                && fe.DataContext is StorageItemViewModel itemVM
                )
            {
                itemVM.Initialize();
            }
        }

        private void Image_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var item = sender as FrameworkElement;
            item.Scale(1.020f, 1.020f, centerX: (float)item.ActualWidth * 0.5f, centerY: (float)item.ActualHeight * 0.5f, duration: 50)
                .Start();
        }

        private void Image_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var item = sender as FrameworkElement;
            item.Scale(1.0f, 1.0f, centerX: (float)item.ActualWidth * 0.5f, centerY: (float)item.ActualHeight * 0.5f, duration: 50)
                .Start();
        }

        private void MenuFlyout_Opened(object sender, object e)
        {
            var flyout = sender as FlyoutBase;
            var pageVM = DataContext as FolderListupPageViewModel;
            if (flyout.Target is ContentControl content)
            {
                var itemVM = (content.DataContext as StorageItemViewModel) ?? content.Content as StorageItemViewModel;
                if (itemVM == null)
                {
                    flyout.Hide();
                    return;
                }

                OpenImageViewerItem.Visibility = itemVM.Type == Models.Domain.StorageItemTypes.Folder ? Visibility.Visible : Visibility.Collapsed;
                OpenImageViewerItem.CommandParameter = itemVM;
                OpenImageViewerItem.Command = pageVM.AltOpenFolderItemCommand;
                
                OpenListupItem.Visibility = itemVM.Type == Models.Domain.StorageItemTypes.Archive ? Visibility.Visible : Visibility.Collapsed;
                OpenListupItem.CommandParameter = itemVM; 
                OpenListupItem.Command = pageVM.AltOpenFolderItemCommand;
            }
            else
            {
                flyout.Hide();
                return;
            }
        }
    }
}
