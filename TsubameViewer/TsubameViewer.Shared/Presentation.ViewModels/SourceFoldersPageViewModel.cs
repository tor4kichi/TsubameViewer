﻿using I18NPortable;
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

    public sealed class SourceFoldersPageViewModel : ViewModelBase
    {
        public ObservableCollection<StorageItemViewModel> Folders { get; }

        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly SourceFoldersRepository _SourceFoldersRepository;
        private readonly IEventAggregator _eventAggregator;
        
        public OpenFolderItemCommand OpenFolderItemCommand { get; }
        public SourceChoiceCommand SourceChoiceCommand { get; }

        CompositeDisposable _navigationDisposables;

        bool _foldersInitialized = false;
        public SourceFoldersPageViewModel(
            ThumbnailManager thumbnailManager,
            FolderListingSettings folderListingSettings,
            OpenFolderItemCommand openFolderItemCommand,
            SourceChoiceCommand sourceChoiceCommand,
            SourceFoldersRepository SourceFoldersRepository,
            IEventAggregator eventAggregator            
            )
        {
            Folders = new ObservableCollection<StorageItemViewModel>();
            OpenFolderItemCommand = openFolderItemCommand;
            SourceChoiceCommand = sourceChoiceCommand;
            _SourceFoldersRepository = SourceFoldersRepository;
            _eventAggregator = eventAggregator;
            _thumbnailManager = thumbnailManager;
            _folderListingSettings = folderListingSettings;
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationDisposables = new CompositeDisposable();
            if (!_foldersInitialized)
            {
                _foldersInitialized = true;

                Folders.Add(new StorageItemViewModel(_thumbnailManager, _folderListingSettings) { });
                await foreach (var item in _SourceFoldersRepository.GetSourceFolders())
                {
                    Folders.Add(new StorageItemViewModel(item.item, item.token, _thumbnailManager, _folderListingSettings));
                }
            }

            _eventAggregator.GetEvent<SourceFoldersRepository.AddedEvent>()
                .Subscribe(args => 
                {
                    Folders.Add(new StorageItemViewModel(args.StorageItem, args.Token, _thumbnailManager, _folderListingSettings));
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
}