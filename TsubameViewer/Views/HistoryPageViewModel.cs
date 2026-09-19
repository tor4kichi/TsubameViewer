using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
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

#nullable enable
namespace TsubameViewer.ViewModels;

public sealed partial class HistoryPageViewModel 
    : NavigationAwareViewModelBase
    , IRecipient<ImageSourceFavoriteChanged>
{
    [ObservableProperty]
    string _filterText = "";

    readonly List<Regex> _migemoQueryRegexItems = [];

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
            if (_migemoQueryRegexItems.All(x => x.IsMatch(itemVM.Name) == true)) { return true; }
            return itemVM.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
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
                    item.RestoreThumbnailLoadingTask(ct);
                    if (item.Name.Equals(lastIntaractItemPath, StringComparison.Ordinal))
                    {
                        item.ThumbnailChanged();
                        break;
                    }
                }
            }

            DisposableBuilder db = new();
            this.ObservePropertyChanged(x => x.FilterText, false)
                .ThrottleLast(TimeSpan.FromSeconds(0.5))
                .SubscribeAwait(this, static async (x, s, ct) =>
                {
                    s._migemoQueryRegexItems.Clear();
                    if (s._folderListingSettings.IsInPageSearchWithMigemo)
                    {
                        try
                        {
                            s._migemoQueryRegexItems.AddRange(
                                x.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                                .Select(MigemoService.Query));
                        }
                        catch { }
                    }
                    s.FilteredItems.RefreshFilter(ct);
                }, AwaitOperation.Switch)
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
