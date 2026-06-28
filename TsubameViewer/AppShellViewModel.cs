using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluent.Icons;
using I18NPortable;
using Microsoft.Toolkit.Uwp.Helpers;
using R3;
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
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using ZLinq;
using static TsubameViewer.Core.Models.SourceFolders.SourceStorageItemsRepository;
#nullable enable

namespace TsubameViewer.ViewModels;

public sealed partial class AppShellViewModel 
    : ObservableRecipient    
    , IRecipient<SourceStorageItemAddedMessage>
    , IRecipient<SourceStorageItemRemovedMessage>
    , IRecipient<SourceStorageItemMovedOrRenameMessage>
    , IRecipient<SourceStorageItemIgnoringRequestMessage>
    , IRecipient<SourceStorageItemReorderedMessage>
{
    readonly IScheduler _scheduler;
    readonly IMessenger _messenger;
    readonly FolderContainerTypeManager _folderContainerTypeManager;

    public List<object> HeaderMenuItems { get; }
    public ObservableCollection<object> MenuItems { get;  }

    R3.CompositeDisposable _disposables = new R3.CompositeDisposable();

    public AppShellViewModel(
        IScheduler scheduler,
        IMessenger messenger,
        ApplicationSettings applicationSettings,
        NavigationStackRepository restoreNavigationManager,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        FolderContainerTypeManager folderContainerTypeManager,
        SourceChoiceCommand sourceChoiceCommand,
        RefreshNavigationCommand refreshNavigationCommand,
        OpenPageCommand openPageCommand,
        StartSelectionCommand startSelectionCommand,
        BackNavigationCommand backNavigationCommand
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
        BackNavigationCommand = backNavigationCommand;

        HeaderMenuItems = [
            new MenuItemViewModel() { PageType = nameof(Views.SourceStorageItemsPage), Title = "Folders".Translate(), AccessKey = "1", KeyboardAceseralator = VirtualKey.Number1 },
            new MenuItemViewModel() { PageType = nameof(Views.FolderListupPage), Parameters = new NavigationParameters() { { PageNavigationConstants.AlbamPathKey, Uri.EscapeDataString(FavoriteAlbam.FavoriteAlbamId.ToString()) }}, Title = "FavoriteAlbam".Translate(), AccessKey = "2", KeyboardAceseralator = VirtualKey.Number2 },
            new MenuItemViewModel() { PageType = nameof(Views.HistoryPage), Title = "HistoryPage_Title".Translate(), AccessKey = "3", KeyboardAceseralator = VirtualKey.Number3 },
        ];
        MenuItems = [];
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

    void IRecipient<SourceStorageItemReorderedMessage>.Receive(SourceStorageItemReorderedMessage message)
    {
        RefreshFolderSubItems();
    }


    public void RefreshFolderSubItems()
    {
        MenuItems.Clear();
        foreach (var headerItem in HeaderMenuItems)
        {
            MenuItems.Add(headerItem);
        }

        foreach (var entry in SourceStorageItemsRepository.GetParsistantItemsFromCache().AsValueEnumerable().OrderBy(x => x.Order))
        {
            MenuItems.Add(new MenuItemViewModel()
            {
                PageType = nameof(Views.FolderListupPage),
                Parameters = new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(entry.Path))),
                Title = Path.GetFileName(entry.Path),
            }); ;
        }
    }

    [ObservableProperty]
    bool _isDisplayMenu = true;

    public ApplicationSettings ApplicationSettings { get; }
    public NavigationStackRepository RestoreNavigationManager { get; }
    public SourceStorageItemsRepository SourceStorageItemsRepository { get; }
    public SourceChoiceCommand SourceChoiceCommand { get; }
    public RefreshNavigationCommand RefreshNavigationCommand { get; }
    public BackNavigationCommand BackNavigationCommand { get; }
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
                .Append(" ").Append(SystemInformation.Instance.DeviceFamily);
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



}

public sealed partial class InPageSearchContext : IDisposable
{
    public InPageSearchContext(
        IMessenger messenger,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        FolderContainerTypeManager folderContainerTypeManager)
    {
        _messenger = messenger;
        SourceStorageItemsRepository = sourceStorageItemsRepository;
        _folderContainerTypeManager = folderContainerTypeManager;

        DisposableBuilder db = new();
        UpdateAutoSuggestCommand = new R3.ReactiveCommand<string>()
            .AddTo(ref db);

        UpdateAutoSuggestCommand
            .AsObservable()
            .Debounce(TimeSpan.FromSeconds(0.250))
            .Where(_ => _onceSkipSuggestUpdate is false)
            .SubscribeAwait(async (x, ct) => await UpdateAutoSuggestAsync(x, ct))
            .AddTo(ref db);

        _disposable = db.Build();

        AutoSuggestBoxItems = new[]
        {
            _autoSuggestItemsGroup,
        };
    }


    public void Dispose()
    {
        _disposable.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    #region Search

    IDisposable _disposable;
    AutoSuggestBoxGroupBase _autoSuggestItemsGroup = new AutoSuggestBoxGroupBase();
    public object[] AutoSuggestBoxItems { get; }
    public R3.ReactiveCommand<string> UpdateAutoSuggestCommand { get; }
    public SourceStorageItemsRepository SourceStorageItemsRepository { get; }
    bool _onceSkipSuggestUpdate = false;
    readonly Core.AsyncLock _suggestUpdateLock = new();
    readonly IMessenger _messenger;
    readonly FolderContainerTypeManager _folderContainerTypeManager;
    CancellationTokenSource? _cts;

    async Task UpdateAutoSuggestAsync(string parameter, CancellationToken ct)
    {
        CancellationTokenSource cts;
        CancellationToken updateCt;
        using (await _suggestUpdateLock.LockAsync(default))
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _autoSuggestItemsGroup.Items.Clear();

            if (_onceSkipSuggestUpdate)
            {
                _onceSkipSuggestUpdate = false;
                return;
            }
            if (string.IsNullOrWhiteSpace(parameter)) { return; }

            _cts = cts = new CancellationTokenSource();
            updateCt = cts.Token;
        }

        object recipentObject = new object();

        try
        {
            List<IStorageItem> result = await Task.Run(async () => await SourceStorageItemsRepository.SearchAsync(parameter.Trim(), updateCt).Take(3).ToListAsync(updateCt), updateCt);

            ct.ThrowIfCancellationRequested();
            updateCt.ThrowIfCancellationRequested();

            using (await _suggestUpdateLock.LockAsync(default))
            {
                foreach (var item in result)
                {
                    _autoSuggestItemsGroup.Items.Add(item);
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

    [RelayCommand]
    async Task SuggestChosenAsync(IStorageItem entry)
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
                await _messenger.NavigateAsync(nameof(EBookViewerPage), parameters);
            }
            else if (SupportedFileTypesHelper.IsSupportedMovieFileExtension(file.FileType))
            {
                await _messenger.NavigateAsync(nameof(MovieViewerPage), parameters);
            }
        }

        using (await _suggestUpdateLock.LockAsync(default))
        {
            _onceSkipSuggestUpdate = false;
        }
    }


    [RelayCommand]
    async Task SearchQuerySubmitAsync(object parameter)
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
            await SuggestChosenAsync(entry);
        }
    }

    #endregion
}


public class AutoSuggestBoxGroupBase : ObservableObject
{
    public string? Label { get; set; }
    public ObservableCollection<IStorageItem> Items { get; } = new ObservableCollection<IStorageItem>();        
}


public class MenuSeparatorViewModel
{

}

public class MenuItemViewModel
{
    public string? Title { get; set; }
    public string? PageType { get; set; }
    public INavigationParameters? Parameters { get; set; }
    public string? AccessKey { get; set; }
    public VirtualKey KeyboardAceseralator { get; set; }
}

public class MenuItemInvokeActionViewModel
{
    public string? Title { get; set; }
    public string? Tooltip { get; set; }
    public Action? Invoked { get; set; }
    public object? Icon { get; set; }
    public string? AccessKey { get; set; }
    public VirtualKey KeyboardAceseralator { get; set; }
}

public class MenuSubItemViewModel : MenuItemViewModel
{
    public ObservableCollection<object> Items { get; } = new();
    public object? Icon { get; set; }
}
