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
using TsubameViewer.ViewModels;
using TsubameViewer.Views.Converters;
using TsubameViewer.Views.Helpers;
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

public sealed partial class HistoryPage : Page, ITitlebarContentAware
{
    public DataTemplate? GetContent()
    {
        return TitlebarContent;
    }

    public R3.Observable<string> ObserveTitleChanged()
    {
        return R3.Observable.Return("HistoryPage_Title".Translate());
    }

    private readonly HistoryPageViewModel _vm;
    private readonly IMessenger _messenger;
    private readonly FocusHelper _focusHelper;

    public HistoryPage()
    {
        this.InitializeComponent();

        DataContext = _vm = Ioc.Default.GetRequiredService<HistoryPageViewModel>();
        _messenger = Ioc.Default.GetRequiredService<IMessenger>();
        _focusHelper = Ioc.Default.GetRequiredService<FocusHelper>();

        FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging;
    }

    CancellationToken _navigationCt;
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _navigationCt = this.GetCancellationTokenOnNavigatingFrom();
        base.OnNavigatedTo(e);
    }

    bool _isFirstItem;
    private void FoldersAdaptiveGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is IStorageItemViewModel itemVM)
        {
            if (itemVM.IsSourceStorageItem is false && itemVM.Name != null && _navigationCt.IsCancellationRequested is false)
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

            itemVM.InitializeAsync(_navigationCt);

            if (_isFirstItem)
            {
                _isFirstItem = false;
                if (_focusHelper.IsRequireSetFocus() && itemVM.Type is not Core.Models.StorageItemTypes.AddFolder)
                {
                    args.ItemContainer.Focus(FocusState.Keyboard);
                }
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
