using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI;
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
using System.Windows.Input;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.Maintenance;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


#nullable enable
namespace TsubameViewer.ViewModels;

public sealed partial class SourceStorageItemsPageViewModel 
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
            if (item.Path?.Equals(message.Value, StringComparison.Ordinal) ?? false)
            {
                item.ThumbnailChanged();
                item.InitializeAsync(default).FireAndForgetSafe();
                break;
            }
        }
    }

    public AdvancedCollectionView ItemsView { get; }
    public ObservableCollection<StorageItemViewModel> Folders { get; }

    readonly LocalBookmarkRepository _bookmarkManager;
    readonly AlbamRepository _albamRepository;
    readonly ThumbnailImageManager _thumbnailManager;
    readonly IScheduler _scheduler;
    readonly IMessenger _messenger;
    readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    readonly LastIntractItemRepository _folderLastIntractItemManager;
    private readonly SourceChoiceCommand _sourceChoiceCommand;
    private readonly AlbamCreateCommand _albamCreateCommand;
    private readonly FolderContainerTypeManager _folderContainerTypeManager;
    private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    [ObservableProperty]
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
        SourceChoiceCommand sourceChoiceCommand,
        AlbamCreateCommand albamCreateCommand,
        FolderContainerTypeManager folderContainerTypeManager,
        DisplaySettingsByPathRepository displaySettingsByPathRepository
        )
    {
        Folders = new ObservableCollection<StorageItemViewModel>();
        ItemsView = new (Folders);
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _folderLastIntractItemManager = folderLastIntractItemManager;        
        _sourceChoiceCommand = sourceChoiceCommand;
        _albamCreateCommand = albamCreateCommand;
        _folderContainerTypeManager = folderContainerTypeManager;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
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

        _messenger.Register<StroageItemAccessRemovedMessage>(this, (r, m) =>
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

        if (!FoldersInitialized)
        {
            _foldersInitialized = true;
            Folders.Add(new StorageItemViewModel("AddNewFolder".Translate(), Core.Models.StorageItemTypes.AddFolder));            
            try
            {
                var items = (await _sourceStorageItemsRepository.GetParsistantItems(ct))
                    .Select(x => new StorageItemViewModel(new StorageItemImageSource(x.item), _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository))
                    .ToList();
                items.Sort(_comparison);
                foreach (var item in items)
                {
                    if (item.Type == Core.Models.StorageItemTypes.Folder)
                    {
                        Folders.Add(item);
                    }
                }
            }
            catch (AggregateException ex)
            {
                Debug.WriteLine(ex.ToString());
            }

            OnPropertyChanged(nameof(FoldersInitialized));
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

    [RelayCommand]
    async Task OpenSourceAsync(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;

            if (itemVM.Type == Core.Models.StorageItemTypes.AddFolder)
            {
                ((ICommand)_sourceChoiceCommand).Execute(null);
                return;
            }
            else if (itemVM.Type == Core.Models.StorageItemTypes.AddAlbam)
            {
                ((ICommand)_albamCreateCommand).Execute(null);
                return;
            }
        }

        if (parameter is IImageSource imageSource)
        {
            var folder = (StorageFolder)((StorageItemImageSource)imageSource).StorageItem;
            var parentSettings = _displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(folder.Path);
            var imagesFolderOpenMode = parentSettings?.ChildImagesFolderOpenMode ?? DisplaySettingsByPathRepository.DefaultChildImagesFolderOpenMode;
            var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
            var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
        }
    }
}

public sealed record SourceItemsGroup(string GroupId, ObservableCollection<StorageItemViewModel> Items);

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