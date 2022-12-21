using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using TsubameViewer.Core.UseCases;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Helpers;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace TsubameViewer.Views
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class AlbamListupPage : Page
    {
        private readonly AlbamListupPageViewModel _vm;
        private readonly FocusHelper _focusHelper;

        public AlbamListupPage()
        {
            this.InitializeComponent();
            DataContext = _vm = Ioc.Default.GetService<AlbamListupPageViewModel>();
            _focusHelper = Ioc.Default.GetService<FocusHelper>();

            this.ItemsAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging1;
        }

        bool _isFirstItem = false;
        private void FoldersAdaptiveGridView_ContainerContentChanging1(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is StorageItemViewModel itemVM && _navigationCts.IsCancellationRequested is false)
            {
                if (itemVM.IsSourceStorageItem is false && itemVM.Name != null)
                {
                    ToolTipService.SetToolTip(args.ItemContainer, new ToolTip() { Content = new TextBlock() { Text = itemVM.Name, TextWrapping = TextWrapping.Wrap } });
                }

                itemVM.Initialize(_navigationCts.Token);

                if (_isFirstItem && itemVM.Type != Core.Models.StorageItemTypes.AddAlbam)
                {
                    _isFirstItem = false;
                    if (_focusHelper.IsRequireSetFocus())
                    {
                        args.ItemContainer.Focus(FocusState.Keyboard);
                    }
                }
            }
        }


        CancellationTokenSource _navigationCts;
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _navigationCts = new CancellationTokenSource();
            _isFirstItem = true;
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            _navigationCts.Cancel();
            _navigationCts.Dispose();
            base.OnNavigatingFrom(e);
        }
    }
}
