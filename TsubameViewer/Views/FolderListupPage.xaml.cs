using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using Microsoft.Toolkit.Uwp;
using Microsoft.Toolkit.Uwp.UI;
using R3;
using R3.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Helpers;
using TsubameViewer.Services;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Converters;
using TsubameViewer.Views.Helpers;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using ZLinq;
using static Microsoft.Toolkit.Uwp.UI.Animations.Expressions.ExpressionValues;

#nullable enable
namespace TsubameViewer.Views;

[ObservableObject]
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

    void FolderListupPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Note: GetCancellationTokenOnUnloadedは使わない
        // 　Unlaodedは次ページのナビゲーション後に呼ばれてしまい
        // 　前ページのアイテム読み込みが残るケースが出ていた
        var ct = this.GetCancellationTokenOnNavigatingFrom();
        _navigationCt = ct;

        ContentViewTypeSelector.SelectedIndex = 0;

        _messenger.Register<RequestConnectedAnimationMessage>(this, (r, m) =>
        {
            var itemVM = _vm.FolderItems.FirstOrDefault(x => x.Path?.Equals(m.TargetItemPath, StringComparison.Ordinal) ?? false);
            if (itemVM != null)
            {
                var image = FoldersAdaptiveGridView.ContainerFromItem(itemVM);
                if (image.FindDescendant("ImageControl") is UIElement target)
                {
                    m.Reply(Task.FromResult<UIElement?>((UIElement?)target));
                }
                else
                {
                    m.Reply(Task.FromResult<UIElement?>(null));
                }
            }
        });

        _messenger.Register<LatestContentViewUpdateMessage>(this, (r, m) => 
        {
            var itemVM = _vm.FolderItems.FirstOrDefault(x => x.Path?.Equals(m.Value, StringComparison.Ordinal) ?? false);
            itemVM?.UpdateLastReadPosition();
        });

        DisposableBuilder db = new();
        HandleCreateFolderDialogTextChanging(ref db);        
        db.Build().RegisterTo(ct);
        InitializeMoveToFolders(ct).FireAndForgetSafe();
    }

    void FolderListupPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _messenger.Unregister<RequestConnectedAnimationMessage>(this);
        _messenger.Unregister<LatestContentViewUpdateMessage>(this);
    }

    void ContentViewTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selector = (Selector)sender;
        if (selector.IsLoaded && selector.SelectedIndex == 1 && _vm?.CurrentFolderItem != null)
        {
            _messenger.Send(new NavigationRequestMessage(nameof(ImageListupPage), PageTransitionHelper.CreatePageParameter(_vm?.CurrentFolderItem.Item)) { TransitionInfo = new SuppressNavigationTransitionInfo() });
            _vm.SetDefaultListupMode();
        }
    }

    readonly FolderListupPageViewModel _vm;
    readonly IMessenger _messenger;
    readonly FocusHelper _focusHelper;

    void FoldersAdaptiveGridView_ContainerContentChanging1(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        d(args).FireAndForgetSafe("FoldersAdaptiveGridView_ContainerContentChanging1");
        async Task d(ContainerContentChangingEventArgs args)
        {
            if (args.Item is IStorageItemViewModel itemVM
                && !itemVM.IsInitialized)
            {
                // Note: x:Bindの変更適用とToolTipService.SetToolTipが同時に実行されると正常に表示されない
                await itemVM.InitializeAsync(_navigationCt);
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
    }

    CancellationTokenSource? _navigationCts;
    CancellationToken _navigationCt;
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
                _pathToLastScrollPosition[_vm.DisplayCurrentPath] = ratio;

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
        var ct = _navigationCt = _navigationCts.Token;
        
        d().FireAndForgetSafe();
        async Task d()
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
    }

    #endregion

    // 前回スクロール位置への復帰に対応する
    // valueはスクロール位置のスクロール可能範囲に対する割合で示される 0.0 ~ 1.0 の範囲の値
    readonly Dictionary<string, double> _pathToLastScrollPosition = new();


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

        if (item is Control control
            && _vm.DisplayCurrentPath != null)
        {
            var sv = FoldersAdaptiveGridView.FindFirstChild<ScrollViewer>();
            if (_pathToLastScrollPosition.TryGetValue(_vm.DisplayCurrentPath, out double ratio) && double.IsNaN(ratio) is false)
            {
                sv.ChangeView(null, sv.ScrollableHeight * ratio, null, true);
            }

            await Task.Delay(50, ct);
            control.Focus(FocusState.Keyboard);
        }
    }

    #region Selection

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
            // こうしないとFavoriteToggleCommandのCanExecuteが実行されない
            _vm.Selection.ForceNotifySelectedItems();
        }
        
        if (e.RemovedItems?.Any() ?? false)
        {                
            var prevCount = FoldersAdaptiveGridView.SelectedItems.Count;
            foreach (var itemVM in e.RemovedItems.Cast<IStorageItemViewModel>())
            {
                _vm.Selection.SelectedItems.Remove(itemVM);
            }

            // こうしないとFavoriteToggleCommandのCanExecuteが実行されない
            _vm.Selection.ForceNotifySelectedItems();
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
        
    private void FoldersAdaptiveGridView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        e.Data.Properties.Add("MyCustomDroppedItems", e.Items.ToList());
        FolderSelectionSplitView.IsPaneOpen = true;
    }

    async Task InitializeMoveToFolders(CancellationToken ct)
    {
        _vm.ObservePropertyChanged(x => x.CurrentFolderItem)
            .Debounce(TimeSpan.FromSeconds(1))
            .SubscribeAwait(async (folderVM, ct) => 
            {
                Folders = [];
                ToggleDisplaySiblingFoldersButton.IsEnabled = false;
                if (_vm.CurrentFolderItem?.Item.StorageItem is StorageFolder parentFolder)
                {
                    var folderQuery = parentFolder.CreateFolderQuery();
                    Folders.Add(parentFolder);
                    Folders.Add(null!);
                    await foreach (var folder in folderQuery.ToAsyncEnumerable().WithCancellation(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        if (!FolderSelectionSplitView.IsPaneOpen)
                        {
                            await Task.Delay(250, ct);
                        }
                        Folders.Add(folder);

                        if (Folders.Count >= 1)
                        {
                            ToggleDisplaySiblingFoldersButton.IsEnabled = true;
                        }
                    }
                }
            })
            .RegisterTo(ct);
    }

    [ObservableProperty]
    ObservableCollection<StorageFolder>? _folders;

    bool CanMoveToFolderSelectedItems(StorageFolder? folder)
    {
        return folder != null;
    }

    [RelayCommand(CanExecute = nameof(CanMoveToFolderSelectedItems))]    
    async Task MoveToFolderSelectedItemsAsync(StorageFolder? hostFolder)
    {
        if (hostFolder == null) { return; }

        using var pooledItems = _vm.Selection.SelectedItems.AsValueEnumerable().ToArrayPool();
        var items = pooledItems.ArraySegment;
        if (items.Count == 0) { return; }

        await MoveItemsToAsync(hostFolder, items.Select(x => x.Item.StorageItem), default);
        _messenger.SendShowTextNotificationMessage(items.Count == 1
            ? "MoveToFolder_Completed_Single".Translate((items[0]).Name, hostFolder.Name)
            : "MoveToFolder_Completed_Multi".Translate(items.Count, hostFolder.Name));

        foreach (var item in items.Cast<IStorageItemViewModel>())
        {
            _messenger.Send(new StorageItemNotFoundMessage(item.Path));
        }

        _vm.Selection.SelectedItems.Clear();
    }

    [RelayCommand]
    void ToggleSiblingFolderPaneDisplay()
    {
        FolderSelectionSplitView.IsPaneOpen = !FolderSelectionSplitView.IsPaneOpen;
    }

    private void FoldersAdaptiveGridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {

    }


    private void ListView_DragEnter(object sender, DragEventArgs e)
    {
        var hostUI = (FrameworkElement)sender;
        if (e.DataView.Properties.TryGetValue("MyCustomDroppedItems", out object itemsRaw) is false) { return; }
        var items = (itemsRaw as List<object>);
        if (items is null or { Count: 0 }) { return; }
        var deferral = R3.Disposable.Create(e.GetDeferral(), deferral => deferral.Complete());
        AsyncTaskErrorHandler.Handle((this, hostUI, e, deferral, items), static async (s) =>
        {
            var (_this, hostUI, e, deferral, items) = s;
            using (deferral)
            {
                foreach (var item in items)
                {
                    if (item is not IStorageItemViewModel myItem)
                    {
                        System.Diagnostics.Debug.WriteLine($"処理できないドラッグされたアイテム: {item?.GetType().Name}");
                        return;
                    }
                }

                if (hostUI.DataContext is StorageFolder folderItem)
                {
                    e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
                    e.DragUIOverride.Caption = "MoveToFolder_WithFolderName".Translate(folderItem.Name);
                }
            }
        });
    }

    private void ListView_Drop(object sender, DragEventArgs e)
    {
        var hostUI = (FrameworkElement)sender;
        if (e.DataView.Properties.TryGetValue("MyCustomDroppedItems", out object itemsRaw) is false) { return; }
        var items = (itemsRaw as List<object>).ToList();
        AsyncTaskErrorHandler.Handle((this, hostUI, e, items), static async (s) =>
        {
            var (_this, hostUI, e, items) = s;
            if (items is null or { Count: 0 }) { return; }

            if (hostUI.DataContext is StorageFolder hostFolder)
            {
                var messenger = Ioc.Default.GetRequiredService<IMessenger>();
                await _this.MoveItemsToAsync(hostFolder, items.Cast<IStorageItemViewModel>().Select(x => x.Item.StorageItem), default);
                messenger.SendShowTextNotificationMessage(items.Count == 1
                    ? "MoveToFolder_Completed_Single".Translate(((IStorageItemViewModel)items[0]).Name, hostFolder.Name)
                    : "MoveToFolder_Completed_Multi".Translate(items.Count, hostFolder.Name));

                foreach (var item in items.Cast<IStorageItemViewModel>())
                {
                    messenger.Send(new StorageItemNotFoundMessage(item.Path));
                }
            }

        });
    }


    private async Task MoveItemsToAsync(Windows.Storage.StorageFolder targetFolder, IEnumerable<Windows.Storage.IStorageItem> items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            Debug.WriteLine($"Move to {targetFolder.Path}: {item.Name}");
            List<Windows.Storage.IStorageItem> failedItems = [];
            if (item is Windows.Storage.StorageFile file)
            {
                try
                {
                    await file.MoveAsync(targetFolder, file.Name, Windows.Storage.NameCollisionOption.FailIfExists).AsTask(ct);
                }
                catch
                {
                    failedItems.Add(item);
                }
            }
            else if (item is Windows.Storage.StorageFolder folder)
            {
                await folder.MoveAsync(targetFolder, Windows.Storage.CreationCollisionOption.OpenIfExists, Windows.Storage.NameCollisionOption.FailIfExists);
            }
        }
    }

    #endregion



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

        (_vm.OpenFolderItemCommand as ICommand).Execute(itemVM);
    }

    #region Create Folder

    [RelayCommand]
    async Task CreateFolder()
    {
        var folder = (StorageFolder)_vm.CurrentFolderItem!.Item.StorageItem;
        CreateFolderDialogTextBox.Text = "";
        CreateFolderDialog.IsPrimaryButtonEnabled = false;
        bool isExitWithEnterKey = false;
        async void CreateFolderDialogTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.OriginalKey == VirtualKey.Enter)
            {
                AsyncTaskErrorHandler.Handle(async () =>
                {
                    var name = CreateFolderDialogTextBox.Text;
                    var isExistFolder = false;
                    try
                    {
                        isExistFolder = await folder.GetFolderAsync(name) != null;
                    }
                    catch (FileNotFoundException)
                    {
                        isExistFolder = false;
                    }


                    if (!isExistFolder)
                    {
                        isExitWithEnterKey = true;
                        CreateFolderDialog.Hide();
                    }
                });
            }
        }

        try
        {
            CreateFolderDialogTextBox.KeyDown += CreateFolderDialogTextBox_KeyDown;
            while (true)
            {
                if (await CreateFolderDialog.ShowAsync() != ContentDialogResult.Primary
                    && !isExitWithEnterKey) { return; }

                isExitWithEnterKey = false;
                var name = CreateFolderDialogTextBox.Text;
                var isExistFolder = false;
                try
                {
                    isExistFolder = await folder.GetFolderAsync(name) != null;
                }
                catch (FileNotFoundException)
                {
                    isExistFolder = false;
                }

                CreateFolder_ExistFolderNameTextBlock.Visibility = isExistFolder.TrueToVisible();
                if (isExistFolder) { continue; }

                try
                {
                    var newfodler = await folder.CreateFolderAsync(CreateFolderDialogTextBox.Text, CreationCollisionOption.FailIfExists);
                    var itemVM = _vm.ToStorageItemVM(newfodler);
                    itemVM.InitializeAsync(_navigationCt).FireAndForgetSafe();
                    _vm.FileItemsView.Insert(0, itemVM);
                    return;
                }
                catch (FileNotFoundException) { }
            }
        }
        finally
        {
            CreateFolderDialogTextBox.KeyDown -= CreateFolderDialogTextBox_KeyDown;
        }
        
    }

    void HandleCreateFolderDialogTextChanging(ref DisposableBuilder db)
    {
        CreateFolderDialogTextBox.ObserveTextChanged()
            .Debounce(TimeSpan.FromSeconds(0.1))
            .SubscribeAwait(this, async (e, s, ct) => 
            {
                var name = s.CreateFolderDialogTextBox.Text;
                if (string.IsNullOrWhiteSpace(name)) 
                {
                    s.CreateFolderDialog.IsPrimaryButtonEnabled = false;
                    return; 
                }
                s.CreateFolderDialog.IsPrimaryButtonEnabled = true;
                var folder = (StorageFolder)s._vm.CurrentFolderItem!.Item.StorageItem;
                bool isExistFolder;
                try
                {
                    isExistFolder = await folder.GetFolderAsync(name) != null;
                }
                catch (FileNotFoundException)
                {
                    isExistFolder = false;
                }

                s.CreateFolderDialog.IsPrimaryButtonEnabled = !isExistFolder;
                CreateFolder_ExistFolderNameTextBlock.Visibility = isExistFolder.TrueToVisible();
            })
            .AddTo(ref db);

        CreateFolderDialog.IsPrimaryButtonEnabled = false;
    }

    #endregion

    #region Search Box

    InPageSearchContext? _searchContext;
    void PrimaryWindowCoreLayout_Loaded(object sender, RoutedEventArgs e)
    {
        if (((AutoSuggestBox)sender).FindDescendant<TextBox>() is { } textBox)
        {
            textBox.TextCompositionStarted += TextBox_TextCompositionStarted;
            textBox.TextCompositionEnded += TextBox_TextCompositionEnded;
            textBox.TextChanged += TextBox_TextChanged;
            _searchContext = Ioc.Default.GetService<InPageSearchContext>();

            _isItemsForceInfoLoaded = false;
        }
    }


    void AutoSuggestBox_Unloaded(object sender, RoutedEventArgs e)
    {
        if (((AutoSuggestBox)sender).FindDescendant<TextBox>() is { } textBox)
        {
            textBox.TextCompositionStarted -= TextBox_TextCompositionStarted;
            textBox.TextCompositionEnded -= TextBox_TextCompositionEnded;
            textBox.TextChanged -= TextBox_TextChanged;
            _searchContext?.Dispose();
            _searchContext = null;
        }
    }



    bool _isInputIncomplete;

    void TextBox_TextCompositionStarted(TextBox sender, TextCompositionStartedEventArgs args)
    {
        _isInputIncomplete = true;
    }

    void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInputIncomplete == false)
        {
            var textBox = (TextBox)sender;
            //(DataContext as AppShellViewModel).UpdateAutoSuggestCommand.Execute(textBox.Text);
        }
    }

    void TextBox_TextCompositionEnded(TextBox sender, TextCompositionEndedEventArgs args)
    {
        _isInputIncomplete = false;
        var textBox = (TextBox)sender;
        //(DataContext as AppShellViewModel).UpdateAutoSuggestCommand.Execute(textBox.Text);
    }



    void AutoSuggestBox_AccessKeyInvoked(UIElement sender, AccessKeyInvokedEventArgs args)
    {
        (sender as Control)!.Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        (args.Element as Control)!.Focus(FocusState.Keyboard);
        args.Handled = true;
    }
    bool _isItemsForceInfoLoaded;
    void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _messenger.Send(new InPageSearchRequestMessage(sender.Text));
        if (!sender.Items.Any())
        {
            sender.ItemsSource = new object[1] { new { Name = "Search_FromAll".Translate() } };
        }
        sender.IsSuggestionListOpen = !string.IsNullOrWhiteSpace(sender.Text);

        if (_isItemsForceInfoLoaded is false)
        {
            Debug.WriteLine("強制読み込み EnsureStorageItemAsync");
            _isItemsForceInfoLoaded = true;
            foreach (var itemVM in _vm.FolderItems)
            {
                (itemVM as LazyFolderOrArchiveFileViewModel)?.EnsureStorageItemAsync(_navigationCt).FireAndForgetSafe();
            }
        }
    }

    void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        _searchContext?.SearchQuerySubmitCommand.Execute(sender.Text);
    }

    void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _messenger.Send(new InPageSearchRequestMessage(sender.Text));
        _messenger.Send(new SearchQuerySubmitedRequestMessage(sender.Text));
    }


    #endregion

    private void ChildImageFolderOpenModeButton_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) { return; }
        _vm.SelectedChildImagesFolderOpenMode = (DefaultFolderOrArchiveOpenMode)e.AddedItems[0];
    }

    private void ChildFolderSettingsFlyout_Opened(object sender, object e)
    {
        
    }
}
