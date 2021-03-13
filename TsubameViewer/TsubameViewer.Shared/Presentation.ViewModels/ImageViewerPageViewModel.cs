using Microsoft.IO;
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
using TsubameViewer.Models.Domain.Bookmark;
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

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class ImageViewerPageViewModel : ViewModelBase, IDisposable
    {
        private string _currentPath;
        private IStorageItem _currentFolderItem;

        private CancellationTokenSource _leavePageCancellationTokenSource;

        IDisposable _ImageEnumerationDisposer;

        private IImageSource[] _Images;
        public IImageSource[] Images
        {
            get { return _Images; }
            private set { SetProperty(ref _Images, value); }
        }

        private BitmapImage[] _CurrentImages = new BitmapImage[0];
        public BitmapImage[] CurrentImages
        {
            get { return _CurrentImages; }
            set { SetProperty(ref _CurrentImages, value); }
        }

        private int _CurrentImageIndex;
        public int CurrentImageIndex
        {
            get => _CurrentImageIndex;
            set => SetProperty(ref _CurrentImageIndex, value);
        }

        private string _ParentFolderOrArchiveName;
        public string ParentFolderOrArchiveName
        {
            get { return _ParentFolderOrArchiveName; }
            private set { SetProperty(ref _ParentFolderOrArchiveName, value); }
        }

        public IReadOnlyReactiveProperty<int> DisplayCurrentImageIndex { get; }

        public ReactiveProperty<double> CanvasWidth { get; }
        public ReactiveProperty<double> CanvasHeight { get; }




        private string _title;
        public string Title
        {
            get { return _title; }
            private set { SetProperty(ref _title, value); }
        }


        private string _pageFolderName;
        public string PageFolderName
        {
            get { return _pageFolderName; }
            set { SetProperty(ref _pageFolderName, value); }
        }

        private string _pageName;
        public string PageName
        {
            get { return _pageName; }
            private set { SetProperty(ref _pageName, value); }
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

        public ImageViewerSettings ImageViewerSettings { get; }

        private readonly IScheduler _scheduler;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly PathReferenceCountManager _PathReferenceCountManager;
        private readonly ImageCollectionManager _imageCollectionManager;
        private readonly BookmarkManager _bookmarkManager;
        private readonly RecentlyAccessManager _recentlyAccessManager;
        private readonly FolderLastIntractItemManager _folderLastIntractItemManager;
        CompositeDisposable _disposables = new CompositeDisposable();

        public ImageViewerPageViewModel(
            IScheduler scheduler,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            PathReferenceCountManager PathReferenceCountManager,
            ImageCollectionManager imageCollectionManager,
            ImageViewerSettings imageCollectionSettings,
            BookmarkManager bookmarkManager,
            RecentlyAccessManager recentlyAccessManager,
            RecyclableMemoryStreamManager recyclableMemoryStreamManager,
            FolderLastIntractItemManager folderLastIntractItemManager,
            ToggleFullScreenCommand toggleFullScreenCommand,
            BackNavigationCommand backNavigationCommand
            )
        {
            _scheduler = scheduler;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _PathReferenceCountManager = PathReferenceCountManager;
            _imageCollectionManager = imageCollectionManager;
            ImageViewerSettings = imageCollectionSettings;
            ToggleFullScreenCommand = toggleFullScreenCommand;
            BackNavigationCommand = backNavigationCommand;
            _bookmarkManager = bookmarkManager;
            _recentlyAccessManager = recentlyAccessManager;
            _folderLastIntractItemManager = folderLastIntractItemManager;
            DisplayCurrentImageIndex = this.ObserveProperty(x => x.CurrentImageIndex)
                .Select(x => x + 1)
                .Do(_ => 
                {
                    if (Images == null || !Images.Any()) { return; }

                    var imageSource = Images[CurrentImageIndex];
                    var names = imageSource.Name.Split(SeparateChars);
                    PageName = names[names.Length - 1];
                    PageFolderName = names.Length >= 2 ? names[names.Length - 2] : string.Empty;
                    _bookmarkManager.AddBookmark(_currentFolderItem.Path, imageSource.Name, new NormalizedPagePosition(Images.Length, _CurrentImageIndex));
                    _folderLastIntractItemManager.SetLastIntractItemName(_currentFolderItem.Path, imageSource.Name);
                })
                .ToReadOnlyReactivePropertySlim()
                .AddTo(_disposables);

            CanvasWidth = new ReactiveProperty<double>()
                .AddTo(_disposables);
            CanvasHeight = new ReactiveProperty<double>()
                .AddTo(_disposables);

            _appView = ApplicationView.GetForCurrentView();

            _SizeChangedSubject
                .Where(x => x >= 0 && Images != null)
                .Throttle(TimeSpan.FromMilliseconds(50), _scheduler)
                .Subscribe(index => 
                {
                    CalcViewResponsibleImageAmount(CanvasWidth.Value, CanvasHeight.Value);
                    _ = ResetImageIndex(index);
                })
                .AddTo(_disposables);
        }



        public void Dispose()
        {
            _disposables.Dispose();
            _ImageEnumerationDisposer?.Dispose();
        }




        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            ClearPrefetch();

            if (Images?.Any() ?? false)
            {
                Images.ForEach(x => (x as IDisposable)?.Dispose());
                Images = null;
            }

            CurrentImages = null;

            _leavePageCancellationTokenSource.Cancel();
            _leavePageCancellationTokenSource.Dispose();
            _navigationDisposables.Dispose();
            _ImageEnumerationDisposer?.Dispose();
            _ImageEnumerationDisposer = null;
            _imageLoadingCts?.Cancel();
            _imageLoadingCts?.Dispose();

            _appView.Title = String.Empty;
            ParentFolderOrArchiveName = String.Empty;

            base.OnNavigatedFrom(parameters);
        }

        public override void OnNavigatingTo(INavigationParameters parameters)
        {
            Views.PrimaryWindowCoreLayout.SetCurrentNavigationParameters(parameters);

            base.OnNavigatingTo(parameters);
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationDisposables = new CompositeDisposable();
            _leavePageCancellationTokenSource = new CancellationTokenSource()
                .AddTo(_navigationDisposables);

            // 一旦ボタン類を押せないように変更通知
            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();

            var mode = parameters.GetNavigationMode();
            if (mode == NavigationMode.New
                || mode == NavigationMode.Back
                || mode == NavigationMode.Forward
                )
            {
                if (parameters.TryGetValue(PageNavigationConstants.Path, out string path))
                {
                    var unescapedPath = Uri.UnescapeDataString(path);
                    if (_currentPath != unescapedPath)
                    {
                        _currentPath = unescapedPath;

                        // PathReferenceCountManagerへの登録が遅延する可能性がある
                        string token = null;
                        var ext = Path.GetExtension(_currentPath);
                        var directoryPath = Path.GetDirectoryName(_currentPath);
                        var rawPath = _currentPath;
                        foreach (var _ in Enumerable.Repeat(0, 10))
                        {
                            // PathReferenceCountManagerに対して画像ファイルは登録していないため
                            // 画像ファイルの場合は親フォルダのパスを利用する
                            if (SupportedFileTypesHelper.IsSupportedImageFileExtension(ext))
                            {
                                token = _PathReferenceCountManager.GetToken(directoryPath);
                                if (token != null)
                                {
                                    _currentPath = directoryPath;
                                    break;
                                }
                            }

                            // 画像をファイルアクティベーションした場合には
                            // 例外的に画像ファイルのパスが渡される
                            token = _PathReferenceCountManager.GetToken(rawPath);
                            if (token != null)
                            {
                                _currentPath = rawPath;
                                break;
                            }

                            await Task.Delay(100);
                        }

                        foreach (var _ in Enumerable.Repeat(0, 10))
                        {
                            token = _PathReferenceCountManager.GetToken(_currentPath);
                            if (token != null)
                            {
                                break;
                            }
                            await Task.Delay(100);
                        }

                        foreach (var tempToken in _PathReferenceCountManager.GetTokens(_currentPath))
                        {
                            try
                            {
                                _currentFolderItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(tempToken, _currentPath);
                                token = tempToken;
                            }
                            catch
                            {
                                _PathReferenceCountManager.Remove(tempToken);
                            }
                        }

                        Images = default;
                        CurrentImageIndex = 0;
                    }
                }
            }

            // 以下の場合に表示内容を更新する
            //    1. 表示フォルダが変更された場合
            //    2. 前回の更新が未完了だった場合
            if (_currentFolderItem != null)
            {
                await RefreshItems(_leavePageCancellationTokenSource.Token);
            }

            // 表示する画像を決める
            if (mode == NavigationMode.Forward 
                || parameters.ContainsKey(PageNavigationConstants.Restored) 
                || (mode == NavigationMode.New && !parameters.ContainsKey(PageNavigationConstants.PageName))
                )
            {
                var bookmarkPageName = _bookmarkManager.GetBookmarkedPageName(_currentFolderItem.Path);
                if (bookmarkPageName != null)
                {
                    for (var i = 0; i < Images.Length; i++)
                    {
                        if (Images[i].Name == bookmarkPageName)
                        {
                            CurrentImageIndex = i;
                            break;
                        }
                    }
                }
            }
            else if (mode == NavigationMode.New && parameters.ContainsKey(PageNavigationConstants.PageName)
                )
            {
                if (parameters.TryGetValue(PageNavigationConstants.PageName, out string pageName))
                {
                    var unescapedPageName = Uri.UnescapeDataString(pageName);
                    var firstSelectItem = Images.FirstOrDefault(x => x.Name == unescapedPageName);
                    if (firstSelectItem != null)
                    {
                        CurrentImageIndex = Images.IndexOf(firstSelectItem);
                    }
                }

                // TODO: FileSortTypeを受け取って表示順の入れ替えに対応するべきか否か
                //if (parameters.TryGetValue("sort", out string sortMethod))
                {

                }
            }

            // 表示画像が揃ったら改めてボタンを有効化
            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();

            // 画像更新
            new [] 
            {
                ImageViewerSettings.ObserveProperty(x => x.IsEnableSpreadDisplay).ToUnit() 
            }
                .Merge()
                .Throttle(TimeSpan.FromMilliseconds(50), _scheduler)
                .Subscribe(async _ =>
                {
                    CalcViewResponsibleImageAmount(CanvasWidth.Value, CanvasHeight.Value);
                    await ResetImageIndex(CurrentImageIndex);
                })
                .AddTo(_navigationDisposables);

            await base.OnNavigatedToAsync(parameters);
        }

        BitmapImage _emptyImage = new BitmapImage();

        int _nowRequestedImageIndex = 0;
        async Task MoveImageIndex(IndexMoveDirection direction, int? request = null)
        {
            if (Images == null || Images.Length == 0) { return; }

            if (direction != IndexMoveDirection.Backward)
            {
                ClearPrefetch();
            }

            // requestIndex round
            // roundした場合は読み込み数の切り捨て必要

            // 最後と最初の画像は読み込みキャンセルさせない

            if (_nowRequestedImageIndex >= Images.Length - _CurrentImages.Length || _nowRequestedImageIndex <= _CurrentImages.Length)
            {
                _imageLoadingCts ??= new CancellationTokenSource();
            }
            else
            {
                _imageLoadingCts?.Cancel();
                _imageLoadingCts?.Dispose();
                _imageLoadingCts = new CancellationTokenSource();
            }

            var ct = _imageLoadingCts.Token;
            
            using (await _imageLoadingLock.LockAsync(ct))
            {
                try
                {
                    // 読み込むべきインデックスを先に洗い出す
                    var rawRequestIndex = direction switch
                    {
                        IndexMoveDirection.Refresh => request ?? CurrentImageIndex,
                        IndexMoveDirection.Forward => CurrentImageIndex + (_prevForceSingleView ? 1 : _CurrentImages.Length),
                        IndexMoveDirection.Backward => CurrentImageIndex - _CurrentImages.Length,
                        _ => throw new NotSupportedException(),
                    };

                    // 表示位置のWrap処理
                    var requestIndex = rawRequestIndex switch
                    {
                        int i when i < 0 => Images.Length - _CurrentImages.Length,
                        int i when i >= Images.Length => 0,
                        _ => rawRequestIndex
                    };

                    // 最後尾から先頭にジャンプした場合に音を鳴らす
                    if (requestIndex != rawRequestIndex)
                    {
                        ElementSoundPlayer.State = ElementSoundPlayerState.On;
                        ElementSoundPlayer.Volume = 1.0;
                        ElementSoundPlayer.Play(ElementSoundKind.Invoke);

                        _ = Task.Delay(500).ContinueWith(prevTask =>
                        {
                            _scheduler.Schedule(async () => 
                            {
                                using (await _imageLoadingLock.LockAsync(ct))
                                {
                                    ElementSoundPlayer.State = ElementSoundPlayerState.Auto;
                                }
                            });
                        });
                    }


                    _nowRequestedImageIndex = requestIndex;

                    // 表示用のインデックスを生成
                    // 後ろ方向にページ移動していた場合は1 -> 0のように逆順の並びにすることで
                    // 見開きページかつ横長ページを表示しようとしたときに後ろ方向の一個前だけを選択して表示できるようにしている
                    var indexies = Enumerable.Range(0, _CurrentImages.Length);
                    if (direction == IndexMoveDirection.Backward)
                    {
                        indexies = indexies.Reverse();
                    }

                    // Imagesが扱えるindexの範囲に限定
                    indexies = indexies.Where(x => x + requestIndex < Images.Length && x + requestIndex >= 0);

                    if (indexies.Count() == 0) 
                    {
                        return; 
                    }

                    var canvasWidth = (int)CanvasWidth.Value;
                    var canvasHeight = (int)CanvasHeight.Value;


                    int generateImageIndex = -1;
                    bool isForceSingleImageView = false;   
                    
                    // 最後のページを一枚だけで表示する必要がある時、表示しない側の画像を空にする
                    if (_CurrentImages.Length >= 2 && indexies.Count() == 1)
                    {
                        _CurrentImages[1 - indexies.First()] = _emptyImage;
                    }

                    foreach (var i in indexies)
                    {
                        if (isForceSingleImageView)
                        {
                            _CurrentImages[i] = _emptyImage;
                            continue;
                        }

                        if (ct.IsCancellationRequested) { return; }

                        var imageSource = Images[requestIndex + i];
                        var bitmapImage = await MakeBitmapImageAsync(imageSource, canvasWidth, canvasHeight, ct);
                        if (bitmapImage.PixelHeight < bitmapImage.PixelWidth)
                        {
                            isForceSingleImageView = true;

                            // 二枚目以降が横長だった場合は表示しない
                            if (generateImageIndex != -1)
                            {
                                _CurrentImages[1 - i] = _emptyImage;
                                // TODO: 横長画像をスキップした場合に、生成したbitmapImageを後で使い回せるようにしたい
                                break;
                            }
                        }

                        if (ct.IsCancellationRequested) { return; }

                        generateImageIndex = requestIndex + i;

                        _CurrentImages[i] = bitmapImage;
#if DEBUG
                        Debug.WriteLine($"w={_CurrentImages[i].PixelWidth:F2}, h={_CurrentImages[i].PixelHeight:F2}");
#endif
                    }

                    if (generateImageIndex == -1) { throw new Exception(); }

                    if (ct.IsCancellationRequested) { return; }

                    // SliderとCurrentImageIndexの更新が競合することに対処するためのスキップ用の仕掛け
                    _nowCurrenImageIndexChanging = true;
                    if (isForceSingleImageView)
                    {
                        CurrentImageIndex = generateImageIndex;
                        _prevForceSingleView = true;
                    }
                    else
                    {
                        CurrentImageIndex = requestIndex;
                        _prevForceSingleView = false;
                    }
                    _nowCurrenImageIndexChanging = false;


                    NowDoubleImageView = CurrentImages.Count(x => x != null) >= 2;
                    RaisePropertyChanged(nameof(CurrentImages));

                    NowImageLoadingLongRunning = false;

                    // 先行読み込みを仕込む
                    if (direction == IndexMoveDirection.Forward)
                    {
                        if (NowDoubleImageView)
                        {
                            var firstPageImageSource = Images.ElementAtOrDefault(CurrentImageIndex + (isForceSingleImageView ? 1 : 2));
                            if (firstPageImageSource != null)
                            {
                                SetPrefetch(0, firstPageImageSource);

                                var secondPageImageSource = Images.ElementAtOrDefault(CurrentImageIndex + (isForceSingleImageView ? 2 : 3));
                                if (secondPageImageSource != null)
                                {
                                    SetPrefetch(1, secondPageImageSource);
                                }
                            }
                        }
                        else
                        {
                            var firstPageImageSource = Images.ElementAtOrDefault(CurrentImageIndex + 1);
                            if (firstPageImageSource != null)
                            {
                                SetPrefetch(0, firstPageImageSource);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    CurrentImageIndex = direction switch
                    {
                        IndexMoveDirection.Refresh => CurrentImageIndex,
                        IndexMoveDirection.Forward => Math.Min(CurrentImageIndex + _CurrentImages.Length, Images.Length - 1),
                        IndexMoveDirection.Backward => Math.Max(CurrentImageIndex - _CurrentImages.Length, 0),
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

        bool _nowCurrenImageIndexChanging;
        bool _prevForceSingleView;

        async Task ResetImageIndex(int requestIndex)
        {
            _prevForceSingleView = false;
            await MoveImageIndex(IndexMoveDirection.Refresh, requestIndex);
        }

        


        async Task<BitmapImage> MakeBitmapImageAsync(IImageSource imageSource, int canvasWidth, int canvasHeight, CancellationToken ct)
        {
            var bitmapImage = await GetImageIfPrefetched(imageSource) 
                ?? await imageSource.GenerateBitmapImageAsync(ct);

            // 画面より小さい画像を表示するときはアンチエイリアスと省メモリのため画面サイズにまで縮小
            if (bitmapImage.PixelHeight > bitmapImage.PixelWidth)
            {
                if (bitmapImage.PixelHeight > canvasHeight)
                {
                    bitmapImage.DecodePixelHeight = canvasHeight;
                }
            }
            else
            {
                if (bitmapImage.PixelWidth > canvasWidth)
                {
                    bitmapImage.DecodePixelWidth = canvasWidth;
                }
            }

            return bitmapImage;
        }


        public enum IndexMoveDirection
        {
            Refresh,
            Forward,
            Backward,
        }

        CancellationTokenSource _imageLoadingCts;
        FastAsyncLock _imageLoadingLock = new FastAsyncLock();

        private async Task RefreshItems(CancellationToken ct)
        {
            _ImageEnumerationDisposer?.Dispose();
            _ImageEnumerationDisposer = null;

            var result = await _imageCollectionManager.GetImageSourcesForImageViewerAsync(_currentFolderItem);
            if (result != null)
            {
                Images = result.Images;
                CurrentImageIndex = result.FirstSelectedIndex;
                _ImageEnumerationDisposer = result.ItemsEnumeratorDisposer;
                ParentFolderOrArchiveName = result.ParentFolderOrArchiveName;

                if (_currentFolderItem is StorageFolder ||
                    (_currentFolderItem is StorageFile file && SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType))
                    )
                {
                    PageFolderNames = Images.Select(x => SeparateChars.Any(sc => x.Name.Contains(sc)) ? x.Name.Split(SeparateChars).TakeLast(2).First() : string.Empty).Distinct().Where(x => !string.IsNullOrEmpty(x)).ToArray();
                }
                else
                {
                    PageFolderNames = new string[0];
                }


                ItemType = SupportedFileTypesHelper.StorageItemToStorageItemTypes(_currentFolderItem);

                _appView.Title = _currentFolderItem.Name;
                Title = ItemType == StorageItemTypes.Image ? ParentFolderOrArchiveName : _currentFolderItem.Name;

                GoNextImageCommand.RaiseCanExecuteChanged();
                GoPrevImageCommand.RaiseCanExecuteChanged();

                _recentlyAccessManager.AddWatched(_currentPath, DateTimeOffset.Now);
            }
        }


        #region Prefetch Images


        PrefetchImageInfo[] _PrefetchImageDatum = new PrefetchImageInfo[2];
        
        void SetPrefetch(int slot, IImageSource imageSource)
        {
            ClearPrefetch(slot);
            if (imageSource == null) { return; }
            _PrefetchImageDatum[slot] = new PrefetchImageInfo(imageSource);
            _ = _PrefetchImageDatum[slot].StartPrefetchAsync();
        }

        async Task<BitmapImage> GetImageIfPrefetched(IImageSource imageSource)
        {
            var prefetch = _PrefetchImageDatum.FirstOrDefault(x => x?.ImageSource == imageSource);
            if (prefetch == null || prefetch.IsCanceled) { return null; }

            return await prefetch.StartPrefetchAsync();
        }

        void ClearPrefetch(int slot)
        {
            _PrefetchImageDatum[slot]?.Cancel();
            _PrefetchImageDatum[slot]?.Dispose();
            _PrefetchImageDatum[slot] = null;
        }

        void ClearPrefetch()
        {
            ClearPrefetch(0);
            ClearPrefetch(1);
        }

        
        #endregion


        #region Commands

        public ToggleFullScreenCommand ToggleFullScreenCommand { get; }
        public BackNavigationCommand BackNavigationCommand { get; }

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

        void ExecuteChangePageFolderCommand(string pageName)
        {
            var pageFirstItem = Images.FirstOrDefault(x => x.Name.Contains(pageName));
            if (pageFirstItem == null) { return; }

            var pageFirstItemIndex = Images.IndexOf(pageFirstItem);
            _ = ResetImageIndex(pageFirstItemIndex);
        }

        private DelegateCommand<double?> _ChangePageCommand;
        public DelegateCommand<double?> ChangePageCommand =>
            _ChangePageCommand ?? (_ChangePageCommand = new DelegateCommand<double?>(ExecuteChangePageCommand));

        async void ExecuteChangePageCommand(double? parameter)
        {
            if (_nowCurrenImageIndexChanging) { return; }

            await ResetImageIndex((int)parameter.Value);
        }

        private DelegateCommand _DoubleViewCorrectCommand;
        public DelegateCommand DoubleViewCorrectCommand =>
            _DoubleViewCorrectCommand ?? (_DoubleViewCorrectCommand = new DelegateCommand(ExecuteDoubleViewCorrectCommand));

        void ExecuteDoubleViewCorrectCommand()
        {
            _ = ResetImageIndex(Math.Max(CurrentImageIndex - 1, 0));
        }


        #endregion

        #region Single/Double View

        private bool _NowDoubleImageView;
        public bool NowDoubleImageView
        {
            get { return _NowDoubleImageView; }
            set { SetProperty(ref _NowDoubleImageView, value); }
        }


        private int _ViewResponsibleImageAmount;
        public int ViewResponsibleImageAmount
        {
            get { return _ViewResponsibleImageAmount; }
            set { SetProperty(ref _ViewResponsibleImageAmount, value); }
        }

        void CalcViewResponsibleImageAmount(double canvasWidth, double canvasHeight)
        {
            var images = _CurrentImages.ToArray();
            if (ImageViewerSettings.IsEnableSpreadDisplay)
            {
                var aspectRatio = canvasWidth / canvasHeight;

                if (aspectRatio > 1.35)
                {
                    ViewResponsibleImageAmount = 2;
                    if (CurrentImages?.Length != 2)
                    {
                        CurrentImages = new BitmapImage[2] { images.ElementAtOrDefault(0), null };
                    }
                }
                else
                {
                    ViewResponsibleImageAmount = 1;
                    if (CurrentImages?.Length != 1)
                    {
                        CurrentImages = new BitmapImage[1] { images.ElementAtOrDefault(0) };
                    }
                }
            }
            else
            {
                ViewResponsibleImageAmount = 1;
                if (CurrentImages?.Length != 1)
                {
                    CurrentImages = new BitmapImage[1] { images.ElementAtOrDefault(0) };
                }
            }

            
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

        static FastAsyncLock _lock = new FastAsyncLock();

        public void Cancel()
        {
            IsCanceled = true;
            _PrefetchCts.Cancel();
        }


        public async Task<BitmapImage> StartPrefetchAsync()
        {
            using (await _lock.LockAsync(_PrefetchCts.Token))
            {                
                Image ??= await ImageSource.GenerateBitmapImageAsync(_PrefetchCts.Token);
                IsCompleted = true;

                Debug.WriteLine("prefetch done: " + ImageSource.Name);

                return Image;
            }
        }

        public void Dispose()
        {
            ((IDisposable)_PrefetchCts).Dispose();
        }
    }


}
