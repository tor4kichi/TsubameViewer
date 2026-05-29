using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using Microsoft.Toolkit.Uwp;
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Microsoft.UI.Xaml.Controls;
using R3;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Core;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Converters;
using TsubameViewer.Views.Helpers;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using ZLinq;

#nullable enable
namespace TsubameViewer.Views;


public static class ObservableCollectionExtensions
{
    public static void InsertSorted<T>(this ObservableCollection<T> collection, T item, Comparison<T> comparison)
    {
        int index = 0;
        while (index < collection.Count && comparison(collection[index], item) < 0)
        {
            index++;
        }
        collection.Insert(index, item);
    }

    public static void InsertSortedDescending<T>(this ObservableCollection<T> collection, T item, Comparison<T> comparison)
    {
        int index = 0;
        while (index < collection.Count && comparison(collection[index], item) > 0)
        {
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

    private readonly ImageListupPageViewModel _vm;
    private readonly IMessenger _messenger;
    private readonly FocusHelper _focusHelper;

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

    private void FolderListupPage_Loaded(object sender, RoutedEventArgs e)
    {
        ContentViewTypeSelector.SelectedIndex = 1;

        _messenger.Register<RequestConnectedAnimationMessage>(this, (r, m) => 
        {
            var image = _realizedItems.FirstOrDefault(x => (x.DataContext as IStorageItemViewModel)?.Path.Equals(m.TargetImage.Path, StringComparison.Ordinal) ?? false);
            if (image is { } target)
            {
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



    public async Task<UIElement?> BringIntoViewLastIntractItem_ForConnectedAnimation(CancellationToken ct)
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
                var currentItemsRepeater = GetCurrentDisplayItemsRepeater();

                if (currentItemsRepeater == null) { return null; }

                UIElement? lastIntractItem = currentItemsRepeater.GetOrCreateElement(lastIntractItemIndex);                
                lastIntractItem.UpdateLayout();
                if (lastIntractItem is Control control)
                {
                    if (lastIntractItem.ActualOffset.Y < ItemsScrollViewer.VerticalOffset
                        || ItemsScrollViewer.VerticalOffset + ItemsScrollViewer.ViewportHeight < lastIntractItem.ActualOffset.Y)
                    {
                        var targetOffset = lastIntractItem.ActualOffset.Y - (float)ItemsScrollViewer.ViewportHeight * 0.5f;
                        ItemsScrollViewer.ChangeView(null, targetOffset, null, disableAnimation: true);
                    }
                    else
                    {
                        control.StartBringIntoView();
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


    private void FolderListupPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _messenger.Unregister<RequestConnectedAnimationMessage>(this);

        StopLoadingTaskMonitor();
    }

    private void ContentViewTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selector = (Selector)sender;
        if (selector.IsLoaded && selector.SelectedIndex == 0 && _vm?.CurrentFolderItem != null)
        {
            _messenger.NavigateAsync(nameof(FolderListupPage), PageTransitionHelper.CreatePageParameter(_vm?.CurrentFolderItem.Item));
        }
    }


    #region Image Loading

    IDisposable? _lodingTaskMonitor;
    async void StartLoadingTaskMonitor(CancellationToken ct)
    {
        StopLoadingTaskMonitor();
        _lastVerticalOffset = 0;
        DisposableBuilder db = new();
        Debug.WriteLine("LoadingTaskMonitor START.");
        R3.Observable.Merge(
                _priorityLoadPendingItems.CollectionChangedAsObservable().ToObservable().AsUnitObservable(),
                _loadPendingItems.CollectionChangedAsObservable().ToObservable().AsUnitObservable(),
                R3.Observable.Interval(TimeSpan.FromMilliseconds(1000)),
                ItemsScrollViewer.ObserveDependencyProperty(ScrollViewer.VerticalOffsetProperty).ToObservable().AsUnitObservable())
            .Debounce(TimeSpan.FromMilliseconds(100))
            .SubscribeAwait(async (_, ct) =>
            {
                var currentVerticalOffset = ItemsScrollViewer.VerticalOffset;
                
                UpdateVisibleRangeItemInitialize();
                if (!_priorityLoadPendingItems.Any() && !_loadPendingItems.Any()) { return; }
                _isVisibleRangeUpdated = false;
                bool scrollDesc = currentVerticalOffset < _lastVerticalOffset;
                _lastVerticalOffset = currentVerticalOffset;
                
                if (_priorityLoadPendingItems.Any())
                {
                    Debug.WriteLine("LoadingTaskMonitor primary.");
                    try
                    {
                        using var items = scrollDesc ? _priorityLoadPendingItems.AsValueEnumerable().Reverse().ToArrayPool() : _priorityLoadPendingItems.AsValueEnumerable().ToArrayPool();
                        var tasks = items.AsValueEnumerable().Select(itemVM => itemVM.InitializeAsync(ct)).ToList();
                        int count = tasks.Count;
                        while (await ValueTaskSupplement.ValueTaskEx.WhenAny(tasks) is int index)
                        {
                            tasks.RemoveAt(index);
                            count--;
                            if (count <= 0)
                            {
                                Debug.WriteLineIf(_isVisibleRangeUpdated, "LoadingTaskMonitor SKIP primary.");
                                break;
                            }
                        }

                        _priorityLoadPendingItems.Clear();
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }

                if (_loadPendingItems.Any())
                {
                    Debug.WriteLine("LoadingTaskMonitor secondary.");
                    try
                    {
                        using var items = scrollDesc ? _loadPendingItems.AsValueEnumerable().Reverse().ToArrayPool() : _loadPendingItems.AsValueEnumerable().ToArrayPool();
                        var tasks = items.AsValueEnumerable().Select(itemVM => itemVM.InitializeAsync(ct)).ToList();
                        int count = tasks.Count;
                        foreach (var ignore in ValueEnumerable.Range(0, tasks.Count))
                        {
                            int index = await ValueTaskSupplement.ValueTaskEx.WhenAny(tasks);
                            tasks.RemoveAt(index);
                            await Task.Delay(1);
                            count--;
                            if (count <= 0 || _isVisibleRangeUpdated)
                            {
                                Debug.WriteLineIf(_isVisibleRangeUpdated, "LoadingTaskMonitor SKIP secondary.");
                                break;
                            }
                        }
                        if (!_isVisibleRangeUpdated)
                        {
                            _loadPendingItems.Clear();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }

                if (_priorityLoadPendingItems.Count == 0 && _loadPendingItems.Count == 0)
                {
                    Debug.WriteLine("LoadingTaskMonitor STOP.");                    
                }
                else
                {
                    Debug.WriteLine("LoadingTaskMonitor Continue.");
                }
            }, (_) => StopLoadingTaskMonitor(), awaitOperation: AwaitOperation.Drop)
            .AddTo(ref db);

        foreach (var item in _realizedItems)
        {
            if (item.DataContext is IStorageItemViewModel itemVM)
            {
                itemVM.RestoreThumbnailLoadingTask(ct);
            }
        }

        _lodingTaskMonitor = db.Build();
    }

    void StopLoadingTaskMonitor()
    {
        if (_lodingTaskMonitor == null) { return; }

        _lodingTaskMonitor?.Dispose();
        _lodingTaskMonitor = null;
        Debug.WriteLine("LoadingTaskMonitor STOP.");
    }



    ItemsRepeater? GetCurrentItemsRepeater() => ItemsSwitchPresenter.Content as ItemsRepeater;


    bool _isVisibleRangeUpdated;
    ObservableCollection<IStorageItemViewModel> _priorityLoadPendingItems = [];
    ObservableCollection<IStorageItemViewModel> _loadPendingItems = [];

    double _lastVerticalOffset;
    void UpdateVisibleRangeItemInitialize()
    {
        //Debug.WriteLine("LoadingTaskMonitor UpdateVisibleRangeItemInitialize.");
        if (GetCurrentItemsRepeater() is not { } itemRepeater) { return; }

        var sv = ItemsScrollViewer;
        Rect boundingBox = sv.ActualSize.ToSize().ToRect();
        Rect currentContentArea = boundingBox;
        currentContentArea.Y -= 140; // アイテムの高さ分を調整
        currentContentArea.Height += 140 * 2;
        double expandLoadingArea = sv.ActualHeight * 2;
        Point scrollPos = new(0, -sv.VerticalOffset);
        Comparison<IStorageItemViewModel> comparisonItemVM = (x, y) =>
        {
            return Comparer<int>.Default.Compare(_vm.FileItemsView.IndexOf(x), _vm.FileItemsView.IndexOf(y));
        };

        _priorityLoadPendingItems.Clear();
        _loadPendingItems.Clear();

        using var items = _realizedItems.AsValueEnumerable().ToArrayPool();
        foreach (var item in items.Span)
        {
            if (item.DataContext is not IStorageItemViewModel itemVM) { continue; }
            if (itemVM.IsRequestImageLoading || itemVM.IsInitialized) { continue; }

            var t = item.TransformToVisual(FileItemsContainer);
            var pos = t.TransformPoint(scrollPos);
            if (currentContentArea.Contains(pos))
            {
                _priorityLoadPendingItems.InsertSorted(itemVM, comparisonItemVM);
            }
            else
            {
                _loadPendingItems.InsertSorted(itemVM, comparisonItemVM);
            }
        }
        _isVisibleRangeUpdated = true;
    }

    #endregion


    #region 初期フォーカス設定

    CancellationTokenSource _navigationCts;
    CancellationToken _ct;
    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        _messenger.Unregister<StartMultiSelectionMessage>(this);

        _navigationCts.Cancel();
        _navigationCts.Dispose();

        ClearSelection();

        base.OnNavigatingFrom(e);
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _navigationCts = new CancellationTokenSource();
        var ct = _ct = _navigationCts.Token;

        _messenger.Register<StartMultiSelectionMessage>(this, (r, m) =>
        {
            StartSelection();
        });

        try
        {
            if (e.NavigationMode == NavigationMode.New)
            {
                await ResetScrollPosition(ct);

                if (_focusHelper.IsRequireSetFocus())
                {
                    var firstItem = await WaitTargetIndexItemLoadingAsync(0, ct);
                    if (firstItem != null)
                    {
                        (firstItem as Control)?.Focus(FocusState.Keyboard);
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
        finally
        {
        }

        StartLoadingTaskMonitor(ct);
        UpdateVisibleRangeItemInitialize();
    }

    private void SaveScrollStatus(UIElement target)
    {
        if (target is FrameworkElement fe
                    && fe.DataContext is IStorageItemViewModel itemVM)
        {
            _vm.SetLastIntractItem(itemVM);
        }
    }

    private async Task<UIElement?> WaitTargetIndexItemLoadingAsync(int index, CancellationToken ct)
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


    private async Task ResetScrollPosition(CancellationToken ct)
    {
        var lastIntractItem = await WaitTargetIndexItemLoadingAsync(0, ct);
        ItemsScrollViewer.ChangeView(null, 0.0, null, disableAnimation: true);

        if (_focusHelper.IsRequireSetFocus() && lastIntractItem is Control control)
        {
            control.Focus(FocusState.Keyboard);
        }
    }


    private void FileItemsRepeater_Large_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
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
        if (FileItemsRepeater_Small.Visibility == Visibility.Visible) { return FileItemsRepeater_Small; }
        else if (FileItemsRepeater_Midium.Visibility == Visibility.Visible) { return FileItemsRepeater_Midium; }
        else if (FileItemsRepeater_Large.Visibility == Visibility.Visible) { return FileItemsRepeater_Large; }
        else { return null; }
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

    ObservableCollection<FrameworkElement> _realizedItems = [];
    private void FileItemsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is FrameworkElement fe)
        {
            _realizedItems.Add(fe);
            if (fe.DataContext is IStorageItemViewModel itemVM)
            {
                _ = itemVM.EnsureImageSizeRatioAsync(_ct);
            }
        }
    }

    private void FileItemsRepeater_Large_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        if (args.Element is FrameworkElement fe)
        {
            _realizedItems.Remove(fe);
            if (fe.DataContext is IStorageItemViewModel itemVM)
            {
                itemVM.StopImageLoading();
            }
        }
    }


    private readonly AnimationBuilder _zoomUpAnimation = AnimationBuilder.Create()
            .Scale(new Vector2(1.020f, 1.020f), duration: TimeSpan.FromMilliseconds(50));

    private readonly AnimationBuilder _zoomDownAnimation = AnimationBuilder.Create()
            .Scale(new Vector2(1, 1), duration: TimeSpan.FromMilliseconds(50));

    private void Image_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        var item = (FrameworkElement)sender;
        var image = item.FindChild<Image>();
        if (image != null)
        {
            _zoomUpAnimation
                .CenterPoint(new Vector2((float)image.ActualWidth * 0.5f, (float)image.ActualHeight * 0.5f), duration: TimeSpan.FromMilliseconds(1))
                .Start(image);
        }

        if (item.DataContext is IStorageItemViewModel itemVM)
        {
            if (itemVM.Image?.IsAnimatedBitmap ?? false)
            {
                itemVM.Image.Play();
            }
        }
    }


    private void Image_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        var item = (FrameworkElement)sender;
        var image = item.FindChild<Image>();
        if (image == null) { return; }
        _zoomDownAnimation
            .CenterPoint(new Vector2((float)image.ActualWidth * 0.5f, (float)image.ActualHeight * 0.5f), duration: TimeSpan.FromMilliseconds(1))
            .Start(image);

        if (item.DataContext is IStorageItemViewModel itemVM)
        {
            if (itemVM.Image?.IsAnimatedBitmap ?? false)
            {
                itemVM.Image.Stop();
            }
        }
    }

    RelayCommand<TappedRoutedEventArgs> _PrepareConnectedAnimationWithTappedItemCommand;
    public RelayCommand<TappedRoutedEventArgs> PrepareConnectedAnimationWithTappedItemCommand => _PrepareConnectedAnimationWithTappedItemCommand ??= new RelayCommand<TappedRoutedEventArgs>(item =>
    {
        SaveScrollStatus((UIElement)item!.OriginalSource);

        var image = ((UIElement)item!.OriginalSource).FindDescendantOrSelf<Image>();
        if (image?.Source != null)
        {
            var anim = ConnectedAnimationService.GetForCurrentView()
                .PrepareToAnimate(PageTransitionHelper.ImageJumpConnectedAnimationName, image);
            anim.Configuration = new BasicConnectedAnimationConfiguration();
        }
    });

    [RelayCommand]
    void PrepareConnectedAnimationWithCurrentFocusElement(UIElement item)
    {
        if (item == null) { return; }

        SaveScrollStatus(item);

        var image = item.FindDescendantOrSelf<Image>();
        if (image?.Source != null)
        {
            var anim = ConnectedAnimationService.GetForCurrentView()
                .PrepareToAnimate(PageTransitionHelper.ImageJumpConnectedAnimationName, image);
            anim.Configuration = new BasicConnectedAnimationConfiguration();
        }
    }

    private int _lastSelectedItemIndex = -1;
    private void ImageListItem_Clicked(object sender, RoutedEventArgs e)
    {
        var fe = (FrameworkElement)sender;
        if (IsSelectionModeEnabled
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


    private void ImageListToggleSelectButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is ToggleButton toggleButton)
        {
            ItemSelectedProcess((StorageItemViewModel)toggleButton.DataContext);
        }
    }

    private void ItemSelectedProcess(IStorageItemViewModel itemVM)
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

            _vm.FileDeleteCommand.NotifyCanExecuteChanged();
            _vm.OpenWithExplorerCommand.NotifyCanExecuteChanged();
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
        else if (lastSelectedItemsCount > 0 && selectedItemsCount == 0)
        {
            ClearSelection();
        }
    }

    public int SelectedItemsCount
    {
        get { return (int)GetValue(SelectedItemsCountProperty); }
        set { SetValue(SelectedItemsCountProperty, value); }
    }

    // Using a DependencyProperty as the backing store for SelectedItemsCount.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty SelectedItemsCountProperty =
        DependencyProperty.Register("SelectedItemsCount", typeof(int), typeof(ImageListupPage), new PropertyMetadata(0));




    public bool IsSelectionModeEnabled
    {
        get { return (bool)GetValue(IsSelectionModeEnabledProperty); }
        set { SetValue(IsSelectionModeEnabledProperty, value); }
    }

    // Using a DependencyProperty as the backing store for IsSelectionModeEnabled.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty IsSelectionModeEnabledProperty =
        DependencyProperty.Register("IsSelectionModeEnabled", typeof(bool), typeof(ImageListupPage), new PropertyMetadata(false));



    public void StartSelection()
    {
        IsSelectionModeEnabled = true;
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
        IsSelectionModeEnabled = false;
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

    private RelayCommand<object> _SelectionChangeCommand;
    public RelayCommand<object> SelectionChangeCommand => _SelectionChangeCommand ??= new RelayCommand<object>(item =>
    {
        if (item == null) { return; }

        StorageItemViewModel itemVM = (StorageItemViewModel)item;
        itemVM.IsSelected = !itemVM.IsSelected;
        ItemSelectedProcess(itemVM);
    });

    public string SelectedCountDisplayText
    {
        get { return (string)GetValue(SelectedCountDisplayTextProperty); }
        set { SetValue(SelectedCountDisplayTextProperty, value); }
    }

    // Using a DependencyProperty as the backing store for SelectedCountDisplayText.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty SelectedCountDisplayTextProperty =
        DependencyProperty.Register("SelectedCountDisplayText", typeof(string), typeof(ImageListupPage), new PropertyMetadata(string.Empty));

    private void AlbamItemManagementFlyout_Opening(object sender, object e)
    {
        MenuFlyout menuFlyout = (MenuFlyout)sender;
        menuFlyout.Items.Clear();
        AlbamRepository albamRepository = Ioc.Default.GetRequiredService<AlbamRepository>();
        var expandImageSources = _vm.Selection.SelectedItems.Select(x => x.Item);
        foreach (var albam in albamRepository.GetAlbams())
        {                
            if (expandImageSources.Any(x => albamRepository.IsExistAlbamItem(albam._id, x.Path)) is false)
            {
                // 一つも登録されていないなら全部を登録する
                menuFlyout.Items.Add(new ToggleMenuFlyoutItem() { Text = albam.Name, Command = new AlbamItemAddCommand(albamRepository, albam), CommandParameter = expandImageSources.Where(x => x.StorageItem is not null), IsChecked = false });
            }
            else if (expandImageSources.All(x => albamRepository.IsExistAlbamItem(albam._id, x.Path)))
            {
                // 全て登録済みなら全て削除
                menuFlyout.Items.Add(new ToggleMenuFlyoutItem() { Text = albam.Name, Command = new AlbamItemAddCommand(albamRepository, albam), CommandParameter = expandImageSources, IsChecked = true });
            }
            else 
            {
                // いずれかが登録済みなら、未登録アイテムを登録
                menuFlyout.Items.Add(new ToggleMenuFlyoutItem() { Text = albam.Name, Command = new AlbamItemAddCommand(albamRepository, albam), CommandParameter = expandImageSources.Where(x => x.StorageItem is not null && !albamRepository.IsExistAlbamItem(albam._id, x.Path)), IsChecked = true });
            }                
        }
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
