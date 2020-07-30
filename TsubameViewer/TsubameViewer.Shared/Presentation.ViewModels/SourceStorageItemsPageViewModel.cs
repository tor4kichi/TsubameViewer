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
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views.SourceFolders.Commands;
using Uno.Disposables;
using Windows.Storage;
#if WINDOWS_UWP
using Windows.Storage.AccessCache;
#endif

namespace TsubameViewer.Presentation.ViewModels
{
    // TODO: アクセス履歴対応

    public sealed class SourceStorageItemsPageViewModel : ViewModelBase
    {
        public ObservableCollection<StorageItemViewModel> Folders { get; }
        public ObservableCollection<StorageItemViewModel> Files { get; }

        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly IEventAggregator _eventAggregator;
        
        public OpenFolderItemCommand OpenFolderItemCommand { get; }
        public SourceChoiceCommand SourceChoiceCommand { get; }

        CompositeDisposable _navigationDisposables;

        public SourceItemsGroup[] Groups { get; }

        bool _foldersInitialized = false;
        public SourceStorageItemsPageViewModel(
            ThumbnailManager thumbnailManager,
            FolderListingSettings folderListingSettings,
            OpenFolderItemCommand openFolderItemCommand,
            SourceChoiceCommand sourceChoiceCommand,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            IEventAggregator eventAggregator            
            )
        {
            Folders = new ObservableCollection<StorageItemViewModel>();
            Files = new ObservableCollection<StorageItemViewModel>();
            OpenFolderItemCommand = openFolderItemCommand;
            SourceChoiceCommand = sourceChoiceCommand;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _eventAggregator = eventAggregator;
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
                    GroupId = "Files",
                    Items = Files,
                },
            };
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationDisposables = new CompositeDisposable();
            if (!_foldersInitialized)
            {
                _foldersInitialized = true;

                Folders.Add(new StorageItemViewModel(_sourceStorageItemsRepository, _thumbnailManager, _folderListingSettings) { });
                await foreach (var item in _sourceStorageItemsRepository.GetSourceFolders())
                {
                    if (item.item is StorageFolder)
                    {
                        Folders.Add(new StorageItemViewModel(item.item, item.token, _sourceStorageItemsRepository, _thumbnailManager, _folderListingSettings));
                    }
                    else if (item.item is StorageFile)
                    {
                        Files.Add(new StorageItemViewModel(item.item, item.token, _sourceStorageItemsRepository, _thumbnailManager, _folderListingSettings));
                    }                        
                }
            }

            _eventAggregator.GetEvent<SourceStorageItemsRepository.AddedEvent>()
                .Subscribe(args => 
                {
                    if (args.StorageItem is StorageFolder)
                    {
                        Folders.Insert(0, new StorageItemViewModel(args.StorageItem, args.Token, _sourceStorageItemsRepository, _thumbnailManager, _folderListingSettings));
                    }
                    else if (args.StorageItem is StorageFile)
                    {
                        Files.Insert(0, new StorageItemViewModel(args.StorageItem, args.Token, _sourceStorageItemsRepository, _thumbnailManager, _folderListingSettings));
                    }
                })
                .AddTo(_navigationDisposables);

            await base.OnNavigatedToAsync(parameters);
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _navigationDisposables?.Dispose();

            base.OnNavigatedFrom(parameters);
        }
    }

    public sealed class SourceItemsGroup
    {
        public string GroupId { get; set; }
        public ObservableCollection<StorageItemViewModel> Items { get; set; }
    }
}
