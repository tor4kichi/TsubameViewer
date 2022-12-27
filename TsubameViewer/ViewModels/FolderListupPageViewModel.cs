﻿using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using Microsoft.Toolkit.Uwp.UI;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Helpers;
using TsubameViewer.Services.Navigation;
using TsubameViewer.Services;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using CommunityToolkit.Diagnostics;

namespace TsubameViewer.ViewModels;

public class CachedFolderListupItems
{
    public ObservableCollection<StorageItemViewModel> FolderItems { get; set; }
    public int GetTotalCount()
    {
        return FolderItems.Count;
    }

    public void DisposeItems()
    {
        foreach (var itemVM in FolderItems)
        {
            itemVM.Dispose();
        }
    }
}


public sealed class FolderListupPageViewModel : NavigationAwareViewModelBase
{
    private bool _NowProcessing;
    public bool NowProcessing
    {
        get { return _NowProcessing; }
        set { SetProperty(ref _NowProcessing, value); }
    }

    private readonly IScheduler _scheduler;
    private readonly IMessenger _messenger;
    private readonly IBookmarkService _bookmarkManager;
    private readonly AlbamRepository _albamRepository;
    private readonly ImageCollectionManager _imageCollectionManager;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly IFolderLastIntractItemService _folderLastIntractItemManager;
    private readonly IThumbnailImageService _thumbnailManager;        
    private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    private readonly BackNavigationCommand _backNavigationCommand;

    public ISecondaryTileManager SecondaryTileManager { get; }
    public OpenPageCommand OpenPageCommand { get; }
    public OpenListupCommand OpenListupCommand { get; }
    public OpenFolderItemCommand OpenFolderItemCommand { get; }
    public OpenFolderItemSecondaryCommand OpenFolderItemSecondaryCommand { get; }
    public OpenImageViewerCommand OpenImageViewerCommand { get; }
    public OpenFolderListupCommand OpenFolderListupCommand { get; }
    public OpenImageListupCommand OpenImageListupCommand { get; }

    public OpenWithExplorerCommand OpenWithExplorerCommand { get; }
    public SecondaryTileAddCommand SecondaryTileAddCommand { get; }
    public SecondaryTileRemoveCommand SecondaryTileRemoveCommand { get; }
    public ChangeStorageItemThumbnailImageCommand ChangeStorageItemThumbnailImageCommand { get; }
    public OpenWithExternalApplicationCommand OpenWithExternalApplicationCommand { get; }
    public FileDeleteCommand FileDeleteCommand { get; }
    public ObservableCollection<StorageItemViewModel> FolderItems { get; private set; }

    private AdvancedCollectionView _FileItemsView;
    public AdvancedCollectionView FileItemsView
    {
        get { return _FileItemsView; }
        set { SetProperty(ref _FileItemsView, value); }
    }

    private bool _HasFileItem;
    public bool HasFileItem
    {
        get { return _HasFileItem; }
        set { SetProperty(ref _HasFileItem, value); }
    }


    public ReactivePropertySlim<FileSortType> SelectedFileSortType { get; }
    public ReactivePropertySlim<FileSortType?> SelectedChildFileSortType { get; }


    public ReactivePropertySlim<StorageItemViewModel> FolderLastIntractItem { get; }

    private static readonly Core.AsyncLock _NavigationLock = new ();

    private IImageSource? _currentImageSource;

    private CancellationTokenSource _leavePageCancellationTokenSource;

    private string _DisplayCurrentPath;
    public string DisplayCurrentPath
    {
        get { return _DisplayCurrentPath; }
        set { SetProperty(ref _DisplayCurrentPath, value); }
    }

    private StorageItemViewModel _CurrentFolderItem;
    public StorageItemViewModel CurrentFolderItem
    {
        get { return _CurrentFolderItem; }
        set { SetProperty(ref _CurrentFolderItem, value); }
    }
    
    public SelectionContext Selection { get; } = new SelectionContext();

    private string _selectedCountDisplayText;
    public string SelectedCountDisplayText
    {
        get => _selectedCountDisplayText;
        set => SetProperty(ref _selectedCountDisplayText, value);
    }


  
    public string FoldersManagementPageName => PrimaryWindowCoreLayout.HomePageName;

    private string _DisplayCurrentArchiveFolderName;
    public string DisplayCurrentArchiveFolderName
    {
        get { return _DisplayCurrentArchiveFolderName; }
        private set { SetProperty(ref _DisplayCurrentArchiveFolderName, value); }
    }

    CompositeDisposable _disposables = new CompositeDisposable();
    CompositeDisposable _navigationDisposables;

    DateTimeOffset _sourceItemLastUpdatedTime;

    public FolderListupPageViewModel(
        IScheduler scheduler,
        IMessenger messenger,
        IBookmarkService bookmarkManager,
        AlbamRepository albamRepository,
        ImageCollectionManager imageCollectionManager,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        ISecondaryTileManager secondaryTileManager,
        IFolderLastIntractItemService folderLastIntractItemManager,
        IThumbnailImageService thumbnailManager,            
        DisplaySettingsByPathRepository displaySettingsByPathRepository,
        BackNavigationCommand backNavigationCommand,
        OpenPageCommand openPageCommand,
        OpenListupCommand openListupCommand,
        OpenFolderItemCommand openFolderItemCommand,
        OpenFolderItemSecondaryCommand openFolderItemSecondaryCommand,
        OpenImageViewerCommand openImageViewerCommand,
        OpenFolderListupCommand openFolderListupCommand,
        OpenImageListupCommand openImageListupCommand,
        OpenWithExplorerCommand openWithExplorerCommand,
        SecondaryTileAddCommand secondaryTileAddCommand,
        SecondaryTileRemoveCommand secondaryTileRemoveCommand,
        ChangeStorageItemThumbnailImageCommand changeStorageItemThumbnailImageCommand,
        OpenWithExternalApplicationCommand openWithExternalApplicationCommand,
        FileDeleteCommand fileDeleteCommand
        )
    {
        _scheduler = scheduler;
        _messenger = messenger;
        _bookmarkManager = bookmarkManager;
        _albamRepository = albamRepository;
        _imageCollectionManager = imageCollectionManager;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        SecondaryTileManager = secondaryTileManager;
        _folderLastIntractItemManager = folderLastIntractItemManager;
        _thumbnailManager = thumbnailManager;            
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        _backNavigationCommand = backNavigationCommand;
        OpenPageCommand = openPageCommand;
        OpenListupCommand = openListupCommand;
        OpenFolderItemCommand = openFolderItemCommand;
        OpenFolderItemSecondaryCommand = openFolderItemSecondaryCommand;
        OpenImageViewerCommand = openImageViewerCommand;
        OpenFolderListupCommand = openFolderListupCommand;
        OpenImageListupCommand = openImageListupCommand;
        OpenWithExplorerCommand = openWithExplorerCommand;
        SecondaryTileAddCommand = secondaryTileAddCommand;
        SecondaryTileRemoveCommand = secondaryTileRemoveCommand;
        ChangeStorageItemThumbnailImageCommand = changeStorageItemThumbnailImageCommand;
        OpenWithExternalApplicationCommand = openWithExternalApplicationCommand;
        FileDeleteCommand = fileDeleteCommand;
        FolderItems = new ObservableCollection<StorageItemViewModel>();
        FileItemsView = new AdvancedCollectionView(FolderItems);
        FolderLastIntractItem = new ReactivePropertySlim<StorageItemViewModel>()
            .AddTo(_disposables);

        SelectedFileSortType = new ReactivePropertySlim<FileSortType>(FileSortType.TitleAscending, ReactivePropertyMode.DistinctUntilChanged)
            .AddTo(_disposables);

        SelectedChildFileSortType = new ReactivePropertySlim<FileSortType?>(null)
            .AddTo(_disposables);
    }


    public override async void OnNavigatedFrom(INavigationParameters parameters)
    {
        Selection.EndSelection();
        using (await _NavigationLock.LockAsync(default))
        {
            _leavePageCancellationTokenSource?.Cancel();
            _leavePageCancellationTokenSource?.Dispose();
            _leavePageCancellationTokenSource = null;

            _navigationDisposables?.Dispose();
            _navigationDisposables = null;

            foreach (var itemVM in FolderItems)
            {
                itemVM.StopImageLoading();
            }
           
            if (_currentImageSource != null
                && parameters.ContainsKey(PageNavigationConstants.GeneralPathKey) &&  parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string path))
            {
                _folderLastIntractItemManager.SetLastIntractItemName(_currentImageSource.Path, Uri.UnescapeDataString(path));
            }

            _messenger.Unregister<RefreshNavigationRequestMessage>(this);
            _messenger.Unregister<BackNavigationRequestingMessage>(this);
            _messenger.Unregister<StartMultiSelectionMessage>(this);

            base.OnNavigatedFrom(parameters);
        }
    }

    void ClearContent()
    {
        foreach (var itemVM in FolderItems)
        {
            itemVM.Dispose();
        }
        FolderItems.Clear();

        (_currentImageSource as IDisposable)?.Dispose();
        _currentImageSource = null;
        (_imageCollectionContext as IDisposable)?.Dispose();
        _imageCollectionContext = null;

        CurrentFolderItem?.Dispose();
        CurrentFolderItem = null;

        DisplayCurrentArchiveFolderName = null;
    }

    public StorageItemViewModel GetLastIntractItem()
    {
        if (_currentImageSource == null) { return null; }

        var lastIntaractItem = _folderLastIntractItemManager.GetLastIntractItemName(_currentImageSource.Path);
        if (lastIntaractItem == null) { return null; }

        foreach (var item in FolderItems)
        {
            if (item.Name == lastIntaractItem)
            {
                return item;
            }
        }

        return null;
    }

    private async ValueTask<bool> IsRequireUpdateAsync(string newPath, string pageName, CancellationToken ct)
    {
        Guard.IsNotNullOrWhiteSpace(newPath);

        if (_currentImageSource?.Path != newPath) { return true; }

        if (string.IsNullOrEmpty(pageName) is false
            && _imageCollectionContext is ArchiveImageCollectionContext archiveImageCollectionContext
            && archiveImageCollectionContext.ArchiveDirectoryToken?.Key != pageName
            )
        {
            return true;
        }

        if (_currentImageSource != null
            && await _currentImageSource.StorageItem.GetBasicPropertiesAsync().AsTask(ct) is not null and var prop
            && _sourceItemLastUpdatedTime != prop.DateModified
            )
        {
            return true;
        }

        return false;
    }

    public override async Task OnNavigatedToAsync(INavigationParameters parameters)
    {
        // ナビゲーション全体をカバーしてロックしていないと_leavePageCancellationTokenSourceが先にキャンセルされているケースがある
        using var lockReleaser = await _NavigationLock.LockAsync(default);

        var mode = parameters.GetNavigationMode();
        if (mode == NavigationMode.Refresh && _imageCollectionContext != null)
        {
            await ReloadItemsAsync(_imageCollectionContext, _leavePageCancellationTokenSource.Token);
            return;
        }

        _navigationDisposables = new CompositeDisposable();
        _leavePageCancellationTokenSource = new CancellationTokenSource();
        var ct = _leavePageCancellationTokenSource.Token;

        NowProcessing = true;
        try
        {
            string? lastPath = _currentImageSource?.Path;
            if (parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string path))
            {
                (var newPath, var pageName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(path));

                if (_sourceStorageItemsRepository.IsIgnoredPath(newPath))
                {
                    throw new InvalidOperationException();
                }
                else if (await IsRequireUpdateAsync(newPath, pageName, ct))
                {
                    await ResetContent(newPath, pageName, ct);
                }
                else
                {
                    if (FolderItems != null)
                    {
                        foreach (var itemVM in FolderItems)
                        {
                            itemVM.RestoreThumbnailLoadingTask(ct);
                        }
                    }
                }
            }
            else if (parameters.TryGetValue(PageNavigationConstants.AlbamPathKey, out string albamPath))
            {
                (var albamIdString, _) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(albamPath));

                await ResetContentWithAlbam(albamIdString, ct);                
            }

            if (mode != NavigationMode.New)
            {
                StorageItemViewModel lastIntractItemVM = GetLastIntractItem();
                if (lastIntractItemVM != null)
                {
                    lastIntractItemVM.ThumbnailChanged();
                    lastIntractItemVM.Initialize(ct);

                    FolderLastIntractItem.Value = lastIntractItemVM;
                }
                else
                {
                    FolderLastIntractItem.Value = null;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            NowProcessing = false;
        }

        SelectedFileSortType
            .Subscribe(x => _ = SetSort(x, _leavePageCancellationTokenSource?.Token ?? CancellationToken.None))
            .AddTo(_navigationDisposables);

        if (_imageCollectionContext?.IsSupportedFolderContentsChanged ?? false)
        {
            // アプリ内部操作も含めて変更を検知する
            bool requireRefresh = false;
            _imageCollectionContext.CreateFolderAndArchiveFileChangedObserver()
                .Subscribe(_ =>
                {
                    _scheduler.Schedule(async () => 
                    {
                        if (Window.Current.Visible)
                        {
                            requireRefresh = false;
                            await ReloadItemsAsync(_imageCollectionContext, _leavePageCancellationTokenSource?.Token ?? CancellationToken.None);
                        }
                        else
                        {
                            requireRefresh = true;
                        }
                    });

                    Debug.WriteLine("Folder/Archive Update required. " + _currentImageSource?.Name ?? string.Empty);
                })
                .AddTo(_navigationDisposables);

            Window.Current.WindowActivationStateChanged()
                .Subscribe(async visible =>
                {
                    if (visible && requireRefresh && _imageCollectionContext is not null)
                    {
                        requireRefresh = false;
                        await ReloadItemsAsync(_imageCollectionContext, _leavePageCancellationTokenSource?.Token ?? CancellationToken.None);
                        Debug.WriteLine("Folder/Archive Updated. " + _currentImageSource?.Name ?? string.Empty);
                    }
                })
                .AddTo(_navigationDisposables);
        }

        _messenger.Register<RefreshNavigationRequestMessage>(this, (r, m) => 
        {
            // TODO: 現在のフォルダ名、ないしアーカイブ名が変わっていないかチェック
            _ = ReloadItemsAsync(_imageCollectionContext, _leavePageCancellationTokenSource?.Token ?? default);
        });

        _messenger.Register<StartMultiSelectionMessage>(this, (r, m) => 
        {
            Selection.StartSelection();
            FileDeleteCommand.NotifyCanExecuteChanged();
            OpenWithExplorerCommand.NotifyCanExecuteChanged();
        });

        Selection.ObserveProperty(x => x.IsSelectionModeEnabled)
            .Subscribe(selectionEnabled => 
            {
                if (selectionEnabled)
                {
                    _messenger.Send(new MenuDisplayMessage(Visibility.Collapsed));
                    _messenger.Register<BackNavigationRequestingMessage>(this, (r, m) =>
                    {
                        m.Value.IsHandled = true;
                        Selection.EndSelection();
                    });
                }
                else
                {
                    _messenger.Send(new MenuDisplayMessage(Visibility.Visible));
                    _messenger.Unregister<BackNavigationRequestingMessage>(this);
                }
            })
            .AddTo(_navigationDisposables);

        Selection.SelectedItems.ObserveProperty(x => x.Count)
            .Subscribe(count =>
            {
                SelectedCountDisplayText = "ImageSelection_SelectedCount".Translate(count);
                FileDeleteCommand.NotifyCanExecuteChanged();
                OpenWithExplorerCommand.NotifyCanExecuteChanged();
            })
            .AddTo(_navigationDisposables);

        await base.OnNavigatedToAsync(parameters);
    }

    IImageCollectionContext _imageCollectionContext;

    async Task ResetContent(string path, string pageName, CancellationToken ct)
    {
        using var lockObject = await _RefreshLock.LockAsync(ct);

        HasFileItem = false;
        
        // 表示情報の解決
        ClearContent();

        try
        {
            (_currentImageSource, _imageCollectionContext) = await _imageCollectionManager.GetImageSourceAndContextAsync(path, pageName, ct);

            Guard.IsNotNull(_currentImageSource);
            Guard.IsNotNull(_imageCollectionContext);
            
            CurrentFolderItem = new StorageItemViewModel(_currentImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository);
        }
        catch
        {
            ClearContent();
            throw;
        }
        
        DisplayCurrentPath = _currentImageSource.Path;

        var settingPath = path;
        var settings = _displaySettingsByPathRepository.GetFolderAndArchiveSettings(settingPath);
        if (settings != null)
        {
            SelectedFileSortType.Value = settings.Sort;
            SetSortAsyncUnsafe(SelectedFileSortType.Value, path);
        }
        else
        {
            if (_currentImageSource.StorageItem is StorageFolder)
            {
                SelectedFileSortType.Value = FileSortType.TitleAscending;
                SetSortAsyncUnsafe(SelectedFileSortType.Value, path);
            }
            else if (_currentImageSource.StorageItem is StorageFile file && file.IsSupportedMangaFile())
            {
                SelectedFileSortType.Value = FileSortType.TitleAscending;
                SetSortAsyncUnsafe(SelectedFileSortType.Value, path);
            }
        }

        SelectedChildFileSortType.Value = _displaySettingsByPathRepository.GetFileParentSettings(path);

        try
        {
            await ReloadItemsAsync(_imageCollectionContext, ct);
        }
        catch (OperationCanceledException)
        {
            ClearContent();
            (_backNavigationCommand as ICommand).Execute(null);
        }
    }

    async Task ResetContentWithAlbam(string albamIdString, CancellationToken ct)
    {
        using var lockObject = await _RefreshLock.LockAsync(ct);

        if (Guid.TryParse(albamIdString, out Guid albamId) is false)
        {
            throw new InvalidOperationException();
        }

        var albam = _albamRepository.GetAlbam(albamId);

        HasFileItem = false;
        
        DisplayCurrentPath = "Albam".Translate();

        string path = albam._id.ToString();

        //SelectedChildFileSortType.Value = _displaySettingsByPathRepository.GetFileParentSettings(path);
        SelectedChildFileSortType.Value = FileSortType.None;
        SelectedFileSortType.Value = FileSortType.UpdateTimeDecending;
        SetSortAsyncUnsafe(SelectedFileSortType.Value, path);        

        ClearContent();

        AlbamImageCollectionContext imageCollectionContext = new (albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _messenger);
        AlbamImageSource albamImageSource = new (albam, imageCollectionContext);
        CurrentFolderItem = new StorageItemViewModel(albamImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository);
        DisplayCurrentArchiveFolderName = imageCollectionContext.Name;

        _currentImageSource = albamImageSource;
        _imageCollectionContext = imageCollectionContext;

        try
        {
            await ReloadItemsAsync(_imageCollectionContext, ct);
        }
        catch (OperationCanceledException)
        {
            ClearContent();
            (_backNavigationCommand as ICommand).Execute(null);
        }
    }

    #region Refresh Item

    private static readonly Core.AsyncLock _RefreshLock = new ();

    private async Task ReloadItemsAsync(IImageCollectionContext imageCollectionContext, CancellationToken ct)
    {
        Guard.IsNotNull(imageCollectionContext);

        var oldItemPathMap = FolderItems.Select(x => x.Path).ToHashSet();
        var newItems = await _messenger.WorkWithBusyWallAsync((ct) => imageCollectionContext.GetFolderOrArchiveFilesAsync(ct).Where(x => x is not null), ct).ToListAsync(ct);
        var deletedItems = Enumerable.Except(oldItemPathMap, newItems.Select(x => x.Path))
            .Where(x => oldItemPathMap.Contains(x))
            .ToHashSet();

        using (FileItemsView.DeferRefresh())
        {
            // 削除アイテム
            Debug.WriteLine($"items count : {FolderItems.Count}");
            foreach (var itemVM in FolderItems.Where(x => deletedItems.Contains(x.Path)).ToArray())
            {
                itemVM.Dispose();
                FolderItems.Remove(itemVM);
            }

            Debug.WriteLine($"after deleted : {FolderItems.Count}");
            // 新規アイテム
            foreach (var item in newItems.Where(x => oldItemPathMap.Contains(x.Path) is false))
            {
                FolderItems.Add(new StorageItemViewModel(item, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository));
                ct.ThrowIfCancellationRequested();
            }
            Debug.WriteLine($"after added : {FolderItems.Count}");
        }

        ct.ThrowIfCancellationRequested();

        if (_currentImageSource?.StorageItem != null)
        {
            var prop = await _currentImageSource.StorageItem.GetBasicPropertiesAsync();
            _sourceItemLastUpdatedTime = prop.DateModified;
        }

        _ = Task.Run(async () =>
        {
            bool exist = await imageCollectionContext.IsExistImageFileAsync(ct);
            _scheduler.Schedule(() => HasFileItem = exist);
        }, ct);
    }

#endregion



#region FileSortType


    public static IEnumerable<SortDescription> ToSortDescription(FileSortType fileSortType)
    {
        return fileSortType switch
        {
            FileSortType.TitleAscending => new[] { new SortDescription(nameof(StorageItemViewModel.Name), SortDirection.Ascending) },
            FileSortType.TitleDecending => new[] { new SortDescription(nameof(StorageItemViewModel.Name), SortDirection.Descending) },
            FileSortType.UpdateTimeAscending => new[] { new SortDescription(nameof(StorageItemViewModel.DateCreated), SortDirection.Ascending) },
            FileSortType.UpdateTimeDecending => new[] { new SortDescription(nameof(StorageItemViewModel.DateCreated), SortDirection.Descending) },
            _ => throw new NotSupportedException(),
        };
    }

    private RelayCommand<object> _ChangeFileSortCommand;
    public RelayCommand<object> ChangeFileSortCommand =>
        _ChangeFileSortCommand ??= new RelayCommand<object>(sort =>
        {
            FileSortType? sortType = null;
            if (sort is int num)
            {
                sortType = (FileSortType)num;
            }
            else if (sort is FileSortType sortTypeExact)
            {
                sortType = sortTypeExact;
            }

            if (sortType.HasValue)
            {
                SelectedFileSortType.Value = sortType.Value;
            }
        });

    private async Task SetSort(FileSortType fileSort, CancellationToken ct)
    {
        if (_currentImageSource == null) { throw new NullReferenceException(nameof(_currentImageSource.Path)); }

        using (await _RefreshLock.LockAsync(ct))
        {            
            SetSortAsyncUnsafe(fileSort, _currentImageSource.Path);
        }
    }

    private void SetSortAsyncUnsafe(FileSortType fileSort, string path)
    {
        var sortDescriptions = ToSortDescription(fileSort);
        using (FileItemsView.DeferRefresh())
        {
            FileItemsView.SortDescriptions.Clear();
            FileItemsView.SortDescriptions.Add(new SortDescription(nameof(StorageItemViewModel.Type), SortDirection.Ascending));
            foreach (var sort in sortDescriptions)
            {
                FileItemsView.SortDescriptions.Add(sort);
            }
        }

        _displaySettingsByPathRepository.SetFolderAndArchiveSettings(
            path,
            fileSort
            );
    }


    private RelayCommand<object> _ChangeChildFileSortCommand;
    public RelayCommand<object> ChangeChildFileSortCommand =>
        _ChangeChildFileSortCommand ??= new RelayCommand<object>(sort =>
        {
            if (_currentImageSource == null) { throw new NullReferenceException(nameof(_currentImageSource.Path)); }

            FileSortType? sortType = null;
            if (sort is int num)
            {
                sortType = (FileSortType)num;
            }
            else if (sort is FileSortType sortTypeExact)
            {
                sortType = sortTypeExact;
            }
                            
            SelectedChildFileSortType.Value = sortType;
            _displaySettingsByPathRepository.SetFileParentSettings(_currentImageSource.Path, sortType);
        });

#endregion
}

public abstract class FolderItemsGroupBase
{
    public ObservableCollection<StorageItemViewModel> Items { get; set; }
}
public sealed class FolderFolderItemsGroup : FolderItemsGroupBase
{

}

public sealed class FileFolderItemsGroup : FolderItemsGroupBase
{

}

class FolderListupPageParameter
{
    public string Token { get; set; }
    public string Path { get; set; }
}
