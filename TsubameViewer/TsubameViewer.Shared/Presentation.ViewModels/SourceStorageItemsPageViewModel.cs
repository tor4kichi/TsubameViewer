using I18NPortable;
using Prism.Commands;
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
using Microsoft.Toolkit.Mvvm.Messaging;
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
        private readonly IMessenger _messenger;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly FolderLastIntractItemManager _folderLastIntractItemManager;
        private readonly RecentlyAccessManager _recentlyAccessManager;
        
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
            IMessenger messenger,
            FolderListingSettings folderListingSettings,
            BookmarkManager bookmarkManager,
            ThumbnailManager thumbnailManager,
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
            OpenImageViewerCommand = openImageViewerCommand;
            OpenImageListupCommand = openImageListupCommand;
            OpenFolderListupCommand = openFolderListupCommand;
            OpenWithExplorerCommand = openWithExplorerCommand;
            SecondaryTileAddCommand = secondaryTileAddCommand;
            SecondaryTileRemoveCommand = secondaryTileRemoveCommand;
            _bookmarkManager = bookmarkManager;
            _thumbnailManager = thumbnailManager;
            _messenger = messenger;
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
                        Folders.Add(new StorageItemViewModel(storageItemImageSource, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
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
                var storageItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(entry.Path);
                var storageItemImageSource = new StorageItemImageSource(storageItem, _thumbnailManager);
                return new StorageItemViewModel(storageItemImageSource, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager);
            }

            var recentlyAccessItems = _recentlyAccessManager.GetItemsSortWithRecently(15);
            if (recentlyAccessItems.Select(x => x.Path).SequenceEqual(RecentlyItems.Select(x => x.Path)) is false)
            {
                RecentlyItems.ForEach((StorageItemViewModel x) => x.TryDispose());
                RecentlyItems.Clear();
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

                _LastUpdatedRecentlyAccessEnties = recentlyAccessItems;
            }

            await base.OnNavigatedToAsync(parameters);

            RegisterSourceStorageItemChange();
        }




        List<RecentlyAccessManager.RecentlyAccessEntry> _LastUpdatedRecentlyAccessEnties;

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            UnregisterSourceStorageItemChange();

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

        void UnregisterSourceStorageItemChange()
        {
            _messenger.UnregisterAll(this);
        }


        void RegisterSourceStorageItemChange()
        {
            _messenger.Register<SourceStorageItemsRepository.SourceStorageItemAddedMessage>(this, (r, m) =>
            {
                var args = m.Value;
                var existInFolders = Folders.Skip(1).FirstOrDefault(x => x.Path == args.StorageItem.Path);
                if (existInFolders != null)
                {
                    Folders.Remove(existInFolders);
                }

                var existInFiles = RecentlyItems.FirstOrDefault(x => x.Path == args.StorageItem.Path);
                if (existInFiles != null)
                {
                    RecentlyItems.Remove(existInFiles);
                }

                var storageItemImageSource = new StorageItemImageSource(args.StorageItem, _thumbnailManager);
                if (storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.Folder)
                {
                    // 追加用ボタンの次に配置するための 1
                    Folders.Insert(1, new StorageItemViewModel(storageItemImageSource, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
                }
                else if (storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.Image
                    || storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.Archive
                    || storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.EBook
                    )
                {
                    RecentlyItems.Insert(0, new StorageItemViewModel(storageItemImageSource, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
                }
            });

            _messenger.Register<SourceStorageItemsRepository.SourceStorageItemRemovedMessage>(this, (r, m) =>
            {
                var args = m.Value;
                var existInFolders = Folders.Skip(1).FirstOrDefault(x => x.Path == args.Path);
                if (existInFolders != null)
                {
                    Folders.Remove(existInFolders);
                }

                var existInFiles = RecentlyItems.Where(x => x.Path == args.Path).ToList();
                foreach (var item in existInFiles)
                {
                    RecentlyItems.Remove(item);
                }
            });
        }
    }

    public sealed class SourceItemsGroup
    {
        public string GroupId { get; set; }
        public ObservableCollection<StorageItemViewModel> Items { get; set; }
    }
}
