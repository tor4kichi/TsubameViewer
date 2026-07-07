using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.WinUI;
using I18NPortable;
using R3;
using System.Threading;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Helpers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

#nullable enable
namespace TsubameViewer.Views;

public sealed partial class AlbamListupPage : Page, ITitlebarContentAware
{
    public DataTemplate? GetContent()
    {
        return null;
    }

    public R3.Observable<string> ObserveTitleChanged()
    {
        return R3.Observable.Return("Albam".Translate());
    }

    readonly AlbamListupPageViewModel _vm;
    readonly FocusHelper _focusHelper;

    public AlbamListupPage()
    {
        this.InitializeComponent();
        DataContext = _vm = Ioc.Default.GetRequiredService<AlbamListupPageViewModel>();
        _focusHelper = Ioc.Default.GetRequiredService<FocusHelper>();

        this.ItemsAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging1;
    }

    bool _isFirstItem = false;
    async void FoldersAdaptiveGridView_ContainerContentChanging1(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (_navigationCts == null || _navigationCts.IsCancellationRequested) { return; }
        var ct = _navigationCts.Token;
        if (args.Item is IStorageItemViewModel itemVM)
        {
            await itemVM.InitializeAsync(ct);
            if (itemVM.IsSourceStorageItem is false && itemVM.Name != null)
            {
                ToolTipService.SetToolTip(args.ItemContainer, new ToolTip() { Content = new TextBlock() { Text = itemVM.Name, TextWrapping = TextWrapping.Wrap } });
            }

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


    CancellationTokenSource? _navigationCts;
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _navigationCts = new CancellationTokenSource();
        _isFirstItem = true;
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        _navigationCts?.Cancel();
        _navigationCts?.Dispose();
        base.OnNavigatingFrom(e);
    }
}
