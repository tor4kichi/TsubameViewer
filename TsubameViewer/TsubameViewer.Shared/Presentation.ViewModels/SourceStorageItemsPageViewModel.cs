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
#if WINDOWS_UWP
using Windows.Storage.AccessCache;
#endif

namespace TsubameViewer.Presentation.ViewModels
{
    // TODO: アクセス履歴対応

    public sealed class SourceStorageItemsPageViewModel : ViewModelBase, IDisposable
    {
        public ObservableCollection<StorageItemViewModel> Folders { get; }
        public ObservableCollection<StorageItemViewModel> Files { get; }

        private readonly BookmarkManager _bookmarkManager;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly IEventAggregator _eventAggregator;
        
        public OpenFolderItemCommand OpenFolderItemCommand { get; }
        public SourceChoiceCommand SourceChoiceCommand { get; }
        public OpenImageViewerCommand OpenImageViewerCommand { get; }
        public OpenFolderListupCommand OpenFolderListupCommand { get; }
        public OpenWithExplorerCommand OpenWithExplorerCommand { get; }

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
            SourceChoiceCommand sourceChoiceCommand,
            OpenFolderItemCommand openFolderItemCommand,
            OpenImageViewerCommand openImageViewerCommand,
            OpenFolderListupCommand openFolderListupCommand,
            OpenWithExplorerCommand openWithExplorerCommand
            )
        {
            Folders = new ObservableCollection<StorageItemViewModel>();
            Files = new ObservableCollection<StorageItemViewModel>();
            OpenFolderItemCommand = openFolderItemCommand;
            SourceChoiceCommand = sourceChoiceCommand;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _eventAggregator = eventAggregator;
            OpenImageViewerCommand = openImageViewerCommand;
            OpenFolderListupCommand = openFolderListupCommand;
            OpenWithExplorerCommand = openWithExplorerCommand;
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
                    Items = Files,
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

                    var existInFiles = Files.FirstOrDefault(x => x.Token == args.Token);
                    if (existInFiles != null)
                    {
                        Files.Remove(existInFiles);
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
                        Files.Insert(0, new StorageItemViewModel(storageItemImageSource, args.Token, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
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

                    var existInFiles = Files.FirstOrDefault(x => x.Token == args.Token);
                    if (existInFiles != null)
                    {
                        Files.Remove(existInFiles);
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

                await foreach (var item in _sourceStorageItemsRepository.GetTemporaryItems())
                {
                    var storageItemImageSource = new StorageItemImageSource(item.item, _thumbnailManager);
                    if (storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.Image
                        || storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.Archive
                        || storageItemImageSource.ItemTypes == Models.Domain.StorageItemTypes.EBook
                        )
                    {
                        Files.Add(new StorageItemViewModel(storageItemImageSource, item.token, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager));
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
            }

            await base.OnNavigatedToAsync(parameters);
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _navigationDisposables?.Dispose();

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
