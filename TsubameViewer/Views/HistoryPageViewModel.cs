using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI;
using R3;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.Views;
using ZLinq;

namespace TsubameViewer.ViewModels;

public sealed partial class HistoryPageViewModel 
    : NavigationAwareViewModelBase    
{
    [ObservableProperty]
    string _filterText = "";

    private readonly IMessenger _messenger;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly LastIntractItemRepository _folderLastIntractItemManager;
    private readonly RecentlyAccessRepository _recentlyAccessRepository;
    private readonly LocalBookmarkRepository _bookmarkManager;
    private readonly AlbamRepository _albamRepository;
    private readonly ThumbnailImageManager _thumbnailManager;

    public ObservableCollection<StorageItemViewModel> RecentlyItems { get; } = [];

    public KeyIndexMappedAdvancedCollectionView<IStorageItemViewModel> FilteredItems { get; }
    public OpenFolderItemCommand OpenFolderItemCommand { get; }

    public HistoryPageViewModel(
        IMessenger messenger,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        LastIntractItemRepository folderLastIntractItemManager,
        RecentlyAccessRepository recentlyAccessRepository,

        LocalBookmarkRepository bookmarkManager,
        AlbamRepository albamRepository,
        ThumbnailImageManager thumbnailManager,

        OpenFolderItemCommand openFolderItemCommand
        )
    {
        _messenger = messenger;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _folderLastIntractItemManager = folderLastIntractItemManager;
        _recentlyAccessRepository = recentlyAccessRepository;
        _bookmarkManager = bookmarkManager;
        _albamRepository = albamRepository;
        _thumbnailManager = thumbnailManager;
        OpenFolderItemCommand = openFolderItemCommand;

        FilteredItems = new (RecentlyItems, itemVM => itemVM.Path);
        FilteredItems.Filter = s => string.IsNullOrWhiteSpace(_filterText) ? true : ((s as IStorageItemViewModel).Name?.Contains(_filterText, StringComparison.Ordinal) ?? false);
    }

    [ObservableProperty]
    bool _nowProcessing;

    public override void OnNavigatedFrom(INavigationParameters parameters)
    {

        base.OnNavigatedFrom(parameters);
    }

    public override async Task OnNavigatedToAsync(INavigationParameters parameters, CancellationToken ct)
    {
        async Task<StorageItemViewModel> ToStorageItemViewModel((string Path, DateTimeOffset LastAccessTime) entry)
        {
            var storageItem = await _sourceStorageItemsRepository.TryGetStorageItemFromPath(entry.Path);
            if (storageItem == null) { throw new FileNotFoundException(entry.Path); }

            var storageItemImageSource = new StorageItemImageSource(storageItem);
            return new StorageItemViewModel(storageItemImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository);
        }

        try
        {
            using var deferRefresh = FilteredItems.DeferRefresh();
            NowProcessing = true;

            var recentlyAccessItems = _recentlyAccessRepository.GetItemsSortWithRecently(100);
            if (recentlyAccessItems.Select(x => x.Path).SequenceEqual(RecentlyItems.Select(x => x.Path)) is false)
            {
                foreach (var itemVM in RecentlyItems)
                {
                    itemVM.Dispose();
                }

                RecentlyItems.Clear();
                foreach (var item in recentlyAccessItems)
                {
                    try
                    {
                        var itemVM = await ToStorageItemViewModel(item);
                        RecentlyItems.Add(itemVM);
                    }
                    catch
                    {
                        _recentlyAccessRepository.Delete(item.Path);
                    }

                    ct.ThrowIfCancellationRequested();
                }
            }
            else
            {
                var lastIntaractItemPath = _folderLastIntractItemManager.GetLastIntractItemName(nameof(SourceStorageItemsPageViewModel));
                foreach (var item in RecentlyItems)
                {
                    if (item.Name == lastIntaractItemPath)
                    {
                        item.ThumbnailChanged();
                        _ = item.InitializeAsync(ct);
                    }
                }
            }

            DisposableBuilder db = new();
            this.ObservePropertyChanged(x => x.FilterText, false)
                .Debounce(TimeSpan.FromMilliseconds(500))
                .Subscribe(_ => FilteredItems.RefreshFilter())
                .AddTo(ref db);

            db.Build().RegisterTo(ct);

            await base.OnNavigatedToAsync(parameters, ct);
        }
        finally
        {
            NowProcessing = false;
        }
    }

    [RelayCommand]
    async Task RemoveAllHistoryAsync()
    {
        RecentlyItems.Clear();
        _recentlyAccessRepository.DeleteAll();
    }
}
