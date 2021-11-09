using I18NPortable;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views.SourceFolders.Commands;
using Uno.Disposables;
using Uno.Extensions.Specialized;
using Windows.Storage;
using TsubameViewer.Models.Domain.ReadingFeature;
using TsubameViewer.Presentation.Services.UWP;
using Uno.Extensions;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.RestoreNavigation;
#if WINDOWS_UWP
using Windows.Storage.AccessCache;
#endif

namespace TsubameViewer.Presentation.ViewModels
{
    // TODO: アクセス履歴対応

    public sealed class SourceStorageItemsPageViewModel : ViewModelBase, IDisposable
    {
        public ObservableCollection<StorageItemViewModel> Folders { get; }
        public ObservableCollection<StorageItemViewModel> RecentlyItems { get; }

        private readonly BookmarkManager _bookmarkManager;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly PathReferenceCountManager _PathReferenceCountManager;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly FolderLastIntractItemManager _folderLastIntractItemManager;
        private readonly RecentlyAccessManager _recentlyAccessManager;
        private readonly IEventAggregator _eventAggregator;
        
        public OpenFolderItemCommand OpenFolderItemCommand { get; }
        public OpenFolderItemSecondaryCommand OpenFolderItemSecondaryCommand { get; }
        public SecondaryTileManager SecondaryTileManager { get; }
        public SourceChoiceCommand SourceChoiceCommand { get; }
        public OpenImageViewerCommand OpenImageViewerCommand { get; }
        public OpenImageListupCommand OpenImageListupCommand { get; }
        public OpenFolderListupCommand OpenFolderListupCommand { get; }
        public OpenWithExplorerCommand OpenWithExplorerCommand { get; }
        public SecondaryTileAddCommand SecondaryTileAddCommand { get; }
        public SecondaryTileRemoveCommand SecondaryTileRemoveCommand { get; }

        CompositeDisposable _disposables = new CompositeDisposable();
        CompositeDisposable _navigationDisposables;

        public SourceItemsGroup[] Groups { get; }

        bool _foldersInitialized = false;
        public SourceStorageItemsPageViewModel(
            IEventAggregator eventAggregator,
            FolderListingSettings folderListingSettings,
            BookmarkManager bookmarkManager,
            ThumbnailManager thumbnailManager,
            PathReferenceCountManager PathReferenceCountManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            FolderLastIntractItemManager folderLastIntractItemManager,
            RecentlyAccessManager recentlyAccessManager,
            SecondaryTileManager secondaryTileManager,
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
            RecentlyItems = new ObservableCollection<StorageItemViewModel>();
            OpenFolderItemCommand = openFolderItemCommand;
            OpenFolderItemSecondaryCommand = openFolderItemSecondaryCommand;
            SourceChoiceCommand = sourceChoiceCommand;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _folderLastIntractItemManager = folderLastIntractItemManager;
            _recentlyAccessManager = recentlyAccessManager;
            SecondaryTileManager = secondaryTileManager;
            _eventAggregator = eventAggregator;
            OpenImageViewerCommand = openImageViewerCommand;
            OpenImageListupCommand = openImageListupCommand;
            OpenFolderListupCommand = openFolderListupCommand;
            OpenWithExplorerCommand = openWithExplorerCommand;
            SecondaryTileAddCommand = secondaryTileAddCommand;
            SecondaryTileRemoveCommand = secondaryTileRemoveCommand;
            _bookmarkManager = bookmarkManager;
            _thumbnailManager = thumbnailManager;
            _PathReferenceCountManager = PathReferenceCountManager;
            _folderListingSettings = folderListingSettings;

            Groups = new[]
            {
                new SourceItemsGroup
                {
                    GroupId = "Folders",
                    Items = Folders,
                },
                new SourceItemsGroup
                {
                    GroupId = "RecentlyUsedFiles",
                    Items = RecentlyItems,
                },
            };

            _eventAggregator.GetEvent<SourceStorageItemsRepository.AddedEvent>()
                .Subscribe(args =>
                {

                    var existInFolders = Folders.FirstOrDefault(x => x.Token.TokenString == args.Token);
                    if (existInFolders != null)
                    {
                        Folders.Remove(existInFolders);
                    }

                    var existInFiles = RecentlyItems.FirstOrDefault(x => x.Token.TokenString == args.Token);
                    if (existInFiles != null)
                    {
                        RecentlyItems.Remove(existInFiles);
                    }

                    var storageItemImageSource = new StorageItemImageSource(args.StorageItem, _thumbnailManager);
                    if (storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.Folder)
                    {
                        // 追加用ボタンの次に配置するための 1
                        Folders.Insert(1, new StorageItemViewModel(storageItemImageSource, new StorageItemToken(args.StorageItem.Path, args.Token), _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
                    }
                    else if (storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.Image
                        || storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.Archive
                        || storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.EBook
                        )
                    {
                        RecentlyItems.Insert(0, new StorageItemViewModel(storageItemImageSource, new StorageItemToken(args.StorageItem.Path, args.Token), _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
                    }
                })
                .AddTo(_disposables);

            _eventAggregator.GetEvent<SourceStorageItemsRepository.RemovedEvent>()
                .Subscribe(args =>
                {
                    var existInFolders = Folders.FirstOrDefault(x => x.Token.TokenString == args.Token);
                    if (existInFolders != null)
                    {
                        Folders.Remove(existInFolders);
                    }

                    var existInFiles = RecentlyItems.Where(x => x.Token.TokenString == args.Token).ToList();
                    foreach (var item in existInFiles)
                    {
                        RecentlyItems.Remove(item);
                    }
                })
                .AddTo(_disposables);
        }

        

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationDisposables = new CompositeDisposable();
            if (!_foldersInitialized)
            {
                _foldersInitialized = true;

                Folders.Add(new StorageItemViewModel(_sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager) { });
                await foreach (var item in _sourceStorageItemsRepository.GetParsistantItems())
                {
                    var storageItemImageSource = new StorageItemImageSource(item.item, _thumbnailManager);
                    if (storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.Folder)
                    {
                        Folders.Add(new StorageItemViewModel(storageItemImageSource, new StorageItemToken(item.item.Path, item.token), _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
                    }
                    else
                    {
                        //throw new NotSupportedException();
                    }
                }
            }
            else
            {
                Folders.ForEach(x => x.UpdateLastReadPosition());

                var lastIntaractItemPath = _folderLastIntractItemManager.GetLastIntractItemName(nameof(SourceStorageItemsPageViewModel));
                Folders.Where(x => x.Name == lastIntaractItemPath).ForEach(x => 
                {
                    x.ThumbnailChanged();
                    x.Initialize();
                });
            }

            async Task<StorageItemViewModel> ToStorageItemViewModel(RecentlyAccessManager.RecentlyAccessEntry entry)
            {
                var token = _PathReferenceCountManager.GetToken(entry.Path);
                var storageItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(token, entry.Path);
                var storageItemImageSource = new StorageItemImageSource(storageItem, _thumbnailManager);
                return new StorageItemViewModel(storageItemImageSource, new StorageItemToken(storageItem.Path, token), _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager);
            }

            var recentlyAccessItems = _recentlyAccessManager.GetItemsSortWithRecently(10);
            if (_LastUpdatedRecentlyAccessEnties != null)
            {
                var idSet = _LastUpdatedRecentlyAccessEnties.Select(x => x.Path).ToHashSet();
                {
                    var addNewItems = recentlyAccessItems.TakeWhile(x => !idSet.Contains(x.Path)).Reverse();
                    foreach (var newItem in addNewItems)
                    {
                        var alreadyAdded = RecentlyItems.FirstOrDefault(x => x.Path == newItem.Path);
                        if (alreadyAdded != null)
                        {
                            RecentlyItems.Remove(alreadyAdded);
                            RecentlyItems.Insert(0, alreadyAdded);
                        }
                        else
                        {
                            RecentlyItems.Insert(0, await ToStorageItemViewModel(newItem));
                        }
                    }
                }
                {
                    var deletedItems = recentlyAccessItems.OrderByDescending(x => x.LastAccess).TakeWhile(x => !idSet.Contains(x.Path));
                    foreach (var i in Enumerable.Range(0, deletedItems.Count()))
                    {                        
                        if (RecentlyItems.Count == 0) { break; }
                        if (RecentlyItems.Count <= 10) { break; }

                        RecentlyItems.RemoveAt(RecentlyItems.Count - 1);
                    }
                }
            }
            else
            {
                foreach (var item in recentlyAccessItems)
                {
                    try
                    {
                        RecentlyItems.Add(await ToStorageItemViewModel(item));
                    }
                    catch
                    {
                        _recentlyAccessManager.Delete(item);
                    }
                }
            }

            _LastUpdatedRecentlyAccessEnties = recentlyAccessItems;

            await base.OnNavigatedToAsync(parameters);
        }

        List<RecentlyAccessManager.RecentlyAccessEntry> _LastUpdatedRecentlyAccessEnties;

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _navigationDisposables?.Dispose();

            if (parameters.TryGetValue(PageNavigationConstants.Path, out string path))
            {
                _folderLastIntractItemManager.SetLastIntractItemName(nameof(SourceStorageItemsPageViewModel), Uri.UnescapeDataString(path));
            }

            base.OnNavigatedFrom(parameters);
        }

        public void Dispose()
        {
            ((IDisposable)_disposables).Dispose();
        }
    }

    public sealed class SourceItemsGroup
    {
        public string GroupId { get; set; }
        public ObservableCollection<StorageItemViewModel> Items { get; set; }
    }
}
