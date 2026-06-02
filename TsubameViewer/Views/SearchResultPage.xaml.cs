using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using I18NPortable;
using R3;
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
        return TitlebarContent;
    }

    public R3.Observable<string> ObserveTitleChanged()
    {        
        return _vm.ObservePropertyChanged(x => x.SearchText).Select(x => "SearchResultWith".Translate(x));
    }

    private readonly SearchResultPageViewModel _vm;
    private readonly IMessenger _messenger;

    public SearchResultPage()
    {
        this.InitializeComponent();

        this.FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging1;

        DataContext = _vm = Ioc.Default.GetRequiredService<SearchResultPageViewModel>();
        _messenger = Ioc.Default.GetRequiredService<IMessenger>();
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

        _messenger.Register<LatestContentViewUpdateMessage>(this, (r, m) =>
        {
            var itemVM = _vm.SearchResultItems.SelectMany(x => x.Items).FirstOrDefault(x => x.Path.Equals(m.Value, StringComparison.Ordinal));
            itemVM?.UpdateLastReadPosition();
        });
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        _navigationCts.Cancel();
        _navigationCts.Dispose();

        _messenger.Unregister<LatestContentViewUpdateMessage>(this);
        base.OnNavigatingFrom(e);
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
        //_messenger.Send(new InPageSearchRequestMessage(sender.Text));
        //if (!sender.Items.Any())
        //{
        //    sender.ItemsSource = new object[1] { new { Name = "Search_FromAll".Translate() } };
        //}
        //sender.IsSuggestionListOpen = !string.IsNullOrWhiteSpace(sender.Text);
    }

    private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        //_searchContext.SearchQuerySubmitCommand.Execute(sender.Text);
    }

    private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _searchContext?.SearchQuerySubmitCommand.Execute(sender.Text);
    }


    #endregion
}
