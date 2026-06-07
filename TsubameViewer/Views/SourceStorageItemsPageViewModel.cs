using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using I18NPortable;
using R3;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.Maintenance;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Navigation;
using static TsubameViewer.Core.Models.SourceFolders.SourceStorageItemsRepository;

#nullable enable
namespace TsubameViewer.ViewModels;

public sealed class SourceStorageItemsPageViewModel 
    : NavigationAwareViewModelBase
    , IRecipient<RemoveSourceStorageItemFromAppMessage>
    , IRecipient<ThumbnailImageUpdateRequestMessage>
{
    public void Receive(RemoveSourceStorageItemFromAppMessage message)
    {
        if (message.Value is StorageItemViewModel itemVM)
        {
            Folders.Remove(itemVM);
        }
    }

    public void Receive(ThumbnailImageUpdateRequestMessage message)
    {
        foreach (var item in Folders)
        {
            if (item.Path.Equals(message.Value, StringComparison.Ordinal))
            {
                item.ThumbnailChanged();
                item.InitializeAsync(default).FireAndForgetSafe();
                break;
            }
        }
    }

    public ObservableCollection<StorageItemViewModel> Folders { get; }

    private readonly LocalBookmarkRepository _bookmarkManager;
    private readonly AlbamRepository _albamRepository;
    private readonly ThumbnailImageManager _thumbnailManager;
    private readonly IScheduler _scheduler;
    private readonly IMessenger _messenger;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly LastIntractItemRepository _folderLastIntractItemManager;
    private readonly RecentlyAccessRepository _recentlyAccessRepository;
    
    public OpenFolderItemCommand OpenFolderItemCommand { get; }
    public OpenFolderItemSecondaryCommand OpenFolderItemSecondaryCommand { get; }
    public ISecondaryTileManager SecondaryTileManager { get; }
    public SourceChoiceCommand SourceChoiceCommand { get; }
    public OpenImageViewerCommand OpenImageViewerCommand { get; }
    public OpenImageListupCommand OpenImageListupCommand { get; }
    public OpenFolderListupCommand OpenFolderListupCommand { get; }
    public OpenWithExplorerCommand OpenWithExplorerCommand { get; }
    public SecondaryTileAddCommand SecondaryTileAddCommand { get; }
    public SecondaryTileRemoveCommand SecondaryTileRemoveCommand { get; }
   
    bool _foldersInitialized = false;
    public SourceStorageItemsPageViewModel(
        IScheduler scheduler,
        IMessenger messenger,
        FolderListingSettings folderListingSettings,
        LocalBookmarkRepository bookmarkManager,
        AlbamRepository albamRepository,
        ThumbnailImageManager thumbnailManager,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        LastIntractItemRepository folderLastIntractItemManager,
        RecentlyAccessRepository recentlyAccessRepository,
        ISecondaryTileManager secondaryTileManager,
        SourceChoiceCommand sourceChoiceCommand,
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
        Folders = new ObservableCollection<StorageItemViewModel>();
        OpenFolderItemCommand = openFolderItemCommand;
        OpenFolderItemSecondaryCommand = openFolderItemSecondaryCommand;
        SourceChoiceCommand = sourceChoiceCommand;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _folderLastIntractItemManager = folderLastIntractItemManager;
        _recentlyAccessRepository = recentlyAccessRepository;
        SecondaryTileManager = secondaryTileManager;
        OpenImageViewerCommand = openImageViewerCommand;
        OpenImageListupCommand = openImageListupCommand;
        OpenFolderListupCommand = openFolderListupCommand;
        OpenWithExplorerCommand = openWithExplorerCommand;
        SecondaryTileAddCommand = secondaryTileAddCommand;
        SecondaryTileRemoveCommand = secondaryTileRemoveCommand;
        _bookmarkManager = bookmarkManager;
        _albamRepository = albamRepository;
        _thumbnailManager = thumbnailManager;
        _scheduler = scheduler;
        _messenger = messenger;

        _comparison = (x, y) =>
        {
            return _sourceStorageItemsRepository.GetOrderFromPath(x.Path).CompareTo(_sourceStorageItemsRepository.GetOrderFromPath(y.Path));
        };

        _messenger.Register<RemoveSourceStorageItemFromAppMessage>(this);
        _messenger.Register<ThumbnailImageUpdateRequestMessage>(this);
        _messenger.Register<SourceStorageItemsRepository.SourceStorageItemAddedMessage>(this, (r, m) =>
        {
            var args = m.Value;
            RemoveItem(args.StorageItem.Path);

            var storageItemImageSource = new StorageItemImageSource(args.StorageItem);
            if (m.Value.ListType is SourceStorageItemsRepository.TokenListType.FutureAccessList)
            {
                // 追加用ボタンの次に配置するための 1
                Folders.InsertSorted( new StorageItemViewModel(storageItemImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository), _comparison);
            }
            else
            {
                //                    RecentlyItems.Insert(0, new StorageItemViewModel(storageItemImageSource, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
            }
        });

        _messenger.Register<AccessRemovedValueChangedMessage>(this, (r, m) =>
        {
            _scheduler.Schedule(() =>
            {
                RemoveItem(m.Value);
            });
        });
        _messenger.Register<SourceStorageItemIgnoringRequestMessage>(this, (r, m) =>
        {
            _scheduler.Schedule(() =>
            {
                RemoveItem(m.Path);
            });
        });

        _messenger.Register<SourceStorageItemsRepository.SourceStorageItemRemovedMessage>(this, (r, m) =>
        {
            _scheduler.Schedule(() =>
            {
                RemoveItem(m.Value.Path);
            });
        });
    }

    Comparison<StorageItemViewModel> _comparison;
    public override void OnNavigatedFrom(INavigationParameters parameters)
    {
        //_messenger.Unregister<RemoveSourceStorageItemFromAppMessage>(this);
        //_messenger.Unregister<ThumbnailImageUpdateRequestMessage>(this);
        //_messenger.Unregister<SourceStorageItemsRepository.SourceStorageItemAddedMessage>(this);
        //_messenger.Unregister<AccessRemovedValueChangedMessage>(this);
        //_messenger.Unregister<SourceStorageItemIgnoringRequestMessage>(this);
        //_messenger.Unregister<SourceStorageItemsRepository.SourceStorageItemRemovedMessage>(this);
        foreach (var itemVM in Folders)
        {
            itemVM.StopImageLoading();
        }

        if (parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string q))
        {
            var (path, pageName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(q));
            _folderLastIntractItemManager.SetLastIntractItemName(nameof(SourceStorageItemsPageViewModel), path);
        }

        base.OnNavigatedFrom(parameters);
    }

    public override async Task OnNavigatedToAsync(INavigationParameters parameters, CancellationToken ct)
    {
        var mode = parameters.GetNavigationMode();
        if (mode == NavigationMode.Refresh)
        {
            return;
        }

        if (!_foldersInitialized)
        {
            _foldersInitialized = true;
            Folders.Add(new StorageItemViewModel("AddNewFolder".Translate(), Core.Models.StorageItemTypes.AddFolder));
            try
            {
                await foreach (var item in _sourceStorageItemsRepository.GetParsistantItems().WithCancellation(ct))
                {
                    if (item.item == null)
                    {
                        continue;
                    }

                    int order = _sourceStorageItemsRepository.GetOrderFromPath(item.item.Path);
                    
                    var storageItemImageSource = new StorageItemImageSource(item.item);
                    if (storageItemImageSource.ItemTypes == Core.Models.StorageItemTypes.Folder)
                    {
                        Folders.InsertSorted(new StorageItemViewModel(storageItemImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository), _comparison);
                    }
                    else
                    {
                        //throw new NotSupportedException();
                    }
                }
            }
            catch (AggregateException ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
        else
        {
            var lastIntaractItemPath = _folderLastIntractItemManager.GetLastIntractItemName(nameof(SourceStorageItemsPageViewModel));
            foreach (var folderItem in Folders)
            {
                if (folderItem.Path == null) { continue; }

                folderItem.UpdateLastReadPosition();
                if (folderItem.Name == lastIntaractItemPath)
                {
                    folderItem.ThumbnailChanged();
                    folderItem.InitializeAsync(ct).FireAndForgetSafe();
                }
            }

            ct.ThrowIfCancellationRequested();
        }

        // 並べ替えを保存する
        Folders.ToCollectionChanged()
            .ToObservable()
            .Debounce(TimeSpan.FromMilliseconds(50))
            .Subscribe(Folders, (_, s) => 
            {
                _sourceStorageItemsRepository.UpdateOrder(s.Skip(1).Select(x => x.Path));
                Debug.WriteLine("Source Folders order saved!");
                _messenger.Send<SourceStorageItemReorderedMessage>();
            })
            .RegisterTo(ct);

        
        await base.OnNavigatedToAsync(parameters, ct);
    }

    void RemoveItem(string path)
    {
        var existInFolders = Folders.Skip(1).FirstOrDefault(x => x.Path == path);
        if (existInFolders != null)
        {
            existInFolders.Dispose();
            Folders.Remove(existInFolders);
        }
    }
}

public sealed class SourceItemsGroup
{
    public string GroupId { get; set; }
    public ObservableCollection<StorageItemViewModel> Items { get; set; }
}


public sealed class RemoveSourceStorageItemFromAppMessage : ValueChangedMessage<IStorageItemViewModel>
{
    public RemoveSourceStorageItemFromAppMessage(IStorageItemViewModel value) : base(value)
    {
    }
}

public sealed class SourceStorageItemReorderedMessage : ValueChangedMessage<int>
{
    public SourceStorageItemReorderedMessage() : base(0)
    {
    }
}