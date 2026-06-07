using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using I18NPortable;
using R3;
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

    public R3.Observable<string> ObserveTitleChanged()
    {
        return Observable.Return(nameof(TsubameViewer));
    }

    public SourceStorageItemsPage()
    {
        this.InitializeComponent();

        this.FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging1;
        DataContext = _vm = Ioc.Default.GetRequiredService<SourceStorageItemsPageViewModel>();
        _focusHelper = Ioc.Default.GetRequiredService<FocusHelper>();
        _messenger = Ioc.Default.GetRequiredService<IMessenger>();
    }

    readonly SourceStorageItemsPageViewModel _vm;
    readonly FocusHelper _focusHelper;
    readonly IMessenger _messenger;

    void FoldersAdaptiveGridView_ContainerContentChanging1(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is IStorageItemViewModel itemVM)
        {
            if (itemVM.IsSourceStorageItem is false && itemVM.Name != null && _navigationCts?.IsCancellationRequested is false)
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

    CancellationTokenSource? _navigationCts;
    CancellationToken _ct;
    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        _navigationCts?.Cancel();
        _navigationCts?.Dispose();

        _messenger.Unregister<LatestContentViewUpdateMessage>(this);
        base.OnNavigatingFrom(e);
    }

    bool _isFirstItem = false;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _navigationCts = new CancellationTokenSource();
        _ct = _navigationCts.Token;
        _isFirstItem = true;

        _messenger.Register<LatestContentViewUpdateMessage>(this, (r, m) =>
        {
            var itemVM = _vm.Folders.FirstOrDefault(x => x.Path.Equals(m.Value, StringComparison.Ordinal));
            itemVM?.UpdateLastReadPosition();
        });
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
        //(sender as Control).Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        //(args.Element as Control).Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        //_messenger.Send(new InPageSearchRequestMessage(sender.Text));
        //if (!sender.Items.Any())
        //{
        //    sender.ItemsSource = new object[1] { new { Name = "Search_FromAll".Translate() } };
        //}
        //sender.IsSuggestionListOpen = !string.IsNullOrWhiteSpace(sender.Text);
    }

    void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        //_searchContext?.SearchQuerySubmitCommand.Execute(sender.Text);
    }

    void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        //_messenger.Send(new InPageSearchRequestMessage(sender.Text));
        //_messenger.Send(new SearchQuerySubmitedRequestMessage(sender.Text));
        _searchContext?.SearchQuerySubmitCommand.Execute(sender.Text);
    }


    #endregion
}
