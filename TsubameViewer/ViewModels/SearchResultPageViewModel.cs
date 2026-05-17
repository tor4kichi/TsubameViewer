using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using R3;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.Views;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace TsubameViewer.ViewModels;

public sealed partial class SearchResultPageViewModel 
    : NavigationAwareViewModelBase
    , IRecipient<SearchQuerySubmitedRequestMessage>
{

    public void Receive(SearchQuerySubmitedRequestMessage message)
    {
        _filterText = message.Value;
        OnPropertyChanged(nameof(FilterText));
    }

    public Visibility NotEmptyToVisible(string s)
    {
        return string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;
    }

    [ObservableProperty]
    string _filterText = "";

    private readonly IMessenger _messenger;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly LocalBookmarkRepository _bookmarkManager;
    private readonly AlbamRepository _albamRepository;
    private readonly ThumbnailImageManager _thumbnailManager;

    public ObservableCollection<ItemsGroupedByFolderViewModel> SearchResultItems { get; } = [];

    private string _SearchText;
    public string SearchText
    {
        get { return _SearchText; }
        set { SetProperty(ref _SearchText, value); }
    }

    [ObservableProperty]
    int _hitCount = 0;

    public ISecondaryTileManager SecondaryTileManager { get; }
    public OpenFolderItemCommand OpenFolderItemCommand { get; }
    public OpenFolderItemSecondaryCommand OpenFolderItemSecondaryCommand { get; }
    public OpenImageViewerCommand OpenImageViewerCommand { get; }
    public OpenImageListupCommand OpenImageListupCommand { get; }
    public OpenFolderListupCommand OpenFolderListupCommand { get; }
    public OpenWithExplorerCommand OpenWithExplorerCommand { get; }
    public SecondaryTileAddCommand SecondaryTileAddCommand { get; }
    public SecondaryTileRemoveCommand SecondaryTileRemoveCommand { get; }

    public SearchResultPageViewModel(
        IMessenger messenger,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        FolderListingSettings folderListingSettings,
        LocalBookmarkRepository bookmarkManager,
        AlbamRepository albamRepository,
        ThumbnailImageManager thumbnailManager,
        ISecondaryTileManager secondaryTileManager,

        OpenFolderItemCommand openFolderItemCommand,
        OpenFolderItemSecondaryCommand openFolderItemSecondaryCommand,
        OpenImageViewerCommand openImageViewerCommand,
        OpenImageListupCommand openImageListupCommand,
        OpenFolderListupCommand openFolderListupCommand,
        OpenWithExplorerCommand openWithExplorerCommand,
        SecondaryTileAddCommand secondaryTileAddCommand,
        SecondaryTileRemoveCommand secondaryTileRemoveCommand
        )
    {
        _messenger = messenger;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _bookmarkManager = bookmarkManager;
        _albamRepository = albamRepository;
        _thumbnailManager = thumbnailManager;
        SecondaryTileManager = secondaryTileManager;
        OpenFolderItemCommand = openFolderItemCommand;
        OpenFolderItemSecondaryCommand = openFolderItemSecondaryCommand;
        OpenImageViewerCommand = openImageViewerCommand;
        OpenImageListupCommand = openImageListupCommand;
        OpenFolderListupCommand = openFolderListupCommand;
        OpenWithExplorerCommand = openWithExplorerCommand;
        SecondaryTileAddCommand = secondaryTileAddCommand;
        SecondaryTileRemoveCommand = secondaryTileRemoveCommand;
    }


    CancellationToken _navigationCt;

    public override async Task OnNavigatedToAsync(INavigationParameters parameters, CancellationToken ct)
    {
        _navigationCt = ct;
        var mode = parameters.GetNavigationMode();
        if (mode == NavigationMode.Refresh)
        {
            return;
        }

        SearchResultItems.Clear();        
        if (parameters.TryGetValue("q", out string q))
        {
            FilterText = q;
        }
        else
        {
            throw new Exception();
        }

        this.ObservePropertyChanged(x => x.FilterText)
            .ThrottleLast(TimeSpan.FromSeconds(0.25))
            .SubscribeAwait(this, async (s, state, ct) => 
            {
                await state.ProcessSearchQueryAsync(s, ct);
            }, awaitOperation: AwaitOperation.Switch)
            .RegisterTo(ct);

        _messenger.Register<SearchQuerySubmitedRequestMessage>(this);

        await base.OnNavigatedToAsync(parameters, ct);
    }


    async Task ProcessSearchQueryAsync(string q, CancellationToken ct)
    {
        SearchText = q;
        ApplicationView.GetForCurrentView().Title = "SearchResultWith".Translate($"\"{q}\"");
        Dictionary<string, ItemsGroupedByFolderViewModel> groupByDir = [];
        SearchResultItems.Clear();
        try
        {
            int count = 0;
            await foreach (var entry in _sourceStorageItemsRepository.SearchAsync(q, ct).WithCancellation(ct))
            {
                var dirPath = Path.GetDirectoryName(entry.Path);
                if (!groupByDir.TryGetValue(dirPath, out var groupVM))
                {
                    groupVM = new ItemsGroupedByFolderViewModel(dirPath);
                    SearchResultItems.Add(groupVM);
                    groupByDir.Add(dirPath, groupVM);
                }

                groupVM.Items.Add(ConvertStorageItemViewModel(entry));
                count++;
            }

            HitCount = count;
        }
        catch (OperationCanceledException)
        {
            SearchResultItems.Clear();
        }
    }

    private StorageItemViewModel ConvertStorageItemViewModel(IStorageItem storageItem)
    {
        var storageItemImageSource = new StorageItemImageSource(storageItem);
        return new StorageItemViewModel(storageItemImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository);
    }

    public override void OnNavigatedFrom(INavigationParameters parameters)
    {
        _messenger.Unregister<SearchQuerySubmitedRequestMessage>(this);

        base.OnNavigatedFrom(parameters);
    }
}

public class ItemsGroupedByFolderViewModel
{
    public ItemsGroupedByFolderViewModel(string directoryPath)
    {
        DirectoryPath = new Uri(directoryPath);
        Name = Uri.UnescapeDataString(DirectoryPath.Segments[DirectoryPath.Segments.Length - 1]);
    }

    public Uri DirectoryPath { get; }
    public string Name { get; }
    public ObservableCollection<IStorageItemViewModel> Items { get; } = [];
}

