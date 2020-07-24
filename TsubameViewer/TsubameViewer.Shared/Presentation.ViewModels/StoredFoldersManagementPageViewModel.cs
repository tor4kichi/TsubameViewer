using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.SourceManagement;
using TsubameViewer.Models.UseCase.PageNavigation;
using TsubameViewer.Models.UseCase.PageNavigation.Commands;
using TsubameViewer.Models.UseCase.SourceManagement.Commands;
using Uno.Disposables;
using Windows.Storage;
#if WINDOWS_UWP
using Windows.Storage.AccessCache;
#endif

namespace TsubameViewer.Presentation.ViewModels
{
    // TODO: アクセス履歴対応

    public sealed class StoredFoldersManagementPageViewModel : ViewModelBase
    {
        public ObservableCollection<StorageItemViewModel> Folders { get; }

        private readonly StoredFoldersRepository _storedFoldersRepository;
        private readonly IEventAggregator _eventAggregator;

        public OpenFolderItemCommand OpenFolderItemCommand { get; }
        public SourceChoiceCommand SourceChoiceCommand { get; }

        CompositeDisposable _navigationDisposables;

        bool _foldersInitialized = false;
        public StoredFoldersManagementPageViewModel(
            OpenFolderItemCommand openFolderItemCommand,
            SourceChoiceCommand sourceChoiceCommand,
            StoredFoldersRepository storedFoldersRepository,
            IEventAggregator eventAggregator
            )
        {
            Folders = new ObservableCollection<StorageItemViewModel>();
            OpenFolderItemCommand = openFolderItemCommand;
            SourceChoiceCommand = sourceChoiceCommand;
            _storedFoldersRepository = storedFoldersRepository;
            _eventAggregator = eventAggregator;
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationDisposables = new CompositeDisposable();
            if (!_foldersInitialized)
            {
                _foldersInitialized = true;

                await foreach (var item in _storedFoldersRepository.GetStoredFolderItems())
                {
                    Folders.Add(new StorageItemViewModel(item.item, item.token));
                }
            }

            _eventAggregator.GetEvent<StoredFoldersRepository.AddedEvent>()
                .Subscribe(args => 
                {
                    Folders.Add(new StorageItemViewModel(args.StorageItem, args.Token));
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
