using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.Views.Helpers;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using CommunityToolkit.WinUI.Helpers;
using Microsoft.Toolkit.Mvvm.DependencyInjection;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SourceStorageItemsPage : Page
    {
        public SourceStorageItemsPage()
        {
            DataContext = _vm = Ioc.Default.GetService<SourceStorageItemsPageViewModel>();
            this.InitializeComponent();

            //this.FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging1;
        }

        private SourceStorageItemsPageViewModel _vm { get; }

        private void FoldersAdaptiveGridView_ContainerContentChanging1(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is StorageItemViewModel itemVM && itemVM.IsSourceStorageItem is false && itemVM.Name != null)
            {
                ToolTipService.SetToolTip(args.ItemContainer, new ToolTip() { Content = new TextBlock() { Text = itemVM.Name, TextWrapping = TextWrapping.Wrap } });
            }
        }

        private bool IsRequireSetFocus()
        {
            return Xamarin.Essentials.DeviceInfo.Idiom == Xamarin.Essentials.DeviceIdiom.TV
                || SystemInformation.Instance.DeviceFamily == "Windows.Xbox"
                || UINavigation.UINavigationManager.NowControllerConnected
                ;
        }

        CancellationTokenSource _navigationCts;

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();
            _navigationCts = null;

            base.OnNavigatingFrom(e);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _navigationCts?.Cancel();
            _navigationCts?.Dispose();
            _navigationCts = new CancellationTokenSource();
            var ct = _navigationCts.Token;

            try
            {
                if (IsRequireSetFocus())
                {
                    /*
                    await FoldersAdaptiveGridView.WaitFillingValue(x => x.Items.Any(), ct);
                    var firstItem = FoldersAdaptiveGridView.Items.First();
                    await FoldersAdaptiveGridView.WaitFillingValue(x => x.ContainerFromItem(firstItem) != null, ct);
                    
                    // NavigationView.SelectionChanged が 実行され、MenuItemにフォーカスが移った後に
                    // 改めてページの表示アイテムにフォーカスを移したい
                    await Task.Delay(250);
                    var itemContainer = FoldersAdaptiveGridView.ContainerFromItem(firstItem) as Control;
                    if (itemContainer != null)
                    {
                        itemContainer.Focus(FocusState.Keyboard);
                    }
                    */
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}
