﻿using I18NPortable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views;
using Windows.Storage;
using Windows.System;
using Windows.ApplicationModel.DataTransfer;
using static TsubameViewer.Core.Models.SourceFolders.SourceStorageItemsRepository;


namespace TsubameViewer.ViewModels;

public sealed class PrimaryWindowCoreLayoutViewModel 
    : ObservableRecipient    
    , IRecipient<SourceStorageItemAddedMessage>
    , IRecipient<SourceStorageItemRemovedMessage>
    , IRecipient<SourceStorageItemMovedOrRenameMessage>
    , IRecipient<SourceStorageItemIgnoringRequestMessage>
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
        NavigationStackRepository restoreNavigationManager,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        FolderContainerTypeManager folderContainerTypeManager,
        SourceChoiceCommand sourceChoiceCommand,
        RefreshNavigationCommand refreshNavigationCommand,
        OpenPageCommand openPageCommand,
        StartSelectionCommand startSelectionCommand
        )
    {                        
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

        _foldersMenuSubItem = new MenuSubItemViewModel()
        {
            PageType = nameof(Views.SourceStorageItemsPage),
            Title = "SourceStorageItemsPage".Translate(),
            AccessKey = "1",
            KeyboardAceseralator = VirtualKey.Number1
        };

        MenuItems = new List<object>
        {
            _foldersMenuSubItem,
            new MenuItemViewModel() { PageType = nameof(Views.AlbamListupPage), Title = "Albam".Translate(), AccessKey = "3", KeyboardAceseralator = VirtualKey.Number3 },
        };

        RefreshFolderSubItems();    
        IsActive= true;
    }


    void IRecipient<SourceStorageItemRemovedMessage>.Receive(SourceStorageItemRemovedMessage message)
    {
        _scheduler.Schedule(() => 
        {
            RefreshFolderSubItems();
        });
    }

    void IRecipient<SourceStorageItemAddedMessage>.Receive(SourceStorageItemAddedMessage message)
    {
        RefreshFolderSubItems();
    }

    void IRecipient<SourceStorageItemMovedOrRenameMessage>.Receive(SourceStorageItemMovedOrRenameMessage message)
    {
        RefreshFolderSubItems();
    }

    void IRecipient<SourceStorageItemIgnoringRequestMessage>.Receive(SourceStorageItemIgnoringRequestMessage message)
    {
        RefreshFolderSubItems();
    }


    public void RefreshFolderSubItems()
    {
        _foldersMenuSubItem.Items.Clear();
        foreach (var folderPath in SourceStorageItemsRepository.GetParsistantItemsFromCache())
        {
            _foldersMenuSubItem.Items.Add(new MenuItemViewModel()
            {
                PageType = nameof(Views.FolderListupPage),
                Parameters = new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(folderPath))),
                Title = Path.GetFileName(folderPath),
            }); ;
        }
    }



    MenuSubItemViewModel _foldersMenuSubItem;

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
                _messenger.NavigateAsync(menuItem.PageType, menuItem.Parameters);
            }            
        });

    public ApplicationSettings ApplicationSettings { get; }
    public NavigationStackRepository RestoreNavigationManager { get; }
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
                .Append(" ").Append(SystemInformation.Instance.DeviceFamily)
                ;
            var data = new DataPackage();
            data.SetText(sb.ToString());
            Clipboard.SetContent(data);
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
    private readonly Core.AsyncLock _suggestUpdateLock = new ();
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
        var storageItem = await SourceStorageItemsRepository.TryGetStorageItemFromPath(entry.Path);
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
    public INavigationParameters Parameters { get; set; }
    public string AccessKey { get; set; }
    public VirtualKey KeyboardAceseralator { get; set; }
}


public class MenuSubItemViewModel : MenuItemViewModel
{
    public ObservableCollection<MenuItemViewModel> Items { get; } = new();
}
