using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using Microsoft.Toolkit.Uwp;
using Microsoft.Toolkit.Uwp.UI;
using R3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Converters;
using TsubameViewer.Views.Helpers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using static Microsoft.Toolkit.Uwp.UI.Animations.Expressions.ExpressionValues;

#nullable enable
namespace TsubameViewer.Views;

public sealed partial class FolderListupPage : Page, ITitlebarContentAware
{
    public DataTemplate? GetContent()
    {
        return TitlebarContent;
    }

    public R3.Observable<string> ObserveTitleChanged()
    {
        return _vm.ObservePropertyChanged(x => x.DisplayCurrentPath)
            .Select(x => UriHelper.ToHumanReadable(x));
    }

    public FolderListupPage()
    {
        this.InitializeComponent();

        DataContext = _vm = Ioc.Default.GetRequiredService<FolderListupPageViewModel>();
        _messenger = Ioc.Default.GetRequiredService<IMessenger>();
        _focusHelper = Ioc.Default.GetRequiredService<FocusHelper>();
        this.FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging1;

        Loaded += FolderListupPage_Loaded;
        Unloaded += FolderListupPage_Unloaded;
    }

    private void FolderListupPage_Loaded(object sender, RoutedEventArgs e)
    {
        ContentViewTypeSelector.SelectedIndex = 0;

        _messenger.Register<RequestConnectedAnimationMessage>(this, (r, m) =>
        {
            var itemVM = _vm.FolderItems.FirstOrDefault(x => x.Path.Equals(m.TargetItemPath, StringComparison.Ordinal));
            if (itemVM != null)
            {
                var image = FoldersAdaptiveGridView.ContainerFromItem(itemVM);
                if (image is UIElement target)
                {
                    m.Reply(DispatcherQueue.GetForCurrentThread().EnqueueAsync(async () =>
                    {
                        return (UIElement?)target;
                    }));
                }
                else
                {
                    m.Reply(Task.FromResult<UIElement?>(null));
                }
            }
        });
    }

    private void FolderListupPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _messenger.Unregister<RequestConnectedAnimationMessage>(this);
    }

    private void ContentViewTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selector = (Selector)sender;
        if (selector.IsLoaded && selector.SelectedIndex == 1 && _vm?.CurrentFolderItem != null)
        {
            _messenger.NavigateAsync(nameof(ImageListupPage), PageTransitionHelper.CreatePageParameter(_vm?.CurrentFolderItem.Item));
        }
    }



    private readonly FolderListupPageViewModel _vm;
    private readonly IMessenger _messenger;
    private readonly FocusHelper _focusHelper;

    private async void FoldersAdaptiveGridView_ContainerContentChanging1(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is IStorageItemViewModel itemVM)
        {
            // Note: x:Bindの変更適用とToolTipService.SetToolTipが同時に実行されると正常に表示されない
            await itemVM.InitializeAsync(_ct);
            if (itemVM.Item != null)
            {
                var size = args.ItemContainer.ActualSize.Y != 0 ? args.ItemContainer.ActualSize : args.ItemContainer.DesiredSize.ToVector2();
                if (size.Y == 0)
                {
                    size = new Vector2(120, 200);
                }
                ToolTipService.SetToolTip(args.ItemContainer, 
                    new ToolTip()
                    { 
                        Content = new TextBlock() 
                        { 
                            Text = itemVM.Name, 
                            TextWrapping = TextWrapping.Wrap 
                        },
                        PlacementRect = new Windows.Foundation.Rect(new(), (size - new Vector2(0, 16)).ToSize()),
                        Placement = PlacementMode.Bottom
                    });
            }
        }
    }

    CancellationTokenSource? _navigationCts;
    CancellationToken _ct;
    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (_navigationCts != null)
        {
            _navigationCts.Cancel();
            _navigationCts.Dispose();
        }

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
                        Control? itemContainer = FoldersAdaptiveGridView.ContainerFromItem(firstItem) as Control;
                        if (itemContainer != null) 
                        {
                            await Task.Delay(50);
                            itemContainer.Focus(FocusState.Keyboard);
                        }
                    }
                    else
                    {
                        //ReturnSourceFolderPageButton.Focus(FocusState.Keyboard);
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
            //ReturnSourceFolderPageButton.Focus(FocusState.Keyboard);
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
            foreach (var itemVM in e.AddedItems.Cast<IStorageItemViewModel>())
            {
                _vm.Selection.SelectedItems.Add(itemVM);
            }
        }
        
        if (e.RemovedItems?.Any() ?? false)
        {                
            var prevCount = FoldersAdaptiveGridView.SelectedItems.Count;
            foreach (var itemVM in e.RemovedItems.Cast<IStorageItemViewModel>())
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


    [RelayCommand]
    void SelectionChange(object item)
    {
        if (item is IStorageItemViewModel itemVM)
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
    }


    private void AlbamItemManagementFlyout_Opening(object sender, object e)
    {
        var menuFlyout = (MenuFlyout)sender;
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


    [RelayCommand]
    void OpenItem(IStorageItemViewModel itemVM)
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
            if (image?.Source != null)
            {
                //ConnectedAnimationService.GetForCurrentView()
                //    .PrepareToAnimate(PageTransisionHelper.ImageJumpConnectedAnimationName, image);
            }
        }

        (_vm.OpenFolderItemCommand as ICommand).Execute(itemVM);
    }

    #region Search Box

    InPageSearchContext? _searchContext;
    private void PrimaryWindowCoreLayout_Loaded(object sender, RoutedEventArgs e)
    {
        var textBox = ((AutoSuggestBox)sender).FindDescendant<TextBox>();
        textBox.TextCompositionStarted += TextBox_TextCompositionStarted;
        textBox.TextCompositionEnded += TextBox_TextCompositionEnded;
        textBox.TextChanged += TextBox_TextChanged;
        _searchContext = Ioc.Default.GetService<InPageSearchContext>();
    }


    private void AutoSuggestBox_Unloaded(object sender, RoutedEventArgs e)
    {
        var textBox = ((AutoSuggestBox)sender).FindDescendant<TextBox>();
        textBox.TextCompositionStarted -= TextBox_TextCompositionStarted;
        textBox.TextCompositionEnded -= TextBox_TextCompositionEnded;
        textBox.TextChanged -= TextBox_TextChanged;
        _searchContext?.Dispose();
        _searchContext = null;
    }



    bool _isInputIncomplete;

    private void TextBox_TextCompositionStarted(TextBox sender, TextCompositionStartedEventArgs args)
    {
        _isInputIncomplete = true;
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInputIncomplete == false)
        {
            var textBox = (TextBox)sender;
            //(DataContext as AppShellViewModel).UpdateAutoSuggestCommand.Execute(textBox.Text);
        }
    }

    private void TextBox_TextCompositionEnded(TextBox sender, TextCompositionEndedEventArgs args)
    {
        _isInputIncomplete = false;
        var textBox = (TextBox)sender;
        //(DataContext as AppShellViewModel).UpdateAutoSuggestCommand.Execute(textBox.Text);
    }



    private void AutoSuggestBox_AccessKeyInvoked(UIElement sender, AccessKeyInvokedEventArgs args)
    {
        //(sender as Control).Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    private void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        //(args.Element as Control).Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    InPageSearchRequestMessage? _searchMessage;
    private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _messenger.Send(new InPageSearchRequestMessage(sender.Text));
        if (!sender.Items.Any())
        {
            sender.ItemsSource = new object[1] { new { Name = "Search_FromAll".Translate() } };
        }
        sender.IsSuggestionListOpen = !string.IsNullOrWhiteSpace(sender.Text);
    }

    private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        _searchContext?.SearchQuerySubmitCommand.Execute(sender.Text);
    }

    private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _messenger.Send(new InPageSearchRequestMessage(sender.Text));
        _messenger.Send(new SearchQuerySubmitedRequestMessage(sender.Text));
    }


    #endregion
}
