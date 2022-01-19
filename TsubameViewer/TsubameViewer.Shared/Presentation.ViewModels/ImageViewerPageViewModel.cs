using Microsoft.Toolkit.Uwp.UI.Converters;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.ReadingFeature;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.RestoreNavigation;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views.ViewManagement.Commands;
using Uno;
using Uno.Extensions;
using Uno.Threading;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using static TsubameViewer.Models.Domain.ImageViewer.ImageCollectionManager;
using Microsoft.Toolkit.Mvvm.Messaging;
using System.Windows.Input;
using TsubameViewer.Presentation.Services.UWP;
using Uno.Disposables;
using CompositeDisposable = System.Reactive.Disposables.CompositeDisposable;
using System.Numerics;
using Windows.UI.Xaml.Media;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Presentation.ViewModels.Albam.Commands;
using TsubameViewer.Models.UseCase;
using TsubameViewer.Presentation.ViewModels.SourceFolders.Commands;
using Windows.System;
using Microsoft.Toolkit.Uwp;
using Microsoft.Toolkit.Mvvm.Messaging.Messages;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class ImageLoadedMessage : AsyncRequestMessage<Unit>
    {
        
    }


    public sealed class ImageViewerPageViewModel : ViewModelBase, IDisposable
    {
        private string _currentPath;
        private object _currentFolderItem;

        private CancellationTokenSource _navigationCts;

        IDisposable _ImageEnumerationDisposer;

        private IImageSource[] _Images;
        public IImageSource[] Images
        {
            get { return _Images; }
            private set { SetProperty(ref _Images, value); }
        }

        private int _CurrentImageIndex;
        public int CurrentImageIndex
        {
            get => _CurrentImageIndex;
            set => SetProperty(ref _CurrentImageIndex, value);
        }

        private string _pathForSettings = null;

        private string _ParentFolderOrArchiveName;
        public string ParentFolderOrArchiveName
        {
            get { return _ParentFolderOrArchiveName; }
            private set { SetProperty(ref _ParentFolderOrArchiveName, value); }
        }

        public IReadOnlyReactiveProperty<int> DisplayCurrentImageIndex { get; }
        public ReactivePropertySlim<FileSortType> SelectedFileSortType { get; }

        private readonly FileSortType DefaultFileSortType = FileSortType.None;

        private string _DisplaySortTypeInheritancePath;
        public string DisplaySortTypeInheritancePath
        {
            get { return _DisplaySortTypeInheritancePath; }
            private set { SetProperty(ref _DisplaySortTypeInheritancePath, value); }
        }

        public ReactiveProperty<double> CanvasWidth { get; }
        public ReactiveProperty<double> CanvasHeight { get; }




        private string _title;
        public string Title
        {
            get { return _title; }
            private set { SetProperty(ref _title, value); }
        }


        private string _page1Name;
        public string Page1Name
        {
            get { return _page1Name; }
            private set { SetProperty(ref _page1Name, value); }
        }

        private string _page2Name;
        public string Page2Name
        {
            get { return _page2Name; }
            private set { SetProperty(ref _page2Name, value); }
        }


        private bool _page1Favorite;
        public bool Page1Favorite
        {
            get { return _page1Favorite; }
            private set { SetProperty(ref _page1Favorite, value); }
        }

        private bool _page2Favorite;
        public bool Page2Favorite
        {
            get { return _page2Favorite; }
            private set { SetProperty(ref _page2Favorite, value); }
        }

        private IImageSource[] _currentDisplayImageSources;
        public IImageSource[] CurrentDisplayImageSources
        {
            get => _currentDisplayImageSources;
            set => SetProperty(ref _currentDisplayImageSources, value);
        }

        private string _pageFolderName;
        public string PageFolderName
        {
            get { return _pageFolderName; }
            set { SetProperty(ref _pageFolderName, value); }
        }

        private string[] _pageFolderNames;
        public string[] PageFolderNames
        {
            get { return _pageFolderNames; }
            set { SetProperty(ref _pageFolderNames, value); }
        }


        private StorageItemTypes _ItemType;
        public StorageItemTypes ItemType
        {
            get { return _ItemType; }
            private set { SetProperty(ref _ItemType, value); }
        }

        private bool _nowImageLoadingLongRunning;
        public bool NowImageLoadingLongRunning
        {
            get { return _nowImageLoadingLongRunning; }
            set { SetProperty(ref _nowImageLoadingLongRunning, value); }
        }


        readonly static char[] SeparateChars = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        private ApplicationView _appView;
        CompositeDisposable _navigationDisposables;
        private readonly DispatcherQueue _dispatcherQueue;

        public ImageViewerSettings ImageViewerSettings { get; }

        public ReactivePropertySlim<bool> IsLeftBindingEnabled { get; }
        public ReactiveCommand ToggleLeftBindingCommand { get; }

        public ReactivePropertySlim<bool> IsDoubleViewEnabled { get; }
        public ReactiveCommand ToggleDoubleViewCommand { get; }

        public ReactivePropertySlim<double> DefaultZoom { get; }

        private readonly IScheduler _scheduler;
        private readonly IMessenger _messenger;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly AlbamRepository _albamRepository;
        private readonly ImageCollectionManager _imageCollectionManager;
        private readonly BookmarkManager _bookmarkManager;
        private readonly RecentlyAccessManager _recentlyAccessManager;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly FolderLastIntractItemManager _folderLastIntractItemManager;
        private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
        CompositeDisposable _disposables = new CompositeDisposable();

        public ImageViewerPageViewModel(
            IScheduler scheduler,
            IMessenger messenger,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            AlbamRepository albamRepository,
            ImageCollectionManager imageCollectionManager,
            ImageViewerSettings imageCollectionSettings,
            BookmarkManager bookmarkManager,
            RecentlyAccessManager recentlyAccessManager,
            ThumbnailManager thumbnailManager,
            FolderListingSettings folderListingSettings,
            FolderLastIntractItemManager folderLastIntractItemManager,
            DisplaySettingsByPathRepository displaySettingsByPathRepository,
            ToggleFullScreenCommand toggleFullScreenCommand,
            BackNavigationCommand backNavigationCommand,
            FavoriteAddCommand favoriteAddCommand,
            FavoriteRemoveCommand favoriteRemoveCommand,
            AlbamItemEditCommand albamItemEditCommand,
            ChangeStorageItemThumbnailImageCommand changeStorageItemThumbnailImageCommand,
            OpenWithExplorerCommand openWithExplorerCommand,
            OpenWithExternalApplicationCommand openWithExternalApplicationCommand
            )
        {
            _scheduler = scheduler;
            _messenger = messenger;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _albamRepository = albamRepository;
            _imageCollectionManager = imageCollectionManager;
            ImageViewerSettings = imageCollectionSettings;
            ToggleFullScreenCommand = toggleFullScreenCommand;
            BackNavigationCommand = backNavigationCommand;
            FavoriteAddCommand = favoriteAddCommand;
            FavoriteRemoveCommand = favoriteRemoveCommand;
            AlbamItemEditCommand = albamItemEditCommand;
            ChangeStorageItemThumbnailImageCommand = changeStorageItemThumbnailImageCommand;
            ChangeStorageItemThumbnailImageCommand.IsArchiveThumbnailSetToFile = true;
            OpenWithExplorerCommand = openWithExplorerCommand;
            OpenWithExternalApplicationCommand = openWithExternalApplicationCommand;
            _bookmarkManager = bookmarkManager;
            _recentlyAccessManager = recentlyAccessManager;
            _thumbnailManager = thumbnailManager;
            _folderListingSettings = folderListingSettings;
            _folderLastIntractItemManager = folderLastIntractItemManager;
            _displaySettingsByPathRepository = displaySettingsByPathRepository;

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            ClearDisplayImages();
            _DisplayImages_0 = _displayImagesSingle[0];
            _DisplayImages_1 = _displayImagesSingle[1];
            _DisplayImages_2 = _displayImagesSingle[2];

            DisplayCurrentImageIndex = this.ObserveProperty(x => x.CurrentImageIndex)
                 .Select(x => x + 1)
                 .ToReadOnlyReactivePropertySlim()
                 .AddTo(_disposables);

            CanvasWidth = new ReactiveProperty<double>()
                .AddTo(_disposables);
            CanvasHeight = new ReactiveProperty<double>()
                .AddTo(_disposables);

            _appView = ApplicationView.GetForCurrentView();

            SelectedFileSortType = new ReactivePropertySlim<FileSortType>(DefaultFileSortType)
                .AddTo(_disposables);

            IsLeftBindingEnabled = new ReactivePropertySlim<bool>(mode: ReactivePropertyMode.DistinctUntilChanged).AddTo(_disposables);

            ToggleLeftBindingCommand = new ReactiveCommand().AddTo(_disposables);
            ToggleLeftBindingCommand.Subscribe(() => 
            {
                static bool SwapIfDoubleView(BitmapImage[] images)
                {
                    if (images.Any() && images.Length == 2)
                    {
                        (images[0], images[1]) = (images[1], images[0]);

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                IsLeftBindingEnabled.Value = !IsLeftBindingEnabled.Value;                
                ImageViewerSettings.SetViewerSettingsPerPath(_currentPath, IsDoubleViewEnabled.Value, IsLeftBindingEnabled.Value, DefaultZoom.Value);

                if (SwapIfDoubleView(DisplayImages_0))
                {
                    RaisePropertyChanged(nameof(DisplayImages_0));
                }
                if (SwapIfDoubleView(DisplayImages_1))
                {
                    RaisePropertyChanged(nameof(DisplayImages_1));
                }
                if (SwapIfDoubleView(DisplayImages_2))
                {
                    RaisePropertyChanged(nameof(DisplayImages_2));
                }
            }).AddTo(_disposables);
            IsDoubleViewEnabled = new ReactivePropertySlim<bool>(mode: ReactivePropertyMode.DistinctUntilChanged)
                .AddTo(_disposables);
            ToggleDoubleViewCommand = new ReactiveCommand()
                .AddTo(_disposables);
            ToggleDoubleViewCommand.Subscribe(async () => 
            {
                IsDoubleViewEnabled.Value = !IsDoubleViewEnabled.Value;
                ImageViewerSettings.SetViewerSettingsPerPath(_currentPath, IsDoubleViewEnabled.Value, IsLeftBindingEnabled.Value, DefaultZoom.Value);
                Debug.WriteLine($"window w={CanvasWidth.Value:F0}, h={CanvasHeight.Value:F0}");
                await ResetImageIndex(CurrentImageIndex);
            })
                .AddTo(_disposables);
            DefaultZoom = new ReactivePropertySlim<double>(mode: ReactivePropertyMode.DistinctUntilChanged).AddTo(_disposables);
        }



        public void Dispose()
        {
            _disposables.Dispose();
            _ImageEnumerationDisposer?.Dispose();
        }




        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            ClearCachedImages();
            ClearDisplayImages();
            _currentDisplayImageIndex = 0;
            IsAlreadySetDisplayImages = false;

            if (Images?.Any() ?? false)
            {
                Images.ForEach((IImageSource x) => x.TryDispose());
                _nowCurrenImageIndexChanging = true;
                Images = null;
                _nowCurrenImageIndexChanging = false;
            }

            _navigationCts.Cancel();
            _navigationCts.Dispose();
            _navigationDisposables.Dispose();
            _ImageEnumerationDisposer?.Dispose();
            _ImageEnumerationDisposer = null;
            _imageLoadingCts?.Cancel();
            _imageLoadingCts?.Dispose();

            _appView.Title = String.Empty;
            ParentFolderOrArchiveName = String.Empty;

            _messenger.Unregister<AlbamItemAddedMessage>(this);
            _messenger.Unregister<AlbamItemRemovedMessage>(this);

            base.OnNavigatedFrom(parameters);
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationDisposables = new CompositeDisposable();
            _navigationCts = new CancellationTokenSource()
                .AddTo(_navigationDisposables);
            _imageLoadingCts = new CancellationTokenSource();
            _imageCollectionContext = null;
            Page1Name = null;
            Title = null;
            PageFolderName = null;

            // 一旦ボタン類を押せないように変更通知
            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();

            string parsedPageName = null;
            string parsedArchiveFolderName = null;

            var mode = parameters.GetNavigationMode();
            if (mode == NavigationMode.New
                || mode == NavigationMode.Back
                || mode == NavigationMode.Forward
                )
            {
                if (parameters.TryGetValueSafe(PageNavigationConstants.GeneralPathKey, out string path))
                {
                    var unescapedPath = Uri.UnescapeDataString(path);
                    if (string.IsNullOrEmpty(unescapedPath)) { throw new InvalidOperationException(); }

                    (var itemPath, parsedPageName, parsedArchiveFolderName) = PageNavigationConstants.ParseStorageItemId(unescapedPath);

                    if (_currentPath != itemPath)
                    {

                        _currentPath = itemPath;

                        // PathReferenceCountManagerへの登録が遅延する可能性がある
                        IStorageItem currentFolderItem = null;
                        foreach (var _ in Enumerable.Repeat(0, 10))
                        {
                            currentFolderItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(_currentPath);
                            if (currentFolderItem != null)
                            {
                                _currentPath = currentFolderItem.Path;
                                break;
                            }

                            await Task.Delay(100);
                        }

                        Images = default;
                        _CurrentImageIndex = 0;

                        _appView.Title = currentFolderItem.Name;
                        Title = currentFolderItem.Name;

                        _currentFolderItem = currentFolderItem;

                        DisplaySortTypeInheritancePath = null;
                        _pathForSettings = SupportedFileTypesHelper.IsSupportedImageFileExtension(_currentPath)
                            ? Path.GetDirectoryName(_currentPath)
                            : _currentPath;

                        var settings = _displaySettingsByPathRepository.GetFolderAndArchiveSettings(_pathForSettings);
                        if (settings != null)
                        {
                            SelectedFileSortType.Value = settings.Sort;
                        }
                        else if (_displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(_pathForSettings) is not null and var parentSort && parentSort.ChildItemDefaultSort != null)
                        {
                            DisplaySortTypeInheritancePath = parentSort.Path;
                            SelectedFileSortType.Value = parentSort.ChildItemDefaultSort.Value;
                        }
                        else
                        {
                            SelectedFileSortType.Value = DefaultFileSortType;
                        }


                        (IsDoubleViewEnabled.Value, IsLeftBindingEnabled.Value, DefaultZoom.Value)
                            = ImageViewerSettings.GetViewerSettingsPerPath(_currentPath);

                    }
                }
                else if (parameters.TryGetValueSafe(PageNavigationConstants.AlbamPathKey, out string albamPath))
                {
                    var unescapedPath = Uri.UnescapeDataString(albamPath);
                    if (string.IsNullOrEmpty(unescapedPath)) { throw new InvalidOperationException(); }

                    (var itemPath, parsedPageName, _) = PageNavigationConstants.ParseStorageItemId(unescapedPath);

                    if (_currentPath != itemPath)
                    {
                        _currentPath = itemPath;

                        var albam = _albamRepository.GetAlbam(Guid.Parse(itemPath));

                        Images = default;
                        _CurrentImageIndex = 0;

                        _appView.Title = albam.Name;
                        Title = albam.Name;

                        _currentFolderItem = albam;
                        DisplaySortTypeInheritancePath = null;
                        _pathForSettings = null;

                        var settings = _displaySettingsByPathRepository.GetAlbamDisplaySettings(albam._id);
                        if (settings != null)
                        {
                            SelectedFileSortType.Value = settings.Sort;
                        }
                        else
                        {
                            SelectedFileSortType.Value = DefaultFileSortType;
                        }

                        (IsDoubleViewEnabled.Value, IsLeftBindingEnabled.Value, DefaultZoom.Value)
                            = ImageViewerSettings.GetViewerSettingsPerPath(_currentPath);
                    }
                }
            }

            // 以下の場合に表示内容を更新する
            //    1. 表示フォルダが変更された場合
            //    2. 前回の更新が未完了だった場合
            if (_currentFolderItem != null)
            {
                try
                {
#if DEBUG
                    //await _messenger.WorkWithBusyWallAsync(async ct => await Task.Delay(TimeSpan.FromSeconds(5), ct), _leavePageCancellationTokenSource.Token);
#endif
                    await _messenger.WorkWithBusyWallAsync(RefreshItems, _navigationCts.Token);
                }
                catch (OperationCanceledException)
                {
                    (BackNavigationCommand as ICommand).Execute(null);
                    return;
                }
            }

            // 表示する画像を決める
            if (mode == NavigationMode.Forward 
                || parameters.ContainsKey(PageNavigationConstants.Restored) 
                || (mode == NavigationMode.New && string.IsNullOrEmpty(parsedPageName) && string.IsNullOrEmpty(parsedArchiveFolderName)
                )
                )
            {
                
                if (string.IsNullOrEmpty(_pathForSettings) is false)
                {
                    var bookmarkPageName = _bookmarkManager.GetBookmarkedPageName(_pathForSettings);
                    if (bookmarkPageName != null)
                    {
                        try
                        {
                            _CurrentImageIndex = await _imageCollectionContext.GetIndexFromKeyAsync(bookmarkPageName, SelectedFileSortType.Value, _navigationCts.Token);
                        }
                        catch
                        {
                            _CurrentImageIndex = 0;
                        }
                    }
                }
            }
            else if (mode == NavigationMode.New && !string.IsNullOrEmpty(parsedPageName))
            {
                var unescapedPageName = parsedPageName;
                try
                {
                    _CurrentImageIndex = await _imageCollectionContext.GetIndexFromKeyAsync(parsedPageName, SelectedFileSortType.Value, _navigationCts.Token);
                }
                catch
                {
                    _CurrentImageIndex = 0;
                }
            }
            else if (mode == NavigationMode.New && !string.IsNullOrEmpty(parsedArchiveFolderName))
            {
                var unescapedFolderName = parsedArchiveFolderName;
                try
                {
                    _CurrentImageIndex = await _imageCollectionContext.GetIndexFromKeyAsync(parsedArchiveFolderName, SelectedFileSortType.Value, _navigationCts.Token);
                }
                catch
                {
                    _CurrentImageIndex = 0;
                }
            }


            _nowCurrenImageIndexChanging = true;
            RaisePropertyChanged(nameof(CurrentImageIndex));
            _nowCurrenImageIndexChanging = false;

            await ResetImageIndex(CurrentImageIndex);

            SetCurrentDisplayImageIndex(CurrentDisplayImageIndex);

            IsAlreadySetDisplayImages = true;

            // 表示画像が揃ったら改めてボタンを有効化
            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();

            // 画像更新
            Observable.Merge(
                this.ObserveProperty(x => x.CurrentImageIndex, isPushCurrentValueAtFirst: true).ToUnit(),
                this.ObserveProperty(x => x.NowDoubleImageView, isPushCurrentValueAtFirst: false).ToUnit()
                )
                .Subscribe(_ =>
                {
                    var ct = _imageLoadingCts.Token;
                    //using (_imageLoadingLock.LockAsync(ct))
                    {
                        if (Images == null) { return; }
                        if (_imageCollectionContext is null) { return; }
                        int imageIndex = CurrentImageIndex;
                        var imageSources = GetSourceImages(PrefetchIndexType.Current);
                        UpdateDisplayName(imageSources);

                        _currentDisplayImageSources ??= new IImageSource[2];
                        if (imageSources.Length == 1)
                        {
                            _currentDisplayImageSources[0] = imageSources[0];
                            _currentDisplayImageSources[1] = null;

                            Page1Favorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, _currentDisplayImageSources[0].Path);
                            Page2Favorite = false;
                        }
                        else if (imageSources.Length == 2)
                        {
                            _currentDisplayImageSources[0] = imageSources[0];
                            _currentDisplayImageSources[1] = imageSources[1];

                            Page1Favorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, _currentDisplayImageSources[0].Path);
                            Page2Favorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, _currentDisplayImageSources[1].Path);
                        }

                        RaisePropertyChanged(nameof(CurrentDisplayImageSources));

                        var imageSource = imageSources[0];
                        if (_currentFolderItem is IStorageItem)
                        {
                            _bookmarkManager.AddBookmark(_pathForSettings, imageSource.Name, new NormalizedPagePosition(Images.Length, imageIndex));
                            _folderLastIntractItemManager.SetLastIntractItemName(_pathForSettings, imageSource.Path);
                        }
                        else if (_currentFolderItem is AlbamEntry albam)
                        {
                            _folderLastIntractItemManager.SetLastIntractItemName(albam._id, imageSource.Path);
                        }
                    }
                }).AddTo(_navigationDisposables);

            SelectedFileSortType
                .Pairwise()
                .Subscribe(async pair =>
                {
                    if (Images == null) { return; }
                    var ct = _navigationCts.Token;
                    var oldImage = await _imageCollectionContext.GetImageFileAtAsync(CurrentImageIndex, pair.OldItem, ct);
                    var newIndex = await _imageCollectionContext.GetIndexFromKeyAsync(oldImage.Name, pair.NewItem, ct);
                    await ResetImageIndex(newIndex);
                })
                .AddTo(_navigationDisposables);

            _SizeChangedSubject
                .Where(x => Images != null)
                .Select(x => (X: CanvasWidth.Value, Y:CanvasHeight.Value))
                .Pairwise()
                .Where(x => x.NewItem != x.OldItem)
                .Do(_ => NowImageLoadingLongRunning = true)
                .Throttle(TimeSpan.FromMilliseconds(50), _scheduler)
                .Subscribe(async size =>
                {
                    using (await _imageLoadingLock.LockAsync(CancellationToken.None))
                    {
                        ClearCachedImages();
                        ClearDisplayImages(PrevDisplayImageIndex);
                        ClearDisplayImages(NextDisplayImageIndex);
                        RaisePropertyChanged(DisplayImageIndexToName(PrevDisplayImageIndex));
                        RaisePropertyChanged(DisplayImageIndexToName(NextDisplayImageIndex));
                    }

                    await ResetImageIndex(CurrentImageIndex);
                })
                .AddTo(_navigationDisposables);

            ImageViewerSettings.ObserveProperty(x => x.IsEnablePrefetch, isPushCurrentValueAtFirst: false)
                .Subscribe(async isEnabledPrefetch => 
                {
                    if (isEnabledPrefetch)
                    {
                        await PrefetchDisplayImagesAsync(IndexMoveDirection.Refresh, CurrentImageIndex, _imageLoadingCts.Token);
                    }
                })
                .AddTo(_navigationDisposables);


            _messenger.Register<AlbamItemRemovedMessage>(this, (r, m) => 
            {
                var (albamId, path) = m.Value;
                if (albamId == FavoriteAlbam.FavoriteAlbamId)
                {
                    if (_currentDisplayImageSources[0] != null)
                    {
                        Page1Favorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, _currentDisplayImageSources[0].Path);
                    }

                    if (_currentDisplayImageSources[1] != null)
                    {
                        Page2Favorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, _currentDisplayImageSources[1].Path);
                    }
                }
            });

            _messenger.Register<AlbamItemAddedMessage>(this, (r, m) =>
            {
                var (albamId, path) = m.Value;
                if (albamId == FavoriteAlbam.FavoriteAlbamId)
                {
                    if (_currentDisplayImageSources[0] != null)
                    {
                        Page1Favorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, _currentDisplayImageSources[0].Path);
                    }

                    if (_currentDisplayImageSources[1] != null)
                    {
                        Page2Favorite = _albamRepository.IsExistAlbamItem(FavoriteAlbam.FavoriteAlbamId, _currentDisplayImageSources[1].Path);
                    }
                }
            });

            if (_imageCollectionContext?.IsSupportedFolderContentsChanged ?? false)
            {
                // アプリ内部操作も含めて変更を検知する
                bool requireRefresh = false;
                _imageCollectionContext.CreateImageFileChangedObserver()
                    .Subscribe(_ =>
                    {
                        requireRefresh = true;
                        Debug.WriteLine("Images Update required. " + _currentPath);
                    })
                    .AddTo(_navigationDisposables);

                ApplicationLifecycleObservable.WindowActivationStateChanged()
                    .Subscribe(async visible =>
                    {
                        if (visible && requireRefresh && _imageCollectionContext is not null)
                        {
                            var ct = _navigationCts?.Token ?? CancellationToken.None;
                            requireRefresh = false;
                            var currentItemPath = (await _imageCollectionContext.GetImageFileAtAsync(CurrentImageIndex, SelectedFileSortType.Value, ct)).Path;
                            await ReloadItemsAsync(_imageCollectionContext, ct);

                            try
                            {
                                var index = await _imageCollectionContext.GetIndexFromKeyAsync(currentItemPath, SelectedFileSortType.Value, ct);
                                await ResetImageIndex(index >= 0 ? index : 0);
                            }
                            catch
                            {
                                if (await _imageCollectionContext.GetImageFileCountAsync(ct) > 0)
                                {
                                    await ResetImageIndex(0);
                                }
                            }


                            Debug.WriteLine("Images Updated. " + _currentPath);
                        }
                    })
                    .AddTo(_navigationDisposables);
            }

            await base.OnNavigatedToAsync(parameters);
        }

        bool _nowPageFolderNameChanging = false;
        void UpdateDisplayName(IImageSource[] imageSources)
        {
            _nowPageFolderNameChanging = true;
            try
            {
                if (imageSources.Length >= 1)
                {
                    var imageSource = imageSources[0];
                    if (imageSource is ArchiveEntryImageSource)
                    {
                        var names = imageSource.Path.Split(SeparateChars);
                        Page1Name = names[names.Length - 1];
                        PageFolderName = (names.Length >= 2 ? names[names.Length - 2] : string.Empty);
                    }
                    else
                    {
                        Page1Name = imageSource.Name;
                    }
                }

                if (imageSources.Length >= 2)
                {
                    var imageSource = imageSources[1];
                    if (imageSource is ArchiveEntryImageSource)
                    {
                        var names = imageSource.Path.Split(SeparateChars);
                        Page2Name = names[names.Length - 1];
                    }
                    else
                    {
                        Page2Name = imageSource.Name;
                    }
                }
            }
            finally
            {
                _nowPageFolderNameChanging = false;
            }
        }


        private BitmapImage[] _DisplayImages_0;
        public BitmapImage[] DisplayImages_0
        {
            get { return _DisplayImages_0; }
            set { SetProperty(ref _DisplayImages_0, value); }
        }

        private BitmapImage[] _DisplayImages_1;
        public BitmapImage[] DisplayImages_1
        {
            get { return _DisplayImages_1; }
            set { SetProperty(ref _DisplayImages_1, value); }
        }

        private BitmapImage[] _DisplayImages_2;
        public BitmapImage[] DisplayImages_2
        {
            get { return _DisplayImages_2; }
            set { SetProperty(ref _DisplayImages_2, value); }
        }


        BitmapImage _emptyImage = new BitmapImage();

        async Task ResetImageIndex(int requestIndex)
        {
            await MoveImageIndex(IndexMoveDirection.Refresh, requestIndex);
        }





        async Task MoveImageIndex(IndexMoveDirection direction, int? request = null)
        {
            if (Images == null || Images.Length == 0) { return; }
            
            _imageLoadingCts?.Cancel();
            _imageLoadingCts?.Dispose();
            _imageLoadingCts = new CancellationTokenSource();

            var ct = _imageLoadingCts.Token;
            try
            {
                using (await _imageLoadingLock.LockAsync(ct))
                {
                    try
                    {
                        // IndexMoveDirection.Backward 時は CurrentIndex - 1 の位置を起点に考える
                        // DoubleViewの場合は CurrentIndex - 1 -> CurrentIndex - 2 と表示可能かを試す流れ

                        var currentIndex = request ?? CurrentImageIndex;
                        var (movedIndex, displayImageCount, isJumpHeadTail) = await LoadImagesAsync(PrefetchIndexType.Current, direction, currentIndex, ct);

                        // 最後尾から先頭にジャンプした場合に音を鳴らす
                        if (isJumpHeadTail)
                        {
                            ElementSoundPlayer.State = ElementSoundPlayerState.On;
                            ElementSoundPlayer.Volume = 1.0;
                            ElementSoundPlayer.Play(ElementSoundKind.Invoke);

                            _ = Task.Delay(500).ContinueWith(prevTask =>
                            {
                                _scheduler.Schedule(async () =>
                                {
                                    using (await _imageLoadingLock.LockAsync(CancellationToken.None))
                                    {
                                        ElementSoundPlayer.State = ElementSoundPlayerState.Auto;
                                    }
                                });
                            });
                        }

                        NowDoubleImageView = displayImageCount == 2;

                        _nowCurrenImageIndexChanging = true;
                        CurrentImageIndex = movedIndex;
                        _nowCurrenImageIndexChanging = false;

                        NowImageLoadingLongRunning = false;

                        await _messenger.Send(new ImageLoadedMessage());

                        await PrefetchDisplayImagesAsync(direction, movedIndex, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        int requestImageCount = IsDoubleViewEnabled.Value ? 2 : 1;
                        CurrentImageIndex = direction switch
                        {
                            IndexMoveDirection.Refresh => CurrentImageIndex,
                            IndexMoveDirection.Forward => Math.Min(CurrentImageIndex + requestImageCount, Images.Length - 1),
                            IndexMoveDirection.Backward => Math.Max(CurrentImageIndex - requestImageCount, 0),
                            _ => throw new NotSupportedException(),
                        };
                        NowImageLoadingLongRunning = true;
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.ToString());
                    }
                }
            }
            catch (OperationCanceledException)
            {

            }
        }






        bool _nowCurrenImageIndexChanging;


        static int GetMoveDirection(IndexMoveDirection direction)
        {
            return direction switch
            {
                IndexMoveDirection.Refresh => 0,
                IndexMoveDirection.Forward => 1,
                IndexMoveDirection.Backward => -1,
                _ => throw new NotSupportedException()
            };
        }
        static (int position, bool IsJumpHeadTail) GetMovedImageIndex(IndexMoveDirection direction, int currentIndex, int totalImagesCount)
        {
            // 読み込むべきインデックスを先に洗い出す
            int directionMoveIndex = GetMoveDirection(direction);
            int rawRequestFirstIndex = (currentIndex) + directionMoveIndex;

            int requestIndex;
            bool isHeadTailJump = false;
            // 表示位置のWrap処理
            if (rawRequestFirstIndex < 0)
            {
                requestIndex = totalImagesCount - 1;
                isHeadTailJump = true;
            }
            else if (rawRequestFirstIndex >= totalImagesCount)
            {
                requestIndex = 0;
                isHeadTailJump = true;
            }
            else
            {
                requestIndex = rawRequestFirstIndex;
            }

            return (requestIndex, isHeadTailJump);
        }

        async ValueTask<int> SetDisplayImagesAsync(PrefetchIndexType indexType, IndexMoveDirection direction, int requestIndex, bool requestDoubleView, CancellationToken ct)
        {
            bool canNotSwapping = indexType != PrefetchIndexType.Current;
            if (requestDoubleView)
            {
                var indexies = direction == IndexMoveDirection.Backward
                    ? new[] { 0, -1 }
                    : new[] { 0, 1 }
                    ;

                // 表示用のインデックスを生成
                // 後ろ方向にページ移動していた場合は1 -> 0のように逆順の並びにすることで
                // 見開きページかつ横長ページを表示しようとしたときに後ろ方向の一個前だけを選択して表示できるようにしている
                List<IImageSource> candidateImages = new List<IImageSource>();
                foreach (var index in indexies)
                {
                    var candidateIndex = requestIndex + index;
                    if (0 <= candidateIndex && candidateIndex < _Images.Length)
                    {
                        candidateImages.Add(await _imageCollectionContext.GetImageFileAtAsync(candidateIndex, SelectedFileSortType.Value, ct));
                    }
                }
                
                if (candidateImages.Any() is false)
                {
                    throw new InvalidOperationException();
                }

                var sizeCheckResult = await CheckImagesCanDoubleViewInCurrentCanvasSizeAsync(candidateImages, ct);
                if (sizeCheckResult.CanDoubleView)
                {
                    if (direction == IndexMoveDirection.Backward)
                    {
                        if (canNotSwapping || !TryDisplayImagesSwapBackward(sizeCheckResult.Slot2Image, sizeCheckResult.Slot1Image))
                        {
                            SetDisplayImages(indexType,
                                sizeCheckResult.Slot2Image, await GetBitmapImageWithCacheAsync(sizeCheckResult.Slot2Image, ct),
                                sizeCheckResult.Slot1Image, await GetBitmapImageWithCacheAsync(sizeCheckResult.Slot1Image, ct)
                                );
                        }
                    }
                    else if (direction == IndexMoveDirection.Forward)
                    {
                        if (canNotSwapping || !TryDisplayImagesSwapForward(sizeCheckResult.Slot1Image, sizeCheckResult.Slot2Image))
                        {
                            SetDisplayImages(indexType,
                                sizeCheckResult.Slot1Image, await GetBitmapImageWithCacheAsync(sizeCheckResult.Slot1Image, ct),
                                sizeCheckResult.Slot2Image, await GetBitmapImageWithCacheAsync(sizeCheckResult.Slot2Image, ct)
                                );
                        }
                    }
                    else
                    {
                        SetDisplayImages(indexType,
                            sizeCheckResult.Slot1Image, await GetBitmapImageWithCacheAsync(sizeCheckResult.Slot1Image, ct),
                            sizeCheckResult.Slot2Image, await GetBitmapImageWithCacheAsync(sizeCheckResult.Slot2Image, ct)
                            );
                    }

                    return 2;
                }
                else
                {
                    if (direction == IndexMoveDirection.Backward)
                    {
                        if (canNotSwapping || !TryDisplayImagesSwapBackward(sizeCheckResult.Slot1Image))
                        {
                            SetDisplayImages(indexType,
                                sizeCheckResult.Slot1Image, await GetBitmapImageWithCacheAsync(sizeCheckResult.Slot1Image, ct)
                                );
                        }
                    }
                    else if (direction == IndexMoveDirection.Forward)
                    {
                        if (canNotSwapping || !TryDisplayImagesSwapForward(sizeCheckResult.Slot1Image))
                        {
                            SetDisplayImages(indexType,
                                sizeCheckResult.Slot1Image, await GetBitmapImageWithCacheAsync(sizeCheckResult.Slot1Image, ct)
                                );
                        }
                    }
                    else
                    {
                        SetDisplayImages(indexType,
                                sizeCheckResult.Slot1Image, await GetBitmapImageWithCacheAsync(sizeCheckResult.Slot1Image, ct)
                                );
                    }

                    return 1;
                }
            }
            else
            {
                var image = await _imageCollectionContext.GetImageFileAtAsync(requestIndex, SelectedFileSortType.Value, ct);
                if (image == null)
                {
                    throw new InvalidOperationException();
                }

                if (direction == IndexMoveDirection.Backward)
                {
                    if (canNotSwapping || !TryDisplayImagesSwapBackward(image))
                    {
                        SetDisplayImages(indexType,
                            image, await GetBitmapImageWithCacheAsync(image, ct)
                            );
                    }
                }
                else if (direction == IndexMoveDirection.Forward)
                {
                    if (canNotSwapping || !TryDisplayImagesSwapForward(image))
                    {
                        SetDisplayImages(indexType,
                            image, await GetBitmapImageWithCacheAsync(image, ct)
                            );
                    }
                }
                else
                {
                    SetDisplayImages(indexType,
                            image, await GetBitmapImageWithCacheAsync(image, ct)
                            );
                }

                return 1;
            }
        }

        async ValueTask<(int movedIndex, int DisplayImageCount, bool IsJumpHeadTail)> LoadImagesAsync(PrefetchIndexType prefetchIndexType, IndexMoveDirection direction, int currentIndex, CancellationToken ct)
        {
            int requestImageCount = IsDoubleViewEnabled.Value ? 2 : 1;
            int lastRequestImageCount = GetCurrentDisplayImageCount();

            var (requestIndex, isJumpHeadTail) = GetMovedImageIndex(direction, currentIndex, Images.Length);
            if (lastRequestImageCount == 2)
            {
                if (direction == IndexMoveDirection.Forward && !isJumpHeadTail)
                {
                    (requestIndex, isJumpHeadTail) = GetMovedImageIndex(direction, requestIndex, Images.Length);
                }
                else if (direction == IndexMoveDirection.Backward && requestIndex == 0)
                {
                    if (currentIndex == 1)
                    {
                        requestImageCount = 1;
                    }
                    else
                    {
                        requestIndex = 1;
                    }
                }
            }

            var displayImageCount = await SetDisplayImagesAsync(prefetchIndexType, direction, requestIndex, requestImageCount == 2, ct);

            return (displayImageCount == 2 && direction == IndexMoveDirection.Backward ? requestIndex - 1 : requestIndex, displayImageCount, isJumpHeadTail);
        }



        public enum IndexMoveDirection
        {
            Refresh,
            Forward,
            Backward,
        }

        struct ImageDoubleViewCulcResult
        {
            public bool CanDoubleView = false;
            public IImageSource Slot1Image;
            public IImageSource Slot2Image;
        }

        private async ValueTask<ImageDoubleViewCulcResult> CheckImagesCanDoubleViewInCurrentCanvasSizeAsync(IEnumerable<IImageSource> candidateImages, CancellationToken ct)
        {
            if (IsDoubleViewEnabled.Value)
            {
                if (candidateImages.Count() == 1)
                {
                    return new ImageDoubleViewCulcResult() { CanDoubleView = false, Slot1Image = candidateImages.First() };
                }
                else
                {
                    var canvasSize = new Vector2((float)CanvasWidth.Value, (float)CanvasHeight.Value);

                    Debug.WriteLine(canvasSize);
                    var firstImage = candidateImages.ElementAt(0);
                    ThumbnailManager.ThumbnailSize? firstImageSize = firstImage.GetThumbnailSize();
                    var secondImage = candidateImages.ElementAt(1);
                    ThumbnailManager.ThumbnailSize? secondImageSize = secondImage.GetThumbnailSize();

                    bool canDoubleView = false;
                    if (firstImageSize is not null and ThumbnailManager.ThumbnailSize fistImageSizeReal 
                        && secondImageSize is not null and ThumbnailManager.ThumbnailSize secondImageSizeReal)
                    {
                        canDoubleView = CanInsideSameHeightAsLarger(in canvasSize, in fistImageSizeReal, in secondImageSizeReal);
                    }
                    else if (firstImageSize is not null and ThumbnailManager.ThumbnailSize firstImageSizeReal2)
                    {
                        canDoubleView = CanInsideSameHeightAsLarger(in canvasSize, in firstImageSizeReal2, await GetThumbnailSizeAsync(secondImage, ct));
                    }
                    else if (secondImageSize is not null and ThumbnailManager.ThumbnailSize secondImageSizeReal2)
                    {
                        canDoubleView = CanInsideSameHeightAsLarger(in canvasSize, await GetThumbnailSizeAsync(firstImage, ct), in secondImageSizeReal2);
                    }
                    else
                    {
                        canDoubleView = CanInsideSameHeightAsLarger(in canvasSize, await GetThumbnailSizeAsync(firstImage, ct), await GetThumbnailSizeAsync(secondImage, ct));
                    }

                    return canDoubleView
                        ? new ImageDoubleViewCulcResult() { CanDoubleView = canDoubleView, Slot1Image = firstImage, Slot2Image = secondImage }
                        : new ImageDoubleViewCulcResult() { CanDoubleView = canDoubleView, Slot1Image = firstImage }
                        ;
                }
            }
            else
            {
                return new ImageDoubleViewCulcResult() { CanDoubleView = false, Slot1Image = candidateImages.First() };
            }
        }

        private static bool CanInsideSameHeightAsLarger(in Vector2 canvasSize, in ThumbnailManager.ThumbnailSize firstImageSize, in ThumbnailManager.ThumbnailSize secondImageSize)
        {
            float firstImageScaledWidth = (canvasSize.Y / firstImageSize.Height) * firstImageSize.Width;
            float secondImageScaledWidth = (canvasSize.Y / secondImageSize.Height) * secondImageSize.Width;
            return canvasSize.X > (firstImageScaledWidth + secondImageScaledWidth);
        }


        private async ValueTask<ThumbnailManager.ThumbnailSize> GetThumbnailSizeAsync(IImageSource source, CancellationToken ct)
        {
            return _thumbnailManager.GetThumbnailOriginalSize(source.Path)
                ?? _thumbnailManager.SetThumbnailSize(source.Path, await GetBitmapImageWithCacheAsync(source, ct));
        }
     
        private async ValueTask<BitmapImage> GetBitmapImageWithCacheAsync(IImageSource source, CancellationToken ct)
        {
            var image = _CachedImages.FirstOrDefault(x => x.ImageSource == source);
            if (image != null)
            {
                _CachedImages.Remove(image);
                _CachedImages.Insert(0, image);
            }
            else
            {
                image = new PrefetchImageInfo(source);
                _CachedImages.Insert(0, image);

                if (_CachedImages.Count > 8)
                {
                    var last = _CachedImages.Last();
                    last.Dispose();
                    _CachedImages.Remove(last);
                    if (last.Image != null)
                    {
                        //RemoveFromDisplayImages(last.Image);
                    }

                    Debug.WriteLine($"remove from display cache: {last.ImageSource.Name}");
                }
            }

            return await image.GetBitmapImageAsync(ct);
        }


        private readonly List<PrefetchImageInfo> _CachedImages = new ();

        private void ClearCachedImages()
        {
            _CachedImages.ForEach(x => x.Cancel());
            _CachedImages.Clear();
        }

        private int _currentDisplayImageIndex = 0;

        enum PrefetchIndexType
        {
            Prev,
            Current,
            Next,
        }

        public int CurrentDisplayImageIndex => _currentDisplayImageIndex;
        public int PrevDisplayImageIndex => _currentDisplayImageIndex - 1 < 0 ? 2 : _currentDisplayImageIndex - 1;
        public int NextDisplayImageIndex => _currentDisplayImageIndex + 1 > 2 ? 0 : _currentDisplayImageIndex + 1;

        private static string DisplayImageIndexToName(int index)
        {
            return index switch
            {
                0 => nameof(DisplayImages_0),
                1 => nameof(DisplayImages_1),
                2 => nameof(DisplayImages_2),
                _ => throw new NotSupportedException()
            };
        }

        private void SetCurrentDisplayImageIndex(int index)
        {
            _currentDisplayImageIndex = index;
            RaisePropertyChanged(nameof(CurrentDisplayImageIndex));
            RaisePropertyChanged(nameof(PrevDisplayImageIndex));
            RaisePropertyChanged(nameof(NextDisplayImageIndex));
        }

        private int GetDisplayImageIndex(PrefetchIndexType type)
        {
            return type switch
            {
                PrefetchIndexType.Current => CurrentDisplayImageIndex,
                PrefetchIndexType.Prev => PrevDisplayImageIndex,
                PrefetchIndexType.Next => NextDisplayImageIndex,
                _ => throw new NotSupportedException(),
            };
        }

        BitmapImage[][] _displayImagesSingle = new BitmapImage[][] 
        {
            new BitmapImage[1],
            new BitmapImage[1],
            new BitmapImage[1],
        };
        BitmapImage[][] _displayImagesDouble = new BitmapImage[][] 
        {
            new BitmapImage[2],
            new BitmapImage[2],
            new BitmapImage[2],
        };

        IImageSource[][] _sourceImagesSingle = new IImageSource[][]
        {
            new IImageSource[1],
            new IImageSource[1],
            new IImageSource[1],
        };
        IImageSource[][] _sourceImagesDouble = new IImageSource[][]
        {
            new IImageSource[2],
            new IImageSource[2],
            new IImageSource[2],
        };

        IImageSource[] GetSourceImages(PrefetchIndexType type)
        {
            var index = GetDisplayImageIndex(type);
            return NowDoubleImageView ? _sourceImagesDouble[index] : _sourceImagesSingle[index];
        }

        public async Task DisableImageDecodeWhenImageSmallerCanvasSize()
        {
            var ct = _imageLoadingCts.Token;
            try
            {
                using (await _imageLoadingLock.LockAsync(ct))
                {
                    // 現在表示中の画像がデコード済みだった場合だけ、デコードしていない画像として読み込む

                    var images = GetDisplayImages(PrefetchIndexType.Current);
                    if (images.Any(x => x == null) || images.All(x => x.DecodePixelHeight == 0 && x.DecodePixelWidth == 0))
                    {
                        return;
                    }

                    var indexType = GetDisplayImageIndex(PrefetchIndexType.Current);
                    if (NowDoubleImageView)
                    {
                        BitmapImage image1 = images[0];
                        BitmapImage image2 = images[1];

                        var imageSource1 = _sourceImagesDouble[indexType][0];
                        var imageSource2 = _sourceImagesDouble[indexType][1];

                        if (image1.DecodePixelHeight != 0)
                        {
                            using var loader1 = new PrefetchImageInfo(imageSource1);
                            image1 = await loader1.GetBitmapImageAsync(ct);
                            Debug.WriteLine($"Reload with no decode pixel : {imageSource1.Name}");
                        }
                        if (image2.DecodePixelHeight != 0)
                        {
                            using var loader2 = new PrefetchImageInfo(imageSource2);
                            image2 = await loader2.GetBitmapImageAsync(ct);
                            Debug.WriteLine($"Reload with no decode pixel : {imageSource2.Name}");
                        }

                        SetDisplayImages_Internal(PrefetchIndexType.Current,
                            imageSource1, image1,
                            imageSource2, image2
                            );
                    }
                    else
                    {
                        var imageSource1 = _sourceImagesSingle[indexType][0];
                        using var loader1 = new PrefetchImageInfo(imageSource1);
                        SetDisplayImages_Internal(PrefetchIndexType.Current,
                            imageSource1, await loader1.GetBitmapImageAsync(ct)
                            );
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        
        private void SetDisplayImages(PrefetchIndexType type, IImageSource firstSource, BitmapImage firstImage)
        {
            static void SetDecodePixelSize(BitmapImage image, float canvasWidth, float canvasHeight)
            {
                if (image.DecodePixelWidth == 0 && image.DecodePixelHeight == 0)
                {
                    if (image.PixelWidth > canvasWidth || image.PixelHeight > canvasHeight)
                    {
                        // 最適化を期待してVector2で計算
                        //var imageRatioWH = image.PixelWidth / (float)image.PixelHeight;
                        //var canvasRatioWH = CanvasWidth.Value / (float)CanvasHeight.Value;
                        //if (imageRatioWH > canvasRatioWH)
                        Vector2 vector = new Vector2((float)image.PixelWidth, (float)canvasWidth) / new Vector2((float)image.PixelHeight, (float)canvasHeight);
                        if (vector.X > vector.Y)
                        {
                            image.DecodePixelWidth = (int)canvasWidth;
                            Debug.WriteLine($"decode to width {image.PixelWidth} -> {image.DecodePixelWidth}");
                        }
                        else
                        {
                            image.DecodePixelHeight = (int)canvasHeight;
                            Debug.WriteLine($"decode to height {image.PixelHeight} -> {image.DecodePixelHeight}");
                        }
                    }
                }
            }

            SetDecodePixelSize(firstImage, (float)CanvasWidth.Value, (float)CanvasHeight.Value);

            SetDisplayImages_Internal(type, firstSource, firstImage);
        }

        private void SetDisplayImages_Internal(PrefetchIndexType type, IImageSource firstSource, BitmapImage firstImage)
        {
            switch (GetDisplayImageIndex(type))
            {
                case 0:
                    _DisplayImages_0 = _displayImagesSingle[0];
                    _DisplayImages_0[0] = firstImage;
                    _sourceImagesSingle[0][0] = firstSource;
                    RaisePropertyChanged(nameof(DisplayImages_0));
                    break;
                case 1:
                    _DisplayImages_1 = _displayImagesSingle[1];
                    _DisplayImages_1[0] = firstImage;
                    _sourceImagesSingle[1][0] = firstSource;
                    RaisePropertyChanged(nameof(DisplayImages_1));
                    break;
                case 2:
                    _DisplayImages_2 = _displayImagesSingle[2];
                    _DisplayImages_2[0] = firstImage;
                    _sourceImagesSingle[2][0] = firstSource;
                    RaisePropertyChanged(nameof(DisplayImages_2));
                    break;
            }
        }

        private void SetDisplayImages(PrefetchIndexType type, IImageSource firstSource, BitmapImage firstImage, IImageSource secondSource, BitmapImage secondImage)
        {
            if (IsLeftBindingEnabled.Value is false)
            {
                (firstImage, secondImage) = (secondImage, firstImage);
                (firstSource, secondSource) = (secondSource, firstSource);
            }

            // (firstImage.PixelWidth + secondImage.PixelWidth < CanvasWidth.Value) は常にtrue
            SetDecodePixelHeightWhenLargerThenCanvasHeight(firstImage);
            SetDecodePixelHeightWhenLargerThenCanvasHeight(secondImage);

            SetDisplayImages_Internal(type, firstSource, firstImage, secondSource, secondImage);            
        }

        private void SetDecodePixelHeightWhenLargerThenCanvasHeight(BitmapImage image)
        {
            if (image.PixelHeight > CanvasHeight.Value)
            {
                image.DecodePixelHeight = (int)CanvasHeight.Value;
            }
        }

        private void SetDisplayImages_Internal(PrefetchIndexType type, IImageSource firstSource, BitmapImage firstImage, IImageSource secondSource, BitmapImage secondImage)
        {
            switch (GetDisplayImageIndex(type))
            {
                case 0:
                    _DisplayImages_0 = _displayImagesDouble[0];
                    _DisplayImages_0[0] = firstImage;
                    _DisplayImages_0[1] = secondImage;
                    _sourceImagesDouble[0][0] = firstSource;
                    _sourceImagesDouble[0][1] = secondSource;
                    RaisePropertyChanged(nameof(DisplayImages_0));
                    break;
                case 1:
                    _DisplayImages_1 = _displayImagesDouble[1];
                    _DisplayImages_1[0] = firstImage;
                    _DisplayImages_1[1] = secondImage;
                    _sourceImagesDouble[1][0] = firstSource;
                    _sourceImagesDouble[1][1] = secondSource;
                    RaisePropertyChanged(nameof(DisplayImages_1));
                    break;
                case 2:
                    _DisplayImages_2 = _displayImagesDouble[2];
                    _DisplayImages_2[0] = firstImage;
                    _DisplayImages_2[1] = secondImage;
                    _sourceImagesDouble[2][0] = firstSource;
                    _sourceImagesDouble[2][1] = secondSource;
                    RaisePropertyChanged(nameof(DisplayImages_2));
                    break;
            }
        }

        private BitmapImage[] GetDisplayImages(PrefetchIndexType type)
        {
            return GetDisplayImageIndex(type) switch
            {
                0 => _DisplayImages_0,
                1 => _DisplayImages_1,
                2 => _DisplayImages_2,
                _ => throw new NotSupportedException(),
            };
        }

        private void ClearDisplayImages()
        {
            ClearDisplayImages(0);
            ClearDisplayImages(1);
            ClearDisplayImages(2);
        }

        private void ClearDisplayImages(int displayImageIndex)
        {
            _displayImagesSingle[displayImageIndex][0] = _emptyImage;

            _displayImagesDouble[displayImageIndex][0] = _emptyImage;
            _displayImagesDouble[displayImageIndex][1] = _emptyImage;

            _sourceImagesSingle[displayImageIndex][0] = null;

            _sourceImagesDouble[displayImageIndex][0] = null;
            _sourceImagesDouble[displayImageIndex][1] = null;
        }

        private void RemoveFromDisplayImages(BitmapImage target)
        {
            BitmapImage[][][] images = new BitmapImage[][][]
            {
                _displayImagesSingle,
                _displayImagesDouble,
            };

            foreach (var outerIndex in Enumerable.Range(0, images.Length))
            {
                var middleImages = images[outerIndex];
                foreach (var middleIndex in Enumerable.Range(0, middleImages.Length))
                {
                    var innerImages = middleImages[middleIndex];
                    foreach (var innerIndex in Enumerable.Range(0, innerImages.Length))
                    {
                        if (innerImages[innerIndex] == target)
                        {
                            if (outerIndex == 0)
                            {
                                _sourceImagesSingle[middleIndex][innerIndex] = null;
                            }
                            else
                            {
                                _sourceImagesDouble[middleIndex][innerIndex] = null;
                            }

                            innerImages[innerIndex] = null;
                        }
                    }
                }
            }
        }



        private bool TryDisplayImagesSwapForward(IImageSource firstSource)
        {
            var firstForwardCachedImageSource = _sourceImagesSingle[NextDisplayImageIndex][0];
            if (firstForwardCachedImageSource == null)
            {
                return false;
            }

            if (firstForwardCachedImageSource.Equals(firstSource))
            {
                Debug.WriteLine($"swap display {CurrentDisplayImageIndex} -> {NextDisplayImageIndex}");
                SetCurrentDisplayImageIndex(NextDisplayImageIndex);
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool TryDisplayImagesSwapForward(IImageSource firstSource, IImageSource secondSource)
        {
            var firstForwardCachedImageSource = _sourceImagesDouble[NextDisplayImageIndex][0];
            var secondForwardCachedImageSource = _sourceImagesDouble[NextDisplayImageIndex][1];
            if (firstForwardCachedImageSource == null || secondForwardCachedImageSource == null)
            {
                return false;
            }

            if (IsLeftBindingEnabled.Value is false)
            {
                (firstForwardCachedImageSource, secondForwardCachedImageSource) = (secondForwardCachedImageSource, firstForwardCachedImageSource);
            }

            if (firstForwardCachedImageSource.Equals(firstSource)
                && secondForwardCachedImageSource.Equals(secondSource)
                )
            {
                Debug.WriteLine($"swap display {CurrentDisplayImageIndex} -> {NextDisplayImageIndex}");
                SetCurrentDisplayImageIndex(NextDisplayImageIndex);
                return true;
            }
            else
            {
                return false;
            }
        }



        private bool TryDisplayImagesSwapBackward(IImageSource firstSource)
        {
            var firstForwardCachedImageSource = _sourceImagesSingle[PrevDisplayImageIndex][0];
            if (firstForwardCachedImageSource == null)
            {
                return false;
            }

            if (firstForwardCachedImageSource.Equals(firstSource))
            {
                Debug.WriteLine($"swap display {CurrentDisplayImageIndex} -> {PrevDisplayImageIndex}");
                SetCurrentDisplayImageIndex(PrevDisplayImageIndex);
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool TryDisplayImagesSwapBackward(IImageSource firstSource, IImageSource secondSource)
        {
            var firstForwardCachedImageSource = _sourceImagesDouble[PrevDisplayImageIndex][0];
            var secondForwardCachedImageSource = _sourceImagesDouble[PrevDisplayImageIndex][1];

            if (firstForwardCachedImageSource == null || secondForwardCachedImageSource == null)
            {
                return false;
            }

            if (IsLeftBindingEnabled.Value is false)
            {
                (firstForwardCachedImageSource, secondForwardCachedImageSource) = (secondForwardCachedImageSource, firstForwardCachedImageSource);
            }

            if (firstForwardCachedImageSource.Equals(firstSource)
                && secondForwardCachedImageSource.Equals(secondSource)
                )
            {
                Debug.WriteLine($"swap display {CurrentDisplayImageIndex} -> {PrevDisplayImageIndex}");
                SetCurrentDisplayImageIndex(PrevDisplayImageIndex);
                return true;
            }
            else
            {
                return false;
            }
        }


        async ValueTask PrefetchDisplayImagesAsync(IndexMoveDirection direction, int requestIndex, CancellationToken ct)
        {
            if (ImageViewerSettings.IsEnablePrefetch is false) { return; }

            if (direction is IndexMoveDirection.Refresh or IndexMoveDirection.Forward)
            {
                var (movedIndex, displayImageCount, isJumpHeadTail) = await LoadImagesAsync(PrefetchIndexType.Next, IndexMoveDirection.Forward, requestIndex, ct);
                SetPrefetchDisplayImageSingleWhenNowDoubleView(PrefetchIndexType.Next);
            }
            else if (direction is IndexMoveDirection.Backward)
            {
                var (movedIndex, displayImageCount, isJumpHeadTail) = await LoadImagesAsync(PrefetchIndexType.Prev, IndexMoveDirection.Backward, requestIndex, ct);
            }
        }

        private void SetPrefetchDisplayImageSingleWhenNowDoubleView(PrefetchIndexType type)
        {
            var index = GetDisplayImageIndex(type);
            bool isDoubleView = index switch
            {
                0 => _DisplayImages_0.Length == 2,
                1 => _DisplayImages_1.Length == 2,
                2 => _DisplayImages_2.Length == 2,
                _ => throw new NotSupportedException(),
            };

            if (isDoubleView)
            {
                _displayImagesSingle[index][0] = _displayImagesDouble[index][0];
                _sourceImagesSingle[index][0] = _sourceImagesDouble[index][0];
            }
        }



        private bool _IsAlreadySetDisplayImages = false;
        public bool IsAlreadySetDisplayImages
        {
            get => _IsAlreadySetDisplayImages;
            private set => SetProperty(ref _IsAlreadySetDisplayImages, value);
        }

        private int GetCurrentDisplayImageCount()
        {
            if (IsAlreadySetDisplayImages is false) { return 0; }

            return GetDisplayImageIndex(PrefetchIndexType.Current) switch
            {
                0 => _DisplayImages_0.Length,
                1 => _DisplayImages_1.Length,
                2 => _DisplayImages_2.Length,
                _ => throw new NotImplementedException(),
            };
        }


        IImageCollectionContext _imageCollectionContext;
        CancellationTokenSource _imageLoadingCts;
        Models.Infrastructure.AsyncLock _imageLoadingLock = new ();

        private async Task RefreshItems(CancellationToken ct)
        {
            _ImageEnumerationDisposer?.Dispose();
            _ImageEnumerationDisposer = null;

            IImageCollectionContext imageCollectionContext = null;
            if (_currentFolderItem is StorageFolder folder)
            {
                Debug.WriteLine(folder.Path);
                imageCollectionContext = _imageCollectionManager.GetFolderImageCollectionContext(folder, ct);

                _recentlyAccessManager.AddWatched(_currentPath);
            }
            else if (_currentFolderItem is StorageFile file)
            {
                Debug.WriteLine(file.Path);
                if (file.IsSupportedImageFile())
                {
                    try
                    {
                        var parentFolder = await file.GetParentAsync();
                        imageCollectionContext = _imageCollectionManager.GetFolderImageCollectionContext(parentFolder, ct);
                        _recentlyAccessManager.AddWatched(parentFolder.Path);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        _recentlyAccessManager.AddWatched(_currentPath);

                        var parentItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(Path.GetDirectoryName(_currentPath));
                        if (parentItem is StorageFolder parentFolder)
                        {
                            imageCollectionContext = _imageCollectionManager.GetFolderImageCollectionContext(parentFolder, ct);
                        }
                    }
                }
                else if (file.IsSupportedMangaFile())
                {
                    imageCollectionContext = await _imageCollectionManager.GetArchiveImageCollectionContextAsync(file, null, ct);
                    _recentlyAccessManager.AddWatched(file.Path);
                }
            }
            else if (_currentFolderItem is AlbamEntry albam)
            {
                imageCollectionContext = new AlbamImageCollectionContext(albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _folderListingSettings, _thumbnailManager, _messenger);
            }
            else
            {
                throw new NotSupportedException();
            }

            if (imageCollectionContext == null) { return; }

            _ImageEnumerationDisposer = imageCollectionContext as IDisposable;
            _imageCollectionContext = imageCollectionContext;

            ParentFolderOrArchiveName = imageCollectionContext.Name;
            ItemType = SupportedFileTypesHelper.StorageItemToStorageItemTypes(_currentFolderItem);

            var name = _currentFolderItem switch
            {
                IStorageItem storageItem => storageItem.Name,
                AlbamEntry albam => albam.Name,
                _ => throw new NotSupportedException(),
            };
            _appView.Title = name;
            Title = ItemType == StorageItemTypes.Image ? ParentFolderOrArchiveName : name;

            await ReloadItemsAsync(imageCollectionContext, ct);

            if (await imageCollectionContext.IsExistFolderOrArchiveFileAsync(ct))
            {
                var folders = await imageCollectionContext.GetLeafFoldersAsync(ct).ToListAsync(ct);
                if (folders.Count <= 1)
                {
                    PageFolderNames = new string[0];
                }
                else
                {
                    PageFolderNames = folders.Select(x => x.Name).ToArray();
                }
            }
            else
            {
                PageFolderNames = new string[0];
            }

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();            
        }


        private async Task ReloadItemsAsync(IImageCollectionContext imageCollectionContext, CancellationToken ct)
        {
            Images?.AsParallel().WithDegreeOfParallelism(4).ForEach((IImageSource x) => x.TryDispose());

            var imageCount = await imageCollectionContext.GetImageFileCountAsync(ct);
            _nowCurrenImageIndexChanging = true;
            Images = new IImageSource[imageCount];
            _nowCurrenImageIndexChanging = false;
        }


#region Commands

        public ToggleFullScreenCommand ToggleFullScreenCommand { get; }
        public BackNavigationCommand BackNavigationCommand { get; }
        public FavoriteAddCommand FavoriteAddCommand { get; }
        public FavoriteRemoveCommand FavoriteRemoveCommand { get; }
        public AlbamItemEditCommand AlbamItemEditCommand { get; }
        public ChangeStorageItemThumbnailImageCommand ChangeStorageItemThumbnailImageCommand { get; }
        public OpenWithExplorerCommand OpenWithExplorerCommand { get; }
        public OpenWithExternalApplicationCommand OpenWithExternalApplicationCommand { get; }
        public FavoriteToggleCommand FavoriteToggleCommand { get; }

        private DelegateCommand _GoNextImageCommand;
        public DelegateCommand GoNextImageCommand =>
            _GoNextImageCommand ??= new DelegateCommand(ExecuteGoNextImageCommand, CanGoNextCommand) { IsActive = true };

        private void ExecuteGoNextImageCommand()
        {
            _ = MoveImageIndex(IndexMoveDirection.Forward);
        }

        private bool CanGoNextCommand()
        {
            //return CurrentImageIndex + 1 < Images?.Length;
            return true;
        }

        private DelegateCommand _GoPrevImageCommand;
        public DelegateCommand GoPrevImageCommand =>
            _GoPrevImageCommand ??= new DelegateCommand(ExecuteGoPrevImageCommand, CanGoPrevCommand) { IsActive = true };

        private void ExecuteGoPrevImageCommand()
        {
            _ = MoveImageIndex(IndexMoveDirection.Backward);
        }

        private bool CanGoPrevCommand()
        {
            //return CurrentImageIndex >= 1 && Images?.Length > 0;
            return true;
        }


        ISubject<int> _SizeChangedSubject = new BehaviorSubject<int>(-1);

        private DelegateCommand _SizeChangedCommand;
        public DelegateCommand SizeChangedCommand =>
            _SizeChangedCommand ??= new DelegateCommand(async () =>
            {
                if (!(Images?.Any() ?? false)) { return; }
                
                _SizeChangedSubject.OnNext(CurrentImageIndex);
            });

        private DelegateCommand<string> _changePageFolderCommand;
        public DelegateCommand<string> ChangePageFolderCommand =>
            _changePageFolderCommand ?? (_changePageFolderCommand = new DelegateCommand<string>(ExecuteChangePageFolderCommand));

        async void ExecuteChangePageFolderCommand(string pageName)
        {
            if (string.IsNullOrEmpty(pageName)) { return; }
            if (_nowPageFolderNameChanging) { return; }

            var ct = _navigationCts.Token;
            //using (_imageLoadingLock.LockAsync(ct))
            {
                var folders = await _imageCollectionContext.GetLeafFoldersAsync(ct).ToListAsync(ct);
                var folder = folders
                    .FirstOrDefault(x => x.Name == pageName);
                if (string.IsNullOrEmpty(folder?.Path) is false)
                {
                    var index = await _imageCollectionContext.GetIndexFromKeyAsync(folder.Path, SelectedFileSortType.Value, ct);
                    if (index >= 0)
                    {
                        await ResetImageIndex(index);
                    }
                }
            }
        }

        private DelegateCommand<double?> _ChangePageCommand;
        public DelegateCommand<double?> ChangePageCommand =>
            _ChangePageCommand ?? (_ChangePageCommand = new DelegateCommand<double?>(ExecuteChangePageCommand));

        void ExecuteChangePageCommand(double? parameter)
        {
            if (_nowCurrenImageIndexChanging) { return; }

            _ = ResetImageIndex((int)parameter.Value);
        }

        private DelegateCommand _DoubleViewCorrectCommand;
        public DelegateCommand DoubleViewCorrectCommand =>
            _DoubleViewCorrectCommand ?? (_DoubleViewCorrectCommand = new DelegateCommand(ExecuteDoubleViewCorrectCommand));

        void ExecuteDoubleViewCorrectCommand()
        {
            _ = ResetImageIndex(Math.Max(CurrentImageIndex - 1, 0));
        }



        private DelegateCommand<object> _ChangeFileSortCommand;
        public DelegateCommand<object> ChangeFileSortCommand =>
            _ChangeFileSortCommand ??= new DelegateCommand<object>(async sort =>
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
                    DisplaySortTypeInheritancePath = null;
                    SelectedFileSortType.Value = sortType.Value;
                    if (_currentFolderItem is IStorageItem)
                    {
                        _displaySettingsByPathRepository.SetFolderAndArchiveSettings(_pathForSettings, SelectedFileSortType.Value);
                    }
                    else if (_currentFolderItem is AlbamEntry albam)
                    {
                        _displaySettingsByPathRepository.SetAlbamSettings(albam._id, SelectedFileSortType.Value);
                    }
                }
                else
                {
                    if (_currentFolderItem is IStorageItem)
                    {
                        _displaySettingsByPathRepository.ClearFolderAndArchiveSettings(_pathForSettings);
                        if (_displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(_pathForSettings) is not null and var parentSort
                        && parentSort.ChildItemDefaultSort != null
                        )
                        {
                            DisplaySortTypeInheritancePath = parentSort.Path;
                            SelectedFileSortType.Value = parentSort.ChildItemDefaultSort.Value;
                        }
                        else
                        {
                            DisplaySortTypeInheritancePath = null;
                            SelectedFileSortType.Value = DefaultFileSortType;
                        }
                    }
                    else if (_currentFolderItem is AlbamEntry albam)
                    {
                        _displaySettingsByPathRepository.ClearAlbamSettings(albam._id);
                        DisplaySortTypeInheritancePath = null;
                        SelectedFileSortType.Value = DefaultFileSortType;
                    }
                }
            });

#endregion

#region Single/Double View

        private bool _NowDoubleImageView;
        public bool NowDoubleImageView
        {
            get { return _NowDoubleImageView; }
            set { SetProperty(ref _NowDoubleImageView, value); }
        }

#endregion


    }

    public class PrefetchImageInfo : IDisposable
    {
        public PrefetchImageInfo(IImageSource imageSource)
        {
            ImageSource = imageSource;
        }

        CancellationTokenSource _PrefetchCts = new CancellationTokenSource();

        public BitmapImage Image { get; set; }

        public IImageSource ImageSource { get; set; }

        public bool IsCompleted { get; set; }

        public bool IsCanceled { get; set; }

        static Models.Infrastructure.AsyncLock _prefetchProcessLock = new ();

        public void Cancel()
        {
            IsCanceled = true;
            _PrefetchCts.Cancel();
        }


        public async ValueTask<BitmapImage> GetBitmapImageAsync(CancellationToken ct)
        {
            if (Image != null) { return Image; }

            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_PrefetchCts.Token, ct))
            {
                var linkedCt = linkedCts.Token;
                using (await _prefetchProcessLock.LockAsync(linkedCt))
                {
                    if (Image == null)
                    {
                        var image = new BitmapImage();
                        using (var stream = await ImageSource.GetImageStreamAsync(linkedCt))
                        {
                            await image.SetSourceAsync(stream).AsTask(linkedCt);
                        }
                        Image = image;

                        Debug.WriteLine("image load to memory : " + ImageSource.Name);
                    }

                    IsCompleted = true;

                    return Image;
                }
            }
        }

        public void Dispose()
        {
            ((IDisposable)_PrefetchCts).Dispose();
        }
    }


}
