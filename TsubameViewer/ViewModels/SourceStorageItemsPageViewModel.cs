﻿using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using Windows.UI.Xaml.Navigation;
using System.Reactive.Concurrency;
using TsubameViewer.Core.Models.Maintenance;
using System.Diagnostics;

namespace TsubameViewer.ViewModels
{
    // TODO: アクセス履歴対応

    public sealed class SourceStorageItemsPageViewModel : NavigationAwareViewModelBase, IDisposable
    {
        public ObservableCollection<StorageItemViewModel> Folders { get; }
        public ObservableCollection<StorageItemViewModel> RecentlyItems { get; }

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

        CompositeDisposable _disposables = new CompositeDisposable();
        CompositeDisposable _navigationDisposables;

        CancellationTokenSource _navigationCts;
        public SourceItemsGroup[] Groups { get; }

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
            RecentlyItems = new ObservableCollection<StorageItemViewModel>();
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

            RegisterSourceStorageItemChange();
        }

        

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            var mode = parameters.GetNavigationMode();
            if (mode == NavigationMode.Refresh)
            {
                return;
            }

            _navigationDisposables = new CompositeDisposable();
            _navigationCts = new CancellationTokenSource();

            var ct = _navigationCts.Token;

            try
            {
                if (!_foldersInitialized)
                {
                    _foldersInitialized = true;

                    Folders.Add(new StorageItemViewModel("AddNewFolder".Translate(), Core.Models.StorageItemTypes.AddFolder));
                    try
                    {
                        await foreach (var item in _sourceStorageItemsRepository.GetParsistantItems())
                        {
                            if (item.item == null)
                            {
                                continue;
                            }

                            var storageItemImageSource = new StorageItemImageSource(item.item);
                            if (storageItemImageSource.ItemTypes == Core.Models.StorageItemTypes.Folder)
                            {
                                Folders.Add(new StorageItemViewModel(storageItemImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository));
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
                            folderItem.InitializeAsync(ct);
                        }
                    }

                    ct.ThrowIfCancellationRequested();
                }

                async Task<StorageItemViewModel> ToStorageItemViewModel((string Path, DateTimeOffset LastAccessTime) entry)
                {
                    var storageItem = await _sourceStorageItemsRepository.TryGetStorageItemFromPath(entry.Path);
                    if (storageItem == null) { throw new FileNotFoundException(entry.Path); }

                    var storageItemImageSource = new StorageItemImageSource(storageItem);
                    return new StorageItemViewModel(storageItemImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository);
                }

                var recentlyAccessItems = _recentlyAccessRepository.GetItemsSortWithRecently(15);
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

                    _LastUpdatedRecentlyAccessEnties = recentlyAccessItems;
                }
                else
                {
                    var lastIntaractItemPath = _folderLastIntractItemManager.GetLastIntractItemName(nameof(SourceStorageItemsPageViewModel));
                    foreach (var item in RecentlyItems)
                    {
                        if (item.Name == lastIntaractItemPath)
                        {
                            item.ThumbnailChanged();
                            item.InitializeAsync(ct);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }

            await base.OnNavigatedToAsync(parameters);
        }




        List<(string Path, DateTimeOffset LastAccessTime)> _LastUpdatedRecentlyAccessEnties;

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _navigationCts.Cancel();
            _navigationCts.Dispose();
            _navigationCts = null;

            foreach (var itemVM in Enumerable.Concat(Folders, RecentlyItems))
            {
                itemVM.StopImageLoading();
            }

            _navigationDisposables?.Dispose();

            if (parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string q))
            {
                var (path, pageName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(q));
                _folderLastIntractItemManager.SetLastIntractItemName(nameof(SourceStorageItemsPageViewModel), path);
            }

            base.OnNavigatedFrom(parameters);
        }

        public void Dispose()
        {
            ((IDisposable)_disposables).Dispose();
            UnregisterSourceStorageItemChange();
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
                RemoveItem(args.StorageItem.Path);

                var storageItemImageSource = new StorageItemImageSource(args.StorageItem);
                if (m.Value.ListType is SourceStorageItemsRepository.TokenListType.FutureAccessList)
                {
                    // 追加用ボタンの次に配置するための 1
                    Folders.Insert(1, new StorageItemViewModel(storageItemImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository));
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

        void RemoveItem(string path)
        {
            var existInFolders = Folders.Skip(1).FirstOrDefault(x => x.Path == path);
            if (existInFolders != null)
            {
                existInFolders.Dispose();
                Folders.Remove(existInFolders);
            }

            var existInFiles = RecentlyItems.Where(x => x.Path == path).ToList();
            foreach (var item in existInFiles)
            {
                item.Dispose();
                RecentlyItems.Remove(item);
            }
        }
    }

    public sealed class SourceItemsGroup
    {
        public string GroupId { get; set; }
        public ObservableCollection<StorageItemViewModel> Items { get; set; }
    }
}
