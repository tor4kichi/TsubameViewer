using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable
namespace TsubameViewer.Views;

public sealed partial class SearchResultPage : Page, ITitlebarContentAware
{
    public DataTemplate GetContent()
    {
        return null;
    }

    private readonly SearchResultPageViewModel _vm;

    public SearchResultPage()
    {
        this.InitializeComponent();

        this.FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging1;

        DataContext = _vm = Ioc.Default.GetRequiredService<SearchResultPageViewModel>();
    }

    private void FoldersAdaptiveGridView_ContainerContentChanging1(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is IStorageItemViewModel itemVM)
        {
            if (_navigationCts.IsCancellationRequested is false)
            {
                ToolTipService.SetToolTip(args.ItemContainer, new ToolTip() { Content = new TextBlock() { Text = itemVM.Name, TextWrapping = TextWrapping.Wrap } });
            }

            itemVM.InitializeAsync(_ct);
        }
    }

    CancellationTokenSource _navigationCts;
    CancellationToken _ct;
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _navigationCts = new CancellationTokenSource();
        _ct = _navigationCts.Token;
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        _navigationCts.Cancel();
        _navigationCts.Dispose();            
        base.OnNavigatingFrom(e);
    }
}
