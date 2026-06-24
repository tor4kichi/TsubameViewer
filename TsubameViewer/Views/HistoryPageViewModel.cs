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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.Views;
using ZLinq;

namespace TsubameViewer.ViewModels;

public sealed partial class HistoryPageViewModel 
    : NavigationAwareViewModelBase
    , IRecipient<ImageSourceFavoriteChanged>
{
    [ObservableProperty]
    string _filterText = "";

    Regex? _migemoQueryRegex;

    public void Receive(ImageSourceFavoriteChanged message)
    {
        var (imageSourcePath, isFav) = message.Value;
        foreach (var item in RecentlyItems)
        {
            if (item.Path?.Equals(imageSourcePath, StringComparison.Ordinal) ?? false)
            {
                item.IsFavorite = isFav;
                break;
            }
        }
    }

    readonly IMessenger _messenger;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    readonly LastIntractItemRepository _folderLastIntractItemManager;
    readonly RecentlyAccessRepository _recentlyAccessRepository;
    readonly LocalBookmarkRepository _bookmarkManager;
    readonly AlbamRepository _albamRepository;
    readonly ThumbnailImageManager _thumbnailManager;
    private readonly FolderListingSettings _folderListingSettings;

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
        FolderListingSettings folderListingSettings,
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
        _folderListingSettings = folderListingSettings;
        OpenFolderItemCommand = openFolderItemCommand;

        FilteredItems = new (RecentlyItems, itemVM => itemVM.Path);
        FilteredItems.Filter = s =>
        {
            if (s is not IStorageItemViewModel itemVM) { return true; }            
            if (string.IsNullOrEmpty(itemVM.Name)) { return true; }
            if (string.IsNullOrWhiteSpace(_filterText)) { return true; }
            return _migemoQueryRegex != null
                ? _migemoQueryRegex.IsMatch(itemVM.Name)
                : itemVM.Name.Contains(_filterText, StringComparison.Ordinal);
        };
    }

    [ObservableProperty]
    bool _nowProcessing;

    public override void OnNavigatedFrom(INavigationParameters parameters)
    {
        _messenger.Unregister<ImageSourceFavoriteChanged>(this);
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
                        item.InitializeAsync(ct).FireAndForgetSafe();
                    }
                }
            }

            DisposableBuilder db = new();
            this.ObservePropertyChanged(x => x.FilterText, false)
                .Debounce(TimeSpan.FromSeconds(0.25))
                .Subscribe(_ =>
                {
                    if (_folderListingSettings.IsInPageSearchWithMigemo)
                    {
                        try
                        {
                            _migemoQueryRegex = MigemoService.Query(_filterText);
                        }
                        catch
                        {
                            _migemoQueryRegex = null;
                        }
                    }
                    else { _migemoQueryRegex = null; }
                    FilteredItems.RefreshFilter();
                })
                .AddTo(ref db);

            db.Build().RegisterTo(ct);

            _messenger.Register<ImageSourceFavoriteChanged>(this);
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
