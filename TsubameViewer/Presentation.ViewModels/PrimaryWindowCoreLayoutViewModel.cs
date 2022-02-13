using I18NPortable;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.Helpers;
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
using TsubameViewer.Models.Domain.Navigation;
using TsubameViewer.Models.Domain.RestoreNavigation;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Models.UseCase;
using TsubameViewer.Presentation.Navigations;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.ViewModels.SourceFolders.Commands;
using TsubameViewer.Presentation.Views;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml.Media.Animation;
using Xamarin.Essentials;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class PrimaryWindowCoreLayoutViewModel : ObservableObject
    {
        private readonly IScheduler _scheduler;
        private readonly IMessenger _messenger;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;

        public List<object> MenuItems { get;  }

        CompositeDisposable _disposables = new CompositeDisposable();

        public PrimaryWindowCoreLayoutViewModel(
            IScheduler scheduler,
            IMessenger messenger,
            ApplicationSettings applicationSettings,
            RestoreNavigationManager restoreNavigationManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            FolderContainerTypeManager folderContainerTypeManager,
            SourceChoiceCommand sourceChoiceCommand,
            RefreshNavigationCommand refreshNavigationCommand,
            OpenPageCommand openPageCommand,
            StartSelectionCommand startSelectionCommand
            )
        {
            MenuItems = new List<object>
            {
                new MenuItemViewModel() { PageType = nameof(Views.SourceStorageItemsPage), Title = "SourceStorageItemsPage".Translate(), AccessKey = "1", KeyboardAceseralator = VirtualKey.Number1 },
                new MenuItemViewModel() { PageType = nameof(Views.AlbamListupPage), Title = "Albam".Translate(), AccessKey = "2", KeyboardAceseralator = VirtualKey.Number2 },
            };
            _scheduler = scheduler;
            _messenger = messenger;
            ApplicationSettings = applicationSettings;
            RestoreNavigationManager = restoreNavigationManager;
            SourceStorageItemsRepository = sourceStorageItemsRepository;
            _folderContainerTypeManager = folderContainerTypeManager;
            SourceChoiceCommand = sourceChoiceCommand;
            SourceChoiceCommand.OpenAfterChoice = true;
            RefreshNavigationCommand = refreshNavigationCommand;
            OpenPageCommand = openPageCommand;
            StartSelectionCommand = startSelectionCommand;
            UpdateAutoSuggestCommand = new ReactiveCommand<string>();

            UpdateAutoSuggestCommand
                .Throttle(TimeSpan.FromSeconds(0.250), _scheduler)
                .Where(_ => _onceSkipSuggestUpdate is false)
                .Subscribe(ExecuteUpdateAutoSuggestCommand)
                .AddTo(_disposables);

            AutoSuggestBoxItems = new[]
            {
                _AutoSuggestItemsGroup,
            };
        }

        AutoSuggestBoxGroupBase _AutoSuggestItemsGroup = new AutoSuggestBoxGroupBase();

        public object[] AutoSuggestBoxItems { get; }

        private bool _IsDisplayMenu = true;
        public bool IsDisplayMenu
        {
            get { return _IsDisplayMenu; }
            set { SetProperty(ref _IsDisplayMenu, value); }
        }


        RelayCommand<object> _OpenMenuItemCommand;
        public RelayCommand<object> OpenMenuItemCommand =>
            _OpenMenuItemCommand ??= new RelayCommand<object>(item => 
            {
                if (item is MenuItemViewModel menuItem)
                {
                    _messenger.NavigateAsync(menuItem.PageType);
                }                
            });

        public ApplicationSettings ApplicationSettings { get; }
        public RestoreNavigationManager RestoreNavigationManager { get; }
        public SourceStorageItemsRepository SourceStorageItemsRepository { get; }
        public SourceChoiceCommand SourceChoiceCommand { get; }
        public RefreshNavigationCommand RefreshNavigationCommand { get; }
        public OpenPageCommand OpenPageCommand { get; }
        public StartSelectionCommand StartSelectionCommand { get; }


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
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://marshmallow-qa.com/tor4kichi"));
            });


        public RelayCommand SendFeedbackWithStoreReviewCommand { get; } =
            new RelayCommand(async () =>
            {
                await Microsoft.Toolkit.Uwp.Helpers.SystemInformation.LaunchStoreForReviewAsync();
            });




        #region Search

        public ReactiveCommand<string> UpdateAutoSuggestCommand { get; }

        private bool _onceSkipSuggestUpdate = false;
        private readonly Models.Infrastructure.AsyncLock _suggestUpdateLock = new ();
        private CancellationTokenSource _cts;
        private async void ExecuteUpdateAutoSuggestCommand(string parameter)
        {            
            CancellationTokenSource cts;
            CancellationToken ct = default;
            using (await _suggestUpdateLock.LockAsync(default))
            {
                _cts?.Cancel();
                _cts = null;

                _AutoSuggestItemsGroup.Items.Clear();

                if (_onceSkipSuggestUpdate) 
                {
                    _onceSkipSuggestUpdate = false;
                    return; 
                }
                if (string.IsNullOrWhiteSpace(parameter)) { return; }

                _cts = cts = new CancellationTokenSource();
                ct = cts.Token;
            }

            object recipentObject = new object();
            
            try
            {
                var result = await Task.Run(async () => await SourceStorageItemsRepository.SearchAsync(parameter.Trim(), ct).Take(3).ToListAsync(ct), ct);

                ct.ThrowIfCancellationRequested();

                using (await _suggestUpdateLock.LockAsync(default))
                {
                    foreach (var item in result)
                    {
                        _AutoSuggestItemsGroup.Items.Add(item);
                    }
                    _cts = null;
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                cts.Dispose();
            }
        }

        private RelayCommand<IStorageItem> _SuggestChosenCommand;
        public RelayCommand<IStorageItem> SuggestChosenCommand =>
            _SuggestChosenCommand ?? (_SuggestChosenCommand = new RelayCommand<IStorageItem>(ExecuteSuggestChosenCommand));

        async void ExecuteSuggestChosenCommand(IStorageItem entry)
        {
            using (await _suggestUpdateLock.LockAsync(default))
            {
                _onceSkipSuggestUpdate = true;
                _cts?.Cancel();
                _cts = null;
            }

            var path = entry.Path;
            var parameters = new NavigationParameters();
            var storageItem = await SourceStorageItemsRepository.GetStorageItemFromPath(entry.Path);
            parameters.Add(PageNavigationConstants.GeneralPathKey, entry.Path);
            if (storageItem is StorageFolder itemFolder)
            {
                var containerType = await _messenger.WorkWithBusyWallAsync(async ct => await _folderContainerTypeManager.GetFolderContainerTypeWithCacheAsync(itemFolder, ct), CancellationToken.None);
                if (containerType == FolderContainerType.OnlyImages)
                {
                    await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
                    return;
                }
                else
                {
                    await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
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
                    await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
                }
                else if (SupportedFileTypesHelper.IsSupportedEBookFileExtension(file.FileType))
                {
                    await _messenger.NavigateAsync(nameof(EBookReaderPage), parameters);
                }
            }

            using (await _suggestUpdateLock.LockAsync(default))
            { 
                _onceSkipSuggestUpdate = false;
            }
        }


        private RelayCommand<object> _SearchQuerySubmitCommand;
        public RelayCommand<object> SearchQuerySubmitCommand =>
            _SearchQuerySubmitCommand ?? (_SearchQuerySubmitCommand = new RelayCommand<object>(ExecuteSearchQuerySubmitCommand));
        
        async void ExecuteSearchQuerySubmitCommand(object parameter)
        {
            if (parameter is string q)
            {
                if (string.IsNullOrWhiteSpace(q)) { return; }

                using (await _suggestUpdateLock.LockAsync(default))
                {
                    _onceSkipSuggestUpdate = true;
                    _cts?.Cancel();
                    _cts = null;
                }
                    // 検索ページを開く
                    await _messenger.NavigateAsync(nameof(Views.SearchResultPage), isForgetNavigation: true, ("q", q));
                using (await _suggestUpdateLock.LockAsync(default))
                {
                    _onceSkipSuggestUpdate = false;                
                }
            }
            else if (parameter is IStorageItem entry)
            {
                ExecuteSuggestChosenCommand(entry);
            }
        }


        #endregion
    }


    public class AutoSuggestBoxGroupBase : ObservableObject
    {
        public string Label { get; set; }
        public ObservableCollection<IStorageItem> Items { get; } = new ObservableCollection<IStorageItem>();        
    }


    public class MenuSeparatorViewModel
    {

    }

    public class MenuItemViewModel
    {
        public string Title { get; set; }
        public string PageType { get; set; }
        public string Parameters { get; set; }
        public string AccessKey { get; set; }
        public VirtualKey KeyboardAceseralator { get; set; }
    }

}
