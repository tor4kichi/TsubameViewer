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
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.Bookmark;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views.ViewManagement.Commands;
using Uno;
using Uno.Extensions;
using Uno.Threading;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Media.Imaging;
using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class ImageViewerPageViewModel : ViewModelBase
    {
        private string _currentToken;
        private StorageFolder _tokenGettingFolder;

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


        readonly static char[] SeparateChars = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        private ApplicationView _appView;
        CompositeDisposable _navigationDisposables;

        public ImageViewerSettings ImageViewerSettings { get; }

        private readonly IScheduler _scheduler;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly ImageCollectionManager _imageCollectionManager;
        private readonly BookmarkManager _bookmarkManager;
        CompositeDisposable _disposables = new CompositeDisposable();

        public ImageViewerPageViewModel(
            IScheduler scheduler,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            ImageCollectionManager imageCollectionManager,
            ImageViewerSettings imageCollectionSettings,
            BookmarkManager bookmarkManager,
            ToggleFullScreenCommand toggleFullScreenCommand,
            BackNavigationCommand backNavigationCommand
            )
        {
            _scheduler = scheduler;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _imageCollectionManager = imageCollectionManager;
            ImageViewerSettings = imageCollectionSettings;
            ToggleFullScreenCommand = toggleFullScreenCommand;
            BackNavigationCommand = backNavigationCommand;
            _bookmarkManager = bookmarkManager;
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
                })
                .ToReadOnlyReactivePropertySlim()
                .AddTo(_disposables);

            CanvasWidth = new ReactiveProperty<double>()
                .AddTo(_disposables);
            CanvasHeight = new ReactiveProperty<double>()
                .AddTo(_disposables);

            _appView = ApplicationView.GetForCurrentView();
        }






        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            if (Images?.Any() ?? false)
            {
                Images.ForEach(x => (x as IDisposable)?.Dispose());
                Images = null;
            }

            CurrentImages = null;

            _leavePageCancellationTokenSource.Cancel();
            _navigationDisposables.Dispose();
            _ImageEnumerationDisposer?.Dispose();
            _ImageEnumerationDisposer = null;

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
                if (parameters.TryGetValue(PageNavigationConstants.Token, out string token))
                {
                    if (_currentToken != token)
                    {
                        _currentPath = null;
                        _currentFolderItem = null;

                        _currentToken = token;

                        var item = await _sourceStorageItemsRepository.GetItemAsync(token);

                        _tokenGettingFolder = item as StorageFolder;

                        // ファイルアクティベーションなど
                        if (item is StorageFile file)
                        {
                            _currentFolderItem = file;
                        }

                        Images = default;
                        CurrentImageIndex = 0;
                    }
                }
#if DEBUG
                else
                {
                    Debug.Assert(false, "required 'token' parameter in FolderListupPage navigation.");
                }
#endif

                if (parameters.TryGetValue(PageNavigationConstants.Path, out string path))
                {
                    var unescapedPath = Uri.UnescapeDataString(path);
                    if (_currentPath != unescapedPath)
                    {
                        _currentPath = unescapedPath;
                        if (_tokenGettingFolder == null)
                        {
                            // token がファイルを指す場合は _currentFolderItem を通じて表示する
                            if (_currentFolderItem.Name != unescapedPath)
                            {
                                throw new Exception("token parameter is require for path parameter.");
                            }
                        }
                        else
                        {
                            _currentFolderItem = await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);
                        }

                        Images = default;
                        CurrentImageIndex = 0;
                    }
                }
            }

            // 以下の場合に表示内容を更新する
            //    1. 表示フォルダが変更された場合
            //    2. 前回の更新が未完了だった場合
            if (_tokenGettingFolder != null || _currentFolderItem != null)
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


        async Task MoveImageIndex(IndexMoveDirection direction, int? request = null)
        {
            if (Images == null || Images.Length == 0) { return; }

            // requestIndex round
            // roundした場合は読み込み数の切り捨て必要

            _imageLoadingCts?.Cancel();
            _imageLoadingCts?.Dispose();
            _imageLoadingCts = new CancellationTokenSource();
            
            var ct = _imageLoadingCts.Token;
            
            using (await _imageLoadingLock.LockAsync(ct))
            {
                try
                {
                    // 読み込むべきインデックスを先に洗い出す
                    var requestIndex = direction switch
                    {
                        IndexMoveDirection.Refresh => request ?? CurrentImageIndex,
                        IndexMoveDirection.Forward => CurrentImageIndex + (_prevForceSingleView ? 1 : _CurrentImages.Length),
                        IndexMoveDirection.Backward => CurrentImageIndex - _CurrentImages.Length,
                        _ => throw new NotSupportedException(),
                    };

                    // requestIndexの範囲をページ数上下限のそれぞれ1余計な分までをもってクランプ
                    // 見開き表示時に+2/-2の範囲までを表示リクエストすることで
                    // 例えば1ページから-2で0と-1を表示するリクエストとして
                    // 後の処理で-1を切り落とすことで0ページのみを表示させることを意図しています。
                    requestIndex = Math.Clamp(requestIndex, -1, Images.Length);

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

                    if (indexies.Count() == 0) { return; }

                    foreach (var i in Enumerable.Range(0, _CurrentImages.Length))
                    {
                        _CurrentImages[i] = null;
                    }

                    var canvasWidth = (int)CanvasWidth.Value;
                    var canvasHeight = (int)CanvasHeight.Value;


                    int generateImageIndex = -1;
                    bool isForceSingleImageView = false;
                    foreach (var i in indexies)
                    {
                        if (isForceSingleImageView)
                        {
                            _CurrentImages[i] = null;
                            continue;
                        }

                        if (ct.IsCancellationRequested) { return; }

                        var imageSource = Images[requestIndex + i];
                        var bitmapImage = await MakeBitmapImageAsync(imageSource, canvasWidth, canvasHeight, ct);
                        if (bitmapImage.PixelHeight < bitmapImage.PixelWidth)
                        {
                            isForceSingleImageView = true;

                            // 二枚目以降が横長だった場合は表示しない
                            if (_CurrentImages.Any(x => x != null))
                            {
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

                    // SliderとCurrentImageIndexの更新が競合するため、スキップ用の仕掛けが必要
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

        static async Task<BitmapImage> MakeBitmapImageAsync(IImageSource imageSource, int canvasWidth, int canvasHeight, CancellationToken ct)
        {
            var bitmapImage = await imageSource.GenerateBitmapImageAsync(ct);

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

            var result = await _imageCollectionManager.GetImageSourcesAsync(_currentFolderItem);
            if (result != null)
            {
                Images = result.Images;
                CurrentImageIndex = result.FirstSelectedIndex;
                _ImageEnumerationDisposer = result.ItemsEnumeratorDisposer;
                ParentFolderOrArchiveName = result.ParentFolderOrArchiveName;

                PageFolderNames = Images.Select(x => x.Name.Split(SeparateChars).TakeLast(2).First()).Distinct().ToArray();

                ItemType = SupportedFileTypesHelper.StorageItemToStorageItemTypes(_currentFolderItem);

                _appView.Title = _currentFolderItem.Name;
                Title = ItemType == StorageItemTypes.Image ? ParentFolderOrArchiveName : _currentFolderItem.Name;

                GoNextImageCommand.RaiseCanExecuteChanged();
                GoPrevImageCommand.RaiseCanExecuteChanged();
            }
        }

        #region Commands

        public ToggleFullScreenCommand ToggleFullScreenCommand { get; }
        public BackNavigationCommand BackNavigationCommand { get; }

        private DelegateCommand _GoNextImageCommand;
        public DelegateCommand GoNextImageCommand =>
            _GoNextImageCommand ??= new DelegateCommand(ExecuteGoNextImageCommand, CanGoNextCommand) { IsActive = true };

        private void ExecuteGoNextImageCommand()
        {
            if (CurrentImageIndex + 1 < Images?.Length)
            {
                _ = MoveImageIndex(IndexMoveDirection.Forward);
            }
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
            if (CurrentImageIndex >= 1 && Images?.Length > 0)
            {
                _ = MoveImageIndex(IndexMoveDirection.Backward);
            }
        }

        private bool CanGoPrevCommand()
        {
            //return CurrentImageIndex >= 1 && Images?.Length > 0;
            return true;
        }

        private DelegateCommand _SizeChangedCommand;
        public DelegateCommand SizeChangedCommand =>
            _SizeChangedCommand ??= new DelegateCommand(async () =>
            {
                CalcViewResponsibleImageAmount(CanvasWidth.Value, CanvasHeight.Value);

                if (!(Images?.Any() ?? false)) { return; }

                await Task.Delay(50);

                _ = ResetImageIndex(CurrentImageIndex);
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
                    CurrentImages = new BitmapImage[2] { images.ElementAtOrDefault(0), null };
                }
                else
                {
                    ViewResponsibleImageAmount = 1;
                    CurrentImages = new BitmapImage[1] { images.ElementAtOrDefault(0) };
                }
            }
            else
            {
                ViewResponsibleImageAmount = 1;
                CurrentImages = new BitmapImage[1] { images.ElementAtOrDefault(0) };
            }

            
        }

        #endregion


    }



}
