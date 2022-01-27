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
using Uno;
using Uno.Extensions;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
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
    public sealed partial class SourceStorageItemsPage : Page
    {
        public SourceStorageItemsPage()
        {
            this.InitializeComponent();

            this.FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging1;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var oldViewModel = _vm;
            _vm = args.NewValue as SourceStorageItemsPageViewModel;
            if (_vm != null && oldViewModel != _vm)
            {
                this.Bindings.Update();
            }
        }

        private SourceStorageItemsPageViewModel _vm { get; set; }

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
                || Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.DeviceFamily == "Windows.Xbox"
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
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}
