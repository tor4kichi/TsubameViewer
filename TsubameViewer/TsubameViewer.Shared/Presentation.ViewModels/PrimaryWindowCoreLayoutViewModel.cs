using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.Helpers;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.RestoreNavigation;
using TsubameViewer.Models.Domain.Search;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Models.UseCase;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views.SourceFolders.Commands;
using Unity.Attributes;
using Uno.Extensions;
using Uno.Threading;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;
using Xamarin.Essentials;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class PrimaryWindowCoreLayoutViewModel : BindableBase
    {
        public INavigationService NavigationService => _navigationServiceLazy.Value;
        private readonly Lazy<INavigationService> _navigationServiceLazy;
        private readonly IScheduler _scheduler;
        private readonly IMessenger _messenger;
        private readonly PathReferenceCountManager _PathReferenceCountManager;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;
        private readonly StorageItemSearchManager _storageItemSearchManager;

        public List<object> MenuItems { get;  }

        CompositeDisposable _disposables = new CompositeDisposable();

        public PrimaryWindowCoreLayoutViewModel(
            [Dependency("PrimaryWindowNavigationService")] Lazy<INavigationService> navigationServiceLazy,
            IEventAggregator eventAggregator,
            IScheduler scheduler,
            IMessenger messenger,
            ApplicationSettings applicationSettings,
            RestoreNavigationManager restoreNavigationManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            PathReferenceCountManager PathReferenceCountManager,
            FolderContainerTypeManager folderContainerTypeManager,
            StorageItemSearchManager storageItemSearchManager,
            SourceChoiceCommand sourceChoiceCommand,
            RefreshNavigationCommand refreshNavigationCommand,
            OpenPageCommand openPageCommand
            )
        {
            MenuItems = new List<object>
            {
                new MenuItemViewModel() { PageType = nameof(Views.SourceStorageItemsPage) },
                //new MenuItemViewModel() { PageType = nameof(Views.CollectionPage) },
            };
            _navigationServiceLazy = navigationServiceLazy;
            EventAggregator = eventAggregator;
            _scheduler = scheduler;
            _messenger = messenger;
            ApplicationSettings = applicationSettings;
            RestoreNavigationManager = restoreNavigationManager;
            SourceStorageItemsRepository = sourceStorageItemsRepository;
            _PathReferenceCountManager = PathReferenceCountManager;
            _folderContainerTypeManager = folderContainerTypeManager;
            _storageItemSearchManager = storageItemSearchManager;
            SourceChoiceCommand = sourceChoiceCommand;
            SourceChoiceCommand.OpenAfterChoice = true;
            RefreshNavigationCommand = refreshNavigationCommand;
            OpenPageCommand = openPageCommand;


            UpdateAutoSuggestCommand = new ReactiveCommand<string>();

            UpdateAutoSuggestCommand
                .Throttle(TimeSpan.FromSeconds(0.250), _scheduler)
                .Subscribe(ExecuteUpdateAutoSuggestCommand)
                .AddTo(_disposables);

            EventAggregator.GetEvent<PathReferenceCountUpdateWhenSourceManagementChanged.SearchIndexUpdateProgressEvent>()
                .Subscribe(args => 
                {
                    _autoSuggestBoxSearchIndexGroup.SearchIndexUpdateProgressCount = args.ProcessedCount;
                    _autoSuggestBoxSearchIndexGroup.SearchIndexUpdateTotalCount = args.TotalCount;

                    Debug.WriteLine($"[SearchIndexUpdate] progress: {args.ProcessedCount}/{args.TotalCount} ");
                }
                , ThreadOption.UIThread
                , keepSubscriberReferenceAlive: true
                )
                .AddTo(_disposables);

            AutoSuggestBoxItems = new[]
            {
                _AutoSuggestItemsGroup,
                _autoSuggestBoxSearchIndexGroup
            };
        }

        AutoSuggestBoxGroupBase _AutoSuggestItemsGroup = new AutoSuggestBoxGroupBase();
        AutoSuggestBoxSearchIndexGroup _autoSuggestBoxSearchIndexGroup = new AutoSuggestBoxSearchIndexGroup();

        public object[] AutoSuggestBoxItems { get; }

        private bool _IsDisplayMenu = true;
        public bool IsDisplayMenu
        {
            get { return _IsDisplayMenu; }
            set { SetProperty(ref _IsDisplayMenu, value); }
        }


        DelegateCommand<object> _OpenMenuItemCommand;
        public DelegateCommand<object> OpenMenuItemCommand =>
            _OpenMenuItemCommand ??= new DelegateCommand<object>(item => 
            {
                if (item is MenuItemViewModel menuItem)
                {
                    NavigationService.NavigateAsync(menuItem.PageType);
                }
            });

        public IEventAggregator EventAggregator { get; }
        public ApplicationSettings ApplicationSettings { get; }
        public RestoreNavigationManager RestoreNavigationManager { get; }
        public SourceStorageItemsRepository SourceStorageItemsRepository { get; }
        public SourceChoiceCommand SourceChoiceCommand { get; }
        public RefreshNavigationCommand RefreshNavigationCommand { get; }
        public OpenPageCommand OpenPageCommand { get; }


        public RelayCommand SendFeedbackWithMashmallowCommand { get; } = 
            new RelayCommand(async () => 
            {
                var assem = App.Current.GetType().Assembly;
                StringBuilder sb = new StringBuilder();
                sb.Append(SystemInformation.Instance.ApplicationName)
                    .Append(" v").Append(SystemInformation.Instance.ApplicationVersion.ToFormattedString())
                    .AppendLine();
                sb.Append(SystemInformation.Instance.OperatingSystem).Append(" ").Append(SystemInformation.Instance.OperatingSystemArchitecture)
                    .Append("(").Append(SystemInformation.Instance.OperatingSystemVersion).Append(")")
                    .Append(" ").Append(DeviceInfo.Idiom)
                    ;
                await Clipboard.SetTextAsync(sb.ToString());
                await Launcher.OpenAsync("https://marshmallow-qa.com/tor4kichi");
            });


        #region Search

        public ReactiveCommand<string> UpdateAutoSuggestCommand { get; }

        FastAsyncLock _suggestUpdateLock = new FastAsyncLock();
        async void ExecuteUpdateAutoSuggestCommand(string parameter)
        {
            using (await _suggestUpdateLock.LockAsync(default))
            {
                _AutoSuggestItemsGroup.Items.Clear();
                if (string.IsNullOrWhiteSpace(parameter)) { return; }

                var result = await Task.Run(() => _storageItemSearchManager.SearchAsync(parameter.Trim(), 0, 3));
                _AutoSuggestItemsGroup.Items.AddRange(result.Entries);
            }
        }

        private DelegateCommand<StorageItemSearchEntry> _SuggestChosenCommand;
        public DelegateCommand<StorageItemSearchEntry> SuggestChosenCommand =>
            _SuggestChosenCommand ?? (_SuggestChosenCommand = new DelegateCommand<StorageItemSearchEntry>(ExecuteSuggestChosenCommand));

        async void ExecuteSuggestChosenCommand(StorageItemSearchEntry entry)
        {
            var path = entry.Path;

            var parameters = new NavigationParameters();

            var token = _PathReferenceCountManager.GetToken(entry.Path);
            var storageItem = await SourceStorageItemsRepository.GetStorageItemFromPath(token, entry.Path);

            parameters.Add(PageNavigationConstants.Path, entry.Path);

            if (storageItem is StorageFolder itemFolder)
            {
                var containerType = await _messenger.WorkWithBusyWallAsync(async ct => await _folderContainerTypeManager.GetFolderContainerTypeWithCacheAsync(itemFolder, ct), CancellationToken.None);
                if (containerType == FolderContainerType.OnlyImages)
                {
                    await NavigationService.NavigateAsync(nameof(Presentation.Views.ImageViewerPage), parameters, new SuppressNavigationTransitionInfo());
                    return;
                }
                else
                {
                    await NavigationService.NavigateAsync(nameof(Presentation.Views.FolderListupPage), parameters, new DrillInNavigationTransitionInfo());
                    return;
                }
            }
            else if (storageItem is StorageFile file)
            {
                // ファイル
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType)
                    || SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType)
                    )
                {
                    await NavigationService.NavigateAsync(nameof(Presentation.Views.ImageViewerPage), parameters, new SuppressNavigationTransitionInfo());
                }
                else if (SupportedFileTypesHelper.IsSupportedEBookFileExtension(file.FileType))
                {
                    await NavigationService.NavigateAsync(nameof(Presentation.Views.EBookReaderPage), parameters, new SuppressNavigationTransitionInfo());
                }
            }
        }


        private DelegateCommand<object> _SearchQuerySubmitCommand;
        public DelegateCommand<object> SearchQuerySubmitCommand =>
            _SearchQuerySubmitCommand ?? (_SearchQuerySubmitCommand = new DelegateCommand<object>(ExecuteSearchQuerySubmitCommand));

        void ExecuteSearchQuerySubmitCommand(object parameter)
        {
            if (parameter is string q)
            {
                // 検索ページを開く
                NavigationService.NavigateAsync(nameof(Views.SearchResultPage), ("q", q));
            }
            else if (parameter is StorageItemSearchEntry entry)
            {
                ExecuteSuggestChosenCommand(entry);
            }
        }


        #endregion
    }


    public class AutoSuggestBoxGroupBase : BindableBase
    {
        public string Label { get; set; }
        public ObservableCollection<StorageItemSearchEntry> Items { get; } = new ObservableCollection<StorageItemSearchEntry>();
    }

    public class AutoSuggestBoxSearchIndexGroup : AutoSuggestBoxGroupBase
    {
        private uint _SearchIndexUpdateProgressCount;
        public uint SearchIndexUpdateProgressCount
        {
            get { return _SearchIndexUpdateProgressCount; }
            set { SetProperty(ref _SearchIndexUpdateProgressCount, value); }
        }

        private uint _SearchIndexUpdateTotalCount;
        public uint SearchIndexUpdateTotalCount
        {
            get { return _SearchIndexUpdateTotalCount; }
            set { SetProperty(ref _SearchIndexUpdateTotalCount, value); }
        }

    }


    public class MenuSeparatorViewModel
    {

    }

    public class MenuItemViewModel
    {
        public string PageType { get; set; }
        public string Parameters { get; set; }
    }

}
