using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using I18NPortable;
using Microsoft.UI.Xaml.Controls;
using R3;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.Maintenance;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Helpers;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Converters;
using TsubameViewer.Views.Helpers;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using ZLinq;
using static System.Net.Mime.MediaTypeNames;
using Image = Windows.UI.Xaml.Controls.Image;

#nullable enable
namespace TsubameViewer.Views;


public static class ObservableCollectionExtensions
{
    public static void InsertSorted<T>(this IList<T> collection, T item, Comparison<T> comparison)
    {
        int index = 0;
        while (index < collection.Count && comparison(collection[index], item) is int c && c < 0)
        {
            if (c == 0) { return; }
            index++;
        }
        collection.Insert(index, item);
    }

    public static void InsertSortedDescending<T>(this IList<T> collection, T item, Comparison<T> comparison)
    {
        int index = 0;
        while (index < collection.Count && comparison(collection[index], item) is int c && c > 0)
        {
            if (c == 0) { return; }
            index++;
        }
        collection.Insert(index, item);
    }
}

[ObservableObject]
public sealed partial class ImageListupPage : Page, ITitlebarContentAware
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

    readonly ImageListupPageViewModel _vm;
    readonly IMessenger _messenger;
    readonly FocusHelper _focusHelper;

    public ImageListupPage()
    {
        InitializeComponent();

        DataContext = _vm = Ioc.Default.GetRequiredService<ImageListupPageViewModel>();
        _messenger = Ioc.Default.GetRequiredService<IMessenger>();
        _focusHelper = Ioc.Default.GetRequiredService<FocusHelper>();

        Loaded += FolderListupPage_Loaded;
        Unloaded += FolderListupPage_Unloaded;

        FileItemsRepeater_Small.ElementPrepared += FileItemsRepeater_ElementPrepared;
        FileItemsRepeater_Midium.ElementPrepared += FileItemsRepeater_ElementPrepared;
        FileItemsRepeater_Large.ElementPrepared += FileItemsRepeater_ElementPrepared;

        FileItemsRepeater_Small.ElementClearing += FileItemsRepeater_Large_ElementClearing;
        FileItemsRepeater_Midium.ElementClearing += FileItemsRepeater_Large_ElementClearing;
        FileItemsRepeater_Large.ElementClearing += FileItemsRepeater_Large_ElementClearing;
    }

    void FolderListupPage_Loaded(object sender, RoutedEventArgs e)
    {
        ContentViewTypeSelector.SelectedIndex = 1;

        _messenger.Register<RequestConnectedAnimationMessage>(this, (r, m) => 
        {            
            if (_realizedItems.FirstOrDefault(x => x.Path?.Equals(m.TargetItemPath, StringComparison.Ordinal) ?? false) is { } itemVM
            && GetCurrentDisplayItemsRepeater() is { } itemsRepeater)
            {
                var index = _vm.FileItemsView.IndexOf(itemVM);
                var image = itemsRepeater.GetOrCreateElement(index);
                m.Reply(DispatcherQueue.GetForCurrentThread().EnqueueAsync(async () =>
                {
                    return (UIElement?)image;
                }));
            }
            else
            {
                m.Reply(Task.FromResult<UIElement?>(null));
            }
        });
    }

    void FolderListupPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _messenger.Unregister<RequestConnectedAnimationMessage>(this);
    }

    void ContentViewTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selector = (Selector)sender;
        if (selector.IsLoaded && selector.SelectedIndex == 0 && _vm?.CurrentFolderItem != null)
        {
            _messenger.Send(new NavigationRequestMessage(nameof(FolderListupPage), PageTransitionHelper.CreatePageParameter(_vm?.CurrentFolderItem.Item)) { TransitionInfo = new SuppressNavigationTransitionInfo() });
            _vm.SetDefaultListupMode();
        }
    }



    #region 初期フォーカス設定

    CancellationTokenSource? _navigationCts;
    CancellationToken _navigationCt;    
    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        _messenger.Unregister<StartMultiSelectionMessage>(this);

        ClearSelection();

        Debug.WriteLine($"Images RealizedItems: {_realizedItems.Count}");

        base.OnNavigatingFrom(e);
    }


    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        d().FireAndForgetSafe("ImageListupPage.OnNavigatedTo");
        async Task d()
        {
            var ct = _navigationCt = this.GetCancellationTokenOnNavigatingFrom();

            Debug.WriteLine($"NowProcessing: {_vm.NowProcessing}");
            await _vm.ObservePropertyChanged(x => x.NowProcessing)
                .Where(x => x)
                .Take(1)
                .WaitAsync(ct);
            Debug.WriteLine($"NowProcessing: {_vm.NowProcessing}");
            await _vm.ObservePropertyChanged(x => x.NowProcessing)
                .Where(x => !x)
                .Take(1)
                .WaitAsync(ct);
            Debug.WriteLine($"NowProcessing: {_vm.NowProcessing}");

            _messenger.Register<StartMultiSelectionMessage>(this, (r, m) =>
            {
                if (_vm.Selection.IsSelectionModeEnabled)
                {
                    _vm.Selection.EndSelection();
                }
                else
                {
                    _vm.Selection.StartSelection();
                }

                _vm.FileDeleteCommand.NotifyCanExecuteChanged();
            });

            try
            {
                // ここでawaitをつけるとUIの応答性が下がるので避けたい
                if (e.NavigationMode == NavigationMode.New)
                {
                    _ = ResetScrollPosition(ct);
                }
                else
                {                    
                    _ = BringIntoViewLastIntractItem(ct);
                }
            }
            catch (OperationCanceledException) { }

            InitializeMoveToFolders(ct).FireAndForgetSafe("InitializeMoveToFolders");
            HandleCreateFolderDialogTextChanging(ct);

            // itemVM側の非同期ロックに期待して全部を一気に処理させる
            // ここでawaitをつけるとUIの応答性が下がるので避けたい
            //using var items = _realizedItems.AsValueEnumerable().ToArrayPool();
            //try
            //{
            //    foreach (var itemVM in items.ArraySegment)
            //    {
            //        _ = itemVM.EnsureImageSizeRatioAsync(ct);
            //        itemVM.RestoreThumbnailLoadingTask(ct);
            //    }
            //}
            //catch (OperationCanceledException) { }
        }
    }

    void SaveScrollStatus(UIElement target)
    {
        if (target is FrameworkElement fe
                    && fe.DataContext is IStorageItemViewModel itemVM)
        {
            _vm.SetLastIntractItem(itemVM);
        }
    }

    async Task<UIElement?> WaitTargetIndexItemLoadingAsync(int index, CancellationToken ct)
    {
        await this.WaitFillingValue(x => x.GetCurrentDisplayItemsRepeater() != null, ct);

        UIElement? lastIntractItem = null;
        var currentItemsRepeater = GetCurrentDisplayItemsRepeater();

        if (currentItemsRepeater == null) { return null; }

        foreach (int count in Enumerable.Range(0, 10))
        {
            lastIntractItem = currentItemsRepeater.TryGetElement(index);
            if (lastIntractItem is not null)
            {
                await Task.Delay(50, ct);

                break;
            }

            await Task.Delay(50, ct);
        }
        return lastIntractItem;
    }


    async ValueTask ResetScrollPosition(CancellationToken ct)
    {
        ItemsScrollViewer.ChangeView(null, 0.0, null, disableAnimation: true);
        if (_focusHelper.IsRequireSetFocus() && await WaitTargetIndexItemLoadingAsync(0, ct) is Control control)
        {
            control.Focus(FocusState.Keyboard);
        }
    }


    void FileItemsRepeater_Large_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        this.FileItemsRepeater_Small.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
        this.FileItemsRepeater_Midium.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
        this.FileItemsRepeater_Large.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;

        if (_focusHelper.IsRequireSetFocus())
        {
            (args.Element as Control)?.Focus(FocusState.Keyboard);
        }
    }

    private ItemsRepeater? GetCurrentDisplayItemsRepeater()
    {
        return _vm.FileDisplayMode switch
        {
            FileDisplayMode.Small => FileItemsRepeater_Small,
            FileDisplayMode.Midium => FileItemsRepeater_Midium,
            FileDisplayMode.Large => FileItemsRepeater_Large,
            _ => null,
        };
    }

    public async Task<UIElement?> BringIntoViewLastIntractItem(CancellationToken ct)
    {        
        await this.WaitFillingValue(x => x._vm != null && x._vm.NowProcessing is false, ct);

        if (_vm.DisplayCurrentPath == null)
        {
            return null;
        }

        if (_vm.GetLastIntractItem() is not null and var lastIntractItemVM)
        {
            var lastIntractItemIndex = _vm.FileItemsView.IndexOf(lastIntractItemVM);
            if (lastIntractItemIndex >= 0)
            {                                
                UIElement? lastIntractItem = await WaitTargetIndexItemLoadingAsync(lastIntractItemIndex, ct);
                if (lastIntractItem is Control control)
                {
                    if (lastIntractItem.ActualOffset.Y < ItemsScrollViewer.VerticalOffset
                        || ItemsScrollViewer.VerticalOffset + ItemsScrollViewer.ViewportHeight < lastIntractItem.ActualOffset.Y)
                    {
                        var targetOffset = lastIntractItem.ActualOffset.Y - (float)ItemsScrollViewer.ViewportHeight * 0.5f;
                        ItemsScrollViewer.ChangeView(null, targetOffset, null, disableAnimation: true);
                    }

                    if (_focusHelper.IsRequireSetFocus())
                    {
                        control.Focus(FocusState.Keyboard);
                    }
                }

                return lastIntractItem;
            }
        }

        return null;
    }

    #endregion

    public const string ItemsDisplayMode_Large = nameof(FileDisplayMode.Large);
    public const string ItemsDisplayMode_Midium = nameof(FileDisplayMode.Midium);
    public const string ItemsDisplayMode_Small = nameof(FileDisplayMode.Small);
    public const string ItemsDisplayMode_Line = nameof(FileDisplayMode.Line);

    string ToViewDisplayMode(FileDisplayMode mode)
    {
        return mode.ToString();
    }

    ObservableCollection<IStorageItemViewModel> _realizedItems = [];    
    readonly AsyncLock _imageGeneratingLock = new AsyncLock(Environment.ProcessorCount / 2);

    Image? GetImageControl(FrameworkElement fe)
    {
        var cc = fe.GetContentControl() as ContentControl;
        var insideItem = cc?.Content as UIElement;
        return insideItem?.FindDescendant("Image") as Image;
    }
    async void FileItemsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {        
        if (args.Element is FrameworkElement fe)
        {            
            if (fe.DataContext is IStorageItemViewModel itemVM)
            {
                var image = GetImageControl(fe);
                Guard.IsNotNull(image);
                BitmapImage targetBitmap;
                image.Opacity = 0;
                if (image.Source is BitmapImage bitmap)
                {
                    targetBitmap = bitmap;
                }
                else
                {
                    targetBitmap = new BitmapImage()
                    {
                        AutoPlay = false
                    };
                    image.Source = targetBitmap;
                }

                _realizedItems.Add(itemVM);
                await itemVM.EnsureImageSizeRatioAsync(_navigationCt);
                if (itemVM.ImageAspectRatioWH != null)
                {
                    await itemVM.InitializeAsync(targetBitmap, _navigationCt);
                    image.Opacity = 1;
                }
                else
                {
                    try
                    {
                        using (await _imageGeneratingLock.LockAsync(_navigationCt))
                        {
                            await itemVM.InitializeAsync(targetBitmap, _navigationCt);
                            image.Opacity = 1;
                        }
                    }
                    catch (OperationCanceledException) { }
                }
            }
        }
    }

    void FileItemsRepeater_Large_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        if (args.Element is FrameworkElement fe)
        {
            if (fe.DataContext is IStorageItemViewModel itemVM)
            {
                _realizedItems.Remove(itemVM);
                itemVM.StopImageLoading();
            }
        }
    }


    readonly AnimationBuilder _zoomUpAnimation = AnimationBuilder.Create()
            .Scale(new Vector2(1.020f, 1.020f), duration: TimeSpan.FromMilliseconds(50));

    readonly AnimationBuilder _zoomDownAnimation = AnimationBuilder.Create()
            .Scale(new Vector2(1, 1), duration: TimeSpan.FromMilliseconds(50));

    void Image_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        var item = (FrameworkElement)sender;
        var image = item.FindChild<Image>();
        if (image != null)
        {
            _zoomUpAnimation
                .CenterPoint(new Vector2((float)image.ActualWidth * 0.5f, (float)image.ActualHeight * 0.5f), duration: TimeSpan.FromMilliseconds(1))
                .Start(image);

            if (image.Source is BitmapImage bitmapImage
                && bitmapImage.IsAnimatedBitmap)
            {
                bitmapImage.Play();
            }
        }
    }


    void Image_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        var item = (FrameworkElement)sender;
        var image = item.FindChild<Image>();
        if (image == null) { return; }
        _zoomDownAnimation
            .CenterPoint(new Vector2((float)image.ActualWidth * 0.5f, (float)image.ActualHeight * 0.5f), duration: TimeSpan.FromMilliseconds(1))
            .Start(image);

        if (image.Source is BitmapImage bitmapImage
                && bitmapImage.IsAnimatedBitmap)
        {
            bitmapImage.Play();
        }
    }

    [RelayCommand]
    void PrepareConnectedAnimationWithTappedItem(TappedRoutedEventArgs item)
    {
        SaveScrollStatus((UIElement)item!.OriginalSource);

        var image = ((UIElement)item!.OriginalSource).FindDescendantOrSelf<Image>();
        if (image?.Source != null)
        {
            var anim = ConnectedAnimationService.GetForCurrentView()
                .PrepareToAnimate(PageTransitionHelper.ImageJumpConnectedAnimationName, image);
            anim.Configuration = new BasicConnectedAnimationConfiguration();
        }
    }

    [RelayCommand]
    void PrepareConnectedAnimationWithCurrentFocusElement(UIElement item)
    {
        if (item == null) { return; }

        SaveScrollStatus(item);

        var image = item.FindDescendantOrSelf<Windows.UI.Xaml.Controls.Image>();
        if (image?.Source != null)
        {
            var anim = ConnectedAnimationService.GetForCurrentView()
                .PrepareToAnimate(PageTransitionHelper.ImageJumpConnectedAnimationName, image);
            anim.Configuration = new BasicConnectedAnimationConfiguration();
        }
    }

    int _lastSelectedItemIndex = -1;
    void ImageListItem_Clicked(object sender, RoutedEventArgs e)
    {
        var fe = (FrameworkElement)sender;
        if (_vm.Selection.IsSelectionModeEnabled
            || ((uint)Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control) & 0x01) != 0
            )
        {
            if (fe.DataContext is IStorageItemViewModel itemVM)
            {
                itemVM.IsSelected = !itemVM.IsSelected;
                ItemSelectedProcess(itemVM);
            }

            return;
        }

        Image? image = fe.FindDescendantOrSelf<Image>();
        if (image != null 
            && _vm.OpenImageViewerCommand is ICommand command
            && command.CanExecute(image.DataContext)
            )
        {
            if (image.Source != null)
            {
                SaveScrollStatus(fe);

                var anim = ConnectedAnimationService.GetForCurrentView()
                    .PrepareToAnimate(PageTransitionHelper.ImageJumpConnectedAnimationName, image);
                anim.Configuration = new BasicConnectedAnimationConfiguration();
            }

            command.Execute(image.DataContext);
        }
    }

    [ObservableProperty]
    IReadOnlyList<IStorageItemViewModel>? _selectedItems = new List<IStorageItemViewModel>();


    void ImageListToggleSelectButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is ToggleButton toggleButton)
        {
            ItemSelectedProcess((IStorageItemViewModel)toggleButton.DataContext);
        }
    }

    void ItemSelectedProcess(IStorageItemViewModel itemVM)
    {
        var prevSelectedItemIndex = _lastSelectedItemIndex;
        var lastSelectedItemsCount = SelectedItemsCount;
        //if (prevSelectedItemIndex >= 0 
        //    && Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Shift) != CoreVirtualKeyStates.None
        //    )
        //{

        //}
        //else
        {
            if (itemVM.IsSelected)
            {
                _vm.Selection.SelectedItems.Add(itemVM);
                _selectedItems = _vm.Selection.SelectedItems;
            }
            else
            {
                _vm.Selection.SelectedItems.Remove(itemVM);
                _selectedItems = _vm.Selection.SelectedItems;
            }
            SelectedItemsCount = SelectedItemsCount + (itemVM.IsSelected ? 1 : -1);
            _vm.Selection.ForceNotifySelectedItems();

            _vm.FileDeleteCommand.NotifyCanExecuteChanged();
        }

        _lastSelectedItemIndex = _vm.FileItemsView.IndexOf(itemVM);

        var selectedItemsCount = SelectedItemsCount;
        if (selectedItemsCount > 0)
        {
            SelectedCountDisplayText = "ImageSelection_SelectedCount".Translate(selectedItemsCount);
        }

        if (lastSelectedItemsCount == 0 && selectedItemsCount > 0)
        {
            StartSelection();
        }        
    }

    public int SelectedItemsCount
    {
        get { return (int)GetValue(SelectedItemsCountProperty); }
        set { SetValue(SelectedItemsCountProperty, value); }
    }

    public static readonly DependencyProperty SelectedItemsCountProperty =
        DependencyProperty.Register("SelectedItemsCount", typeof(int), typeof(ImageListupPage), new PropertyMetadata(0));





    public void StartSelection()
    {
        _selectedItems = _vm.Selection.SelectedItems;
        _vm.Selection.StartSelection();
        _messenger.Send(new MenuDisplayMessage(Visibility.Collapsed));
        if (_messenger.IsRegistered<BackNavigationRequestingMessage>(this) is false)
        {
            _messenger.Register<BackNavigationRequestingMessage>(this, (r, m) =>
            {
                m.Value.IsHandled = true;
                ClearSelection();
            });
        }

        if (_vm.CurrentFolderItem?.Type == Core.Models.StorageItemTypes.Albam
            && _messenger.IsRegistered<Core.Models.Albam.AlbamItemRemovedMessage>(this) is false)
        {
            _messenger.Register<Core.Models.Albam.AlbamItemRemovedMessage>(this, (r, m) =>
            {
                var (albamId, path, itemType) = m.Value;
                List<IStorageItemViewModel> removeTargets = new();
                foreach (var itemVM in _vm.Selection.SelectedItems)
                {
                    if (itemVM.Item is AlbamItemImageSource albamItem)
                    {
                        if (albamItem.AlbamId == albamId && albamItem.Path == path)
                        {
                            removeTargets.Add(itemVM);
                        }
                    }
                }

                foreach (var itemVM in removeTargets)
                {
                    itemVM.IsSelected = false;
                    ItemSelectedProcess(itemVM);
                }
            });
        }        
    }

    public void ClearSelection()
    {
        foreach (var itemVM in _selectedItems ?? [])
        {
            itemVM.IsSelected = false;
        }

        _selectedItems = null;
        SelectedCountDisplayText = String.Empty;
        SelectedItemsCount = 0;
        _vm.Selection.EndSelection();
        _lastSelectedItemIndex = -1;
        _messenger.Send(new MenuDisplayMessage(Visibility.Visible));
        _messenger.Unregister<BackNavigationRequestingMessage>(this);
        _messenger.Unregister<Core.Models.Albam.AlbamItemRemovedMessage>(this);
    }

    [RelayCommand]
    void SelectionChange(object item)
    {
        if (item == null) { return; }

        IStorageItemViewModel itemVM = (IStorageItemViewModel)item;
        itemVM.IsSelected = !itemVM.IsSelected;
        ItemSelectedProcess(itemVM);
    }

    public string SelectedCountDisplayText
    {
        get { return (string)GetValue(SelectedCountDisplayTextProperty); }
        set { SetValue(SelectedCountDisplayTextProperty, value); }
    }

    public static readonly DependencyProperty SelectedCountDisplayTextProperty =
        DependencyProperty.Register("SelectedCountDisplayText", typeof(string), typeof(ImageListupPage), new PropertyMetadata(string.Empty));

    bool CanMoveToFolderSelectedItems(StorageFolder? folder)
    {
        return folder != null;
    }

    [RelayCommand(CanExecute = nameof(CanMoveToFolderSelectedItems))]
    async Task MoveToFolderSelectedItemsAsync(StorageFolder? hostFolder)
    {
        if (hostFolder == null) { return; }
        if (_vm.Selection.SelectedItems is not { } items || items.Count == 0) { return; }


        await MoveItemsToAsync(hostFolder, items.Select(x => x.Item.StorageItem), default);
        _messenger.SendShowTextNotificationMessage(items.Count == 1
            ? "MoveToFolder_Completed_Single".Translate((items[0]).Name, hostFolder.Name)
            : "MoveToFolder_Completed_Multi".Translate(items.Count, hostFolder.Name));

        StringBuilder sb = new StringBuilder();
        foreach (var item in items.Cast<IStorageItemViewModel>())
        {
            _messenger.Send(new StorageItemNotFoundMessage(item.Path));
            sb.Clear();
            sb.Append(hostFolder.Path);
            sb.Append(Path.DirectorySeparatorChar);
            sb.Append(item.Name);
            _messenger.Send(new StroageItemMovedOrRenamedMessage(sb.ToString(), item.Path));
        }

        _vm.Selection.SelectedItems.Clear();
    }

    [RelayCommand]
    void ToggleSiblingFolderPaneDisplay()
    {
        FolderSelectionSplitView.IsPaneOpen = !FolderSelectionSplitView.IsPaneOpen;
    }

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

    void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _messenger.Send(new InPageSearchRequestMessage(sender.Text));
        if (!sender.Items.Any())
        {
            sender.ItemsSource = new object[1] { new { Name = "Search_FromAll".Translate() } };
        }
        sender.IsSuggestionListOpen = !string.IsNullOrWhiteSpace(sender.Text);
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


    #region Selection

    [ObservableProperty]
    ObservableCollection<StorageFolder>? _folders;


    async Task InitializeMoveToFolders(CancellationToken ct)
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
                    Folders?.Insert(0, newfodler);
                    ToggleDisplaySiblingFoldersButton.IsEnabled = true;
                    FolderSelectionSplitView.IsPaneOpen = true;
                    _vm.HasFolderOrBookItem = true;
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

    void HandleCreateFolderDialogTextChanging(CancellationToken ct)
    {
        R3.Extensions.ObservableEventExtensions.ObserveTextChanged(CreateFolderDialogTextBox)
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
            .RegisterTo(ct);

        CreateFolderDialog.IsPrimaryButtonEnabled = false;
    }

    #endregion

    private void ItemControl_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        args.AllowedOperations = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }
}


public class ImageItemWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double ratioWH)
        {
            int itemHeight = (int)parameter;
            return (double)(itemHeight * (float)ratioWH);
        }
        else if (value is float ratioWHf)
        {
            int itemHeight = (int)parameter;
            return (double)(itemHeight * ratioWHf);
        }
        else 
        {
            return double.NaN;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
