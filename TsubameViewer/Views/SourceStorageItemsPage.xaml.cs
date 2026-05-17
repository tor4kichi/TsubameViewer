using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Helpers;
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

#nullable enable
namespace TsubameViewer.Views;

public sealed partial class SourceStorageItemsPage : Page, ITitlebarContentAware
{
    public DataTemplate? GetContent()
    {
        return TitlebarContent;
    }

    public SourceStorageItemsPage()
    {
        this.InitializeComponent();

        this.FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging1;
        DataContext = _vm = Ioc.Default.GetRequiredService<SourceStorageItemsPageViewModel>();
        _focusHelper = Ioc.Default.GetRequiredService<FocusHelper>();
    }

    private readonly SourceStorageItemsPageViewModel _vm;
    private readonly FocusHelper _focusHelper;

    private void FoldersAdaptiveGridView_ContainerContentChanging1(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is IStorageItemViewModel itemVM)
        {
            if (itemVM.IsSourceStorageItem is false && itemVM.Name != null && _navigationCts.IsCancellationRequested is false)
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

            itemVM.InitializeAsync(_ct);

            if (_isFirstItem )
            {
                _isFirstItem = false;
                if (_focusHelper.IsRequireSetFocus() && itemVM.Type is not Core.Models.StorageItemTypes.AddFolder)
                {
                    args.ItemContainer.Focus(FocusState.Keyboard);
                }
            }
        }
    }

    CancellationTokenSource _navigationCts;
    CancellationToken _ct;
    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        _navigationCts.Cancel();
        _navigationCts.Dispose();

        base.OnNavigatingFrom(e);
    }

    bool _isFirstItem = false;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _navigationCts = new CancellationTokenSource();
        _ct = _navigationCts.Token;
        _isFirstItem = true;
    }
}
