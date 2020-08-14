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
using TsubameViewer.Models.Domain.Bookmark;
using TsubameViewer.Presentation.Services.UWP;
using Uno.Extensions;
using TsubameViewer.Models.Domain;
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
        private readonly FolderListingSettings _folderListingSettings;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly RecentlyAccessManager _recentlyAccessManager;
        private readonly IEventAggregator _eventAggregator;
        
        public OpenFolderItemCommand OpenFolderItemCommand { get; }
        public SecondaryTileManager SecondaryTileManager { get; }
        public SourceChoiceCommand SourceChoiceCommand { get; }
        public OpenImageViewerCommand OpenImageViewerCommand { get; }
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
            SourceStorageItemsRepository sourceStorageItemsRepository,
            RecentlyAccessManager recentlyAccessManager,
            SecondaryTileManager secondaryTileManager,
            SourceChoiceCommand sourceChoiceCommand,
            OpenFolderItemCommand openFolderItemCommand,
            OpenImageViewerCommand openImageViewerCommand,
            OpenFolderListupCommand openFolderListupCommand,
            OpenWithExplorerCommand openWithExplorerCommand,
            SecondaryTileAddCommand secondaryTileAddCommand,
            SecondaryTileRemoveCommand secondaryTileRemoveCommand
            )
        {
            Folders = new ObservableCollection<StorageItemViewModel>();
            RecentlyItems = new ObservableCollection<StorageItemViewModel>();
            OpenFolderItemCommand = openFolderItemCommand;
            SourceChoiceCommand = sourceChoiceCommand;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _recentlyAccessManager = recentlyAccessManager;
            SecondaryTileManager = secondaryTileManager;
            _eventAggregator = eventAggregator;
            OpenImageViewerCommand = openImageViewerCommand;
            OpenFolderListupCommand = openFolderListupCommand;
            OpenWithExplorerCommand = openWithExplorerCommand;
            SecondaryTileAddCommand = secondaryTileAddCommand;
            SecondaryTileRemoveCommand = secondaryTileRemoveCommand;
            _bookmarkManager = bookmarkManager;
            _thumbnailManager = thumbnailManager;
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
                    var existInFolders = Folders.FirstOrDefault(x => x.Token == args.Token);
                    if (existInFolders != null)
                    {
                        Folders.Remove(existInFolders);
                    }

                    var existInFiles = RecentlyItems.FirstOrDefault(x => x.Token == args.Token);
                    if (existInFiles != null)
                    {
                        RecentlyItems.Remove(existInFiles);
                    }

                    var storageItemImageSource = new StorageItemImageSource(args.StorageItem, _thumbnailManager);
                    if (storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.Folder)
                    {
                        // 追加用ボタンの次に配置するための 1
                        Folders.Insert(1, new StorageItemViewModel(storageItemImageSource, args.Token, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
                    }
                    else if (storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.Image
                        || storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.Archive
                        || storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.EBook
                        )
                    {
                        RecentlyItems.Insert(0, new StorageItemViewModel(storageItemImageSource, args.Token, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
                    }
                })
                .AddTo(_disposables);

            _eventAggregator.GetEvent<SourceStorageItemsRepository.RemovedEvent>()
                .Subscribe(args =>
                {
                    var existInFolders = Folders.FirstOrDefault(x => x.Token == args.Token);
                    if (existInFolders != null)
                    {
                        Folders.Remove(existInFolders);
                    }

                    var existInFiles = RecentlyItems.FirstOrDefault(x => x.Token == args.Token);
                    if (existInFiles != null)
                    {
                        RecentlyItems.Remove(existInFiles);
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
                        Folders.Add(new StorageItemViewModel(storageItemImageSource, item.token, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
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
            }

            async Task<StorageItemViewModel> ToStorageItemViewModel(RecentlyAccessManager.RecentlyAccessEntry entry)
            {
                IStorageItem storageItem = null;
                //_currentFolderItem = 
                var tokenItem = await _sourceStorageItemsRepository.GetItemAsync(entry.Token);
                if (!string.IsNullOrEmpty(entry.SubtractPath))
                {
                    storageItem = await FolderHelper.GetFolderItemFromPath(tokenItem as StorageFolder, entry.SubtractPath);
                }
                else
                {
                    storageItem = tokenItem;
                }

                var storageItemImageSource = new StorageItemImageSource(storageItem, _thumbnailManager);
                return new StorageItemViewModel(storageItemImageSource, entry.Token, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager);
            }

            var recentlyAccessItems = _recentlyAccessManager.GetItemsSortWithRecently(10);
            if (_LastUpdatedRecentlyAccessEnties != null)
            {
                var idSet = _LastUpdatedRecentlyAccessEnties.Select(x => x.Id).ToHashSet();
                {
                    var addNewItems = recentlyAccessItems.TakeWhile(x => !idSet.Contains(x.Id)).Reverse();
                    foreach (var newItem in addNewItems)
                    {
                        var alreadyAdded = RecentlyItems.FirstOrDefault(x => x.Token == newItem.Token && x.Path.EndsWith(newItem.SubtractPath));
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
                    var deletedItems = recentlyAccessItems.OrderByDescending(x => x.Id).TakeWhile(x => !idSet.Contains(x.Id));
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

            base.OnNavigatedFrom(parameters);
        }

        public void Dispose()
        {
            ((IDisposable)_disposables).Dispose();
        }


        private DelegateCommand<StorageItemViewModel> _DeleteStoredFolderCommand;
        public DelegateCommand<StorageItemViewModel> DeleteStoredFolderCommand =>
            _DeleteStoredFolderCommand ??= new DelegateCommand<StorageItemViewModel>(async (itemVM) =>
            {
                _sourceStorageItemsRepository.RemoveFolder(itemVM.Token);
            });
    }

    public sealed class SourceItemsGroup
    {
        public string GroupId { get; set; }
        public ObservableCollection<StorageItemViewModel> Items { get; set; }
    }
}
