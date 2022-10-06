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
using Xamarin.Essentials;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Reactive.Bindings.Extensions;
using System.Threading.Tasks;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using Windows.Storage;
using TsubameViewer.Presentation.Views.Helpers;
using Microsoft.Toolkit.Uwp.UI;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using Windows.UI.Xaml.Media.Animation;
using CommunityToolkit.Mvvm.DependencyInjection;
using TsubameViewer.Models.UseCase;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Presentation.ViewModels.Albam.Commands;
using TsubameViewer.Models.Domain;

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

            DataContext = _vm = Ioc.Default.GetService<FolderListupPageViewModel>();
            _focusHelper = Ioc.Default.GetService<FocusHelper>();
            this.FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging1;
        }

        private readonly FolderListupPageViewModel _vm;
        private readonly FocusHelper _focusHelper;

        private void FoldersAdaptiveGridView_ContainerContentChanging1(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is StorageItemViewModel itemVM)
            {
                itemVM.Initialize(_ct);
                ToolTipService.SetToolTip(args.ItemContainer, new ToolTip() { Content = new TextBlock() { Text = itemVM .Name, TextWrapping = TextWrapping.Wrap } });
            }
        }

        CancellationTokenSource _navigationCts;
        CancellationToken _ct;
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            _navigationCts.Cancel();
            _navigationCts.Dispose();

            if (_vm.DisplayCurrentPath != null) 
            {
                try
                {
                    var sv = FoldersAdaptiveGridView.FindFirstChild<ScrollViewer>();
                    var ratio = sv.VerticalOffset / sv.ScrollableHeight;
                    _PathToLastScrollPosition[_vm.DisplayCurrentPath] = ratio;

                    Debug.WriteLine(ratio);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            }

            base.OnNavigatingFrom(e);
        }
        #region 初期フォーカス設定


        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            _navigationCts = new CancellationTokenSource();
            var ct = _ct = _navigationCts.Token;

            try
            {
                ConnectedAnimationService.GetForCurrentView()
                            .GetAnimation(PageTransitionHelper.ImageJumpConnectedAnimationName)?.Cancel();

                base.OnNavigatedTo(e);

                if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.New)
                {
                    if (_focusHelper.IsRequireSetFocus())
                    {
                        await FoldersAdaptiveGridView.WaitFillingValue(x => x.Items.Any(), ct);
                        var firstItem = FoldersAdaptiveGridView.Items.First();
                        if (firstItem is not null)
                        {
                            await FoldersAdaptiveGridView.WaitFillingValue(x => x.ContainerFromItem(firstItem) != null, ct);
                            var itemContainer = FoldersAdaptiveGridView.ContainerFromItem(firstItem) as Control;

                            await Task.Delay(50);
                            itemContainer.Focus(FocusState.Keyboard);
                        }
                        else
                        {
                            ReturnSourceFolderPageButton.Focus(FocusState.Keyboard);
                        }
                    }
                }
                else
                {
                    await BringIntoViewLastIntractItem(ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        #endregion

        // 前回スクロール位置への復帰に対応する
        // valueはスクロール位置のスクロール可能範囲に対する割合で示される 0.0 ~ 1.0 の範囲の値
        Dictionary<string, double> _PathToLastScrollPosition = new();

        public void DeselectItem()
        {
            FoldersAdaptiveGridView.DeselectAll();
        }

        public async Task BringIntoViewLastIntractItem(CancellationToken ct)
        {
            var lastIntaractItem = _vm.GetLastIntractItem();
            if (lastIntaractItem == null)
            {
                ReturnSourceFolderPageButton.Focus(FocusState.Keyboard);
                return;
            }

            FoldersAdaptiveGridView.ScrollIntoView(lastIntaractItem, ScrollIntoViewAlignment.Leading);

            await FoldersAdaptiveGridView.WaitFillingValue(x => x.ContainerFromItem(lastIntaractItem) != null, ct);

            DependencyObject item;
            item = FoldersAdaptiveGridView.ContainerFromItem(lastIntaractItem);

            if (item is Control control)
            {
                var sv = FoldersAdaptiveGridView.FindFirstChild<ScrollViewer>();
                if (_PathToLastScrollPosition.TryGetValue(_vm.DisplayCurrentPath, out double ratio) && double.IsNaN(ratio) is false)
                {
                    sv.ChangeView(null, sv.ScrollableHeight * ratio, null, true);
                }

                await Task.Delay(50, ct);
                control.Focus(FocusState.Keyboard);
            }
        }


        void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FoldersAdaptiveGridView.SelectionMode == ListViewSelectionMode.None)
            {                
                return;
            }

            if (e.AddedItems?.Any() ?? false)
            {
                foreach (var itemVM in e.AddedItems.Cast<StorageItemViewModel>())
                {
                    _vm.Selection.SelectedItems.Add(itemVM);
                }
            }
            
            if (e.RemovedItems?.Any() ?? false)
            {                
                var prevCount = FoldersAdaptiveGridView.SelectedItems.Count;
                foreach (var itemVM in e.RemovedItems.Cast<StorageItemViewModel>())
                {
                    _vm.Selection.SelectedItems.Remove(itemVM);
                }

                // 複数選択開始時に選択アイテムが無い場合にそのまま選択動作が終了しないようにしている
                if (prevCount != 0 && FoldersAdaptiveGridView.SelectedItems.Count == 0)
                {
                    _vm.Selection.EndSelection();
                }
            }
        }


        RelayCommand<object> _SelectionChangeCommand;
        public RelayCommand<object> SelectionChangeCommand => _SelectionChangeCommand ??= new RelayCommand<object>(item =>
        {
            if (item is StorageItemViewModel itemVM)
            {
                if (_vm.Selection.IsSelectionModeEnabled is false)
                {
                    _vm.Selection.StartSelection();
                }

                if (FoldersAdaptiveGridView.SelectedItems.Any(x => x == itemVM))
                {
                    FoldersAdaptiveGridView.SelectedItems.Remove(itemVM);
                }
                else
                {
                    FoldersAdaptiveGridView.SelectedItems.Add(itemVM);
                }
            }
        });


        private void AlbamItemManagementFlyout_Opening(object sender, object e)
        {
            var menuFlyout = sender as MenuFlyout;
            menuFlyout.Items.Clear();
            var albamRepository = Ioc.Default.GetRequiredService<AlbamRepository>();
            var expandImageSources = _vm.Selection.SelectedItems.Select(x => x.Item.FlattenAlbamItemInnerImageSource());
            foreach (var albam in albamRepository.GetAlbams())
            {
                if (expandImageSources.Any(x => albamRepository.IsExistAlbamItem(albam._id, x.Path)) is false)
                {
                    menuFlyout.Items.Add(new ToggleMenuFlyoutItem() { Text = albam.Name, Command = new AlbamItemAddCommand(albamRepository, albam), CommandParameter = expandImageSources, IsChecked = false });
                }
                else if (expandImageSources.All(x => albamRepository.IsExistAlbamItem(albam._id, x.Path)))
                {
                    menuFlyout.Items.Add(new ToggleMenuFlyoutItem() { Text = albam.Name, Command = new AlbamItemAddCommand(albamRepository, albam), CommandParameter = expandImageSources, IsChecked = true });
                }
                else
                {
                    menuFlyout.Items.Add(new ToggleMenuFlyoutItem() { Text = albam.Name, Command = new AlbamItemAddCommand(albamRepository, albam), CommandParameter = expandImageSources.Where(x => !albamRepository.IsExistAlbamItem(albam._id, x.Path)), IsChecked = true });
                }
            }
        }


        RelayCommand<StorageItemViewModel> _OpenItemCommand;
        public RelayCommand<StorageItemViewModel> OpenItemCommand => _OpenItemCommand ??= new RelayCommand<StorageItemViewModel>(itemVM => 
        {                                    
            if (_vm.Selection.IsSelectionModeEnabled is false
                && ((uint)Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control) & 0x01) != 0)
            {
                _vm.Selection.StartSelection();
                return;
            }

            if (FoldersAdaptiveGridView.SelectionMode != ListViewSelectionMode.None)
            {
                return;
            }

            var container = FoldersAdaptiveGridView.ContainerFromItem(itemVM);
            if (container is GridViewItem gvi)
            {
                var image = gvi.ContentTemplateRoot.FindDescendant<Image>();
                if (image.Source != null)
                {
                    //ConnectedAnimationService.GetForCurrentView()
                    //    .PrepareToAnimate(PageTransisionHelper.ImageJumpConnectedAnimationName, image);
                }
            }

            (_vm.OpenFolderItemCommand as ICommand).Execute(itemVM);
        });
    }
}
