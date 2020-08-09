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


        private BitmapImage _CurrentImage;
        public BitmapImage CurrentImage
        {
            get => _CurrentImage;
            private set => SetProperty(ref _CurrentImage, value);
        }

        private BitmapImage _CurrentImage2;
        public BitmapImage CurrentImage2
        {
            get => _CurrentImage2;
            private set => SetProperty(ref _CurrentImage2, value);
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

        bool _nowMovePrev;

        HashSet<int> _DoubleViewSpecialProcessPage;
        HashSet<int> _UserInputDoubleViewSpecialProcessPage;

        private bool _nowSpecialProcessDoubleViewToSingleView;
        public bool NowUserInputSpecialProcessDoubleViewToSingleView
        {
            get { return _nowSpecialProcessDoubleViewToSingleView; }
            set { SetProperty(ref _nowSpecialProcessDoubleViewToSingleView, value); }
        }


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
        private readonly DoubleImageViewSepecialProcessManager _doubleImageViewSepecialProcessManager;
        CompositeDisposable _disposables = new CompositeDisposable();

        public ImageViewerPageViewModel(
            IScheduler scheduler,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            ImageCollectionManager imageCollectionManager,
            ImageViewerSettings imageCollectionSettings,
            BookmarkManager bookmarkManager,
            DoubleImageViewSepecialProcessManager doubleImageViewSepecialProcessManager,
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
            _doubleImageViewSepecialProcessManager = doubleImageViewSepecialProcessManager;
            DisplayCurrentImageIndex = this.ObserveProperty(x => x.CurrentImageIndex)
                .Select(x => x + 1)
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

            CurrentImage = null;

            _leavePageCancellationTokenSource.Cancel();
            _navigationDisposables.Dispose();
            _ImageEnumerationDisposer?.Dispose();
            _ImageEnumerationDisposer = null;

            // フルスクリーンを終了
            ApplicationView.GetForCurrentView().ExitFullScreenMode();

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
                if (parameters.TryGetValue("token", out string token))
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

                if (parameters.TryGetValue("path", out string path))
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
                || parameters.ContainsKey("__restored") 
                || (mode == NavigationMode.New && !parameters.ContainsKey("pageName"))
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
            else if (mode == NavigationMode.New && parameters.ContainsKey("pageName")
                )
            {
                if (parameters.TryGetValue("pageName", out string pageName))
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
            this.ObserveProperty(x => x.CurrentImageIndex)
                .Throttle(TimeSpan.FromMilliseconds(50), _scheduler)
                .Subscribe(async index =>
                {
                    if (Images == null || Images.Length == 0) { return; }
                    Debug.WriteLine("New Index: " + index);
                    _imageLoadingCts?.Cancel();
                    _imageLoadingCts?.Dispose();
                    _imageLoadingCts = new CancellationTokenSource();

                    var ct = _imageLoadingCts.Token;
                    try
                    {
                        using (await _imageLoadingLock.LockAsync(ct))
                        {
                            var nowMovePreview = _nowMovePrev;
                            _nowMovePrev = false;

                            async Task<BitmapImage> MakeBitmapImageAsync(IImageSource imageSource, int canvasWidth, int canvasHeight, CancellationToken ct)
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

                            var imageSource1 = Images[index];

                            var names = imageSource1.Name.Split(SeparateChars);
                            PageName = names[names.Length - 1];
                            PageFolderName = names.Length >= 2 ? names[names.Length - 2] : string.Empty;
                            _bookmarkManager.AddBookmark(_currentFolderItem.Path, imageSource1.Name, new NormalizedPagePosition(Images.Length, _CurrentImageIndex));



                            var canvasWidth = (int)CanvasWidth.Value;
                            var canvasHeight = (int)CanvasHeight.Value;

                            if (ct.IsCancellationRequested) { return; }

                            _CurrentImage = null;
                            _CurrentImage2 = null;
                            if (!NowCanDoubleImageView)
                            {
                                _CurrentImage = await MakeBitmapImageAsync(imageSource1, canvasWidth, canvasHeight, ct);
                                NowUserInputSpecialProcessDoubleViewToSingleView = false;
                            }
                            else
                            {
                                // 見開き表示対応
                                // 表示切替の進行方向に合わせた2枚表示と1枚表示の場合分けが必要になる
                                if (!nowMovePreview)
                                {
                                    if (_thenDoubleViewUpdated_Backward)
                                    {
                                        _thenDoubleViewUpdated_Backward = false;

                                        index = _CurrentImageIndex += 1;
                                        imageSource1 = Images[_CurrentImageIndex];
                                    }

                                    var isNeedSpecialProcessImage1 = _DoubleViewSpecialProcessPage.Contains(index) || _UserInputDoubleViewSpecialProcessPage.Contains(index);
                                    var isNeedSpecialProcessImage2 = _DoubleViewSpecialProcessPage.Contains(index + 1) || _UserInputDoubleViewSpecialProcessPage.Contains(index + 1);
                                    var isNeedSingleView = isNeedSpecialProcessImage1 || isNeedSpecialProcessImage2;

                                    NowUserInputSpecialProcessDoubleViewToSingleView = _UserInputDoubleViewSpecialProcessPage.Contains(index) || _UserInputDoubleViewSpecialProcessPage.Contains(index + 1);

                                    IImageSource imageSource2;
                                    // 前方向への移動
                                    // 一枚目が大きい場合は二枚目を読み込まない
                                    _CurrentImage = await MakeBitmapImageAsync(imageSource1, canvasWidth, canvasHeight, ct);
                                    if (_CurrentImage.PixelHeight < _CurrentImage.PixelWidth)
                                    {
                                        // 横長の場合はSingleViewで表示させるためsecondIamgeSourceは読み込まない
                                        _DoubleViewSpecialProcessPage.Add(index);
                                        _doubleImageViewSepecialProcessManager.SetSpecialProcessPage(_currentFolderItem.Path, _DoubleViewSpecialProcessPage);
                                    }
                                    else if (!isNeedSingleView && (imageSource2 = Images.ElementAtOrDefault(index + 1)) != null)
                                    {
                                        // 二枚目を読み込む場合は前方向にインデックスを補正する
                                        _CurrentImage2 = await MakeBitmapImageAsync(imageSource2, canvasWidth, canvasHeight, ct);
                                        if (_CurrentImage2.PixelHeight < _CurrentImage2.PixelWidth)
                                        {
                                            // 横長の場合はSingleViewで表示させるためsecondIamgeSourceは読み込まない
                                            _DoubleViewSpecialProcessPage.Add(index + 1);
                                            _doubleImageViewSepecialProcessManager.SetSpecialProcessPage(_currentFolderItem.Path, _DoubleViewSpecialProcessPage);
                                            _CurrentImage2 = null;
                                        }
                                        else
                                        {
                                            _CurrentImageIndex += 1;
                                            _thenDoubleViewUpdated_Forward = true;
                                        }
                                    }
                                }
                                else
                                {
                                    if (_thenDoubleViewUpdated_Forward)
                                    {
                                        _thenDoubleViewUpdated_Forward = false;

                                        index = _CurrentImageIndex -= 1;
                                        imageSource1 = Images[_CurrentImageIndex];
                                    }

                                    var isNeedSpecialProcessImage1 = _DoubleViewSpecialProcessPage.Contains(index - 1) || _UserInputDoubleViewSpecialProcessPage.Contains(index - 1);
                                    var isNeedSpecialProcessImage2 = _DoubleViewSpecialProcessPage.Contains(index) || _UserInputDoubleViewSpecialProcessPage.Contains(index);
                                    var isNeedSingleView = isNeedSpecialProcessImage1 || isNeedSpecialProcessImage2;


                                    NowUserInputSpecialProcessDoubleViewToSingleView = _UserInputDoubleViewSpecialProcessPage.Contains(index) || _UserInputDoubleViewSpecialProcessPage.Contains(index - 1);

                                    // 右綴じとしてimageSource1が左、imageSource2が右に来る

                                    IImageSource imageSource2;
                                    // 後方向への移動
                                    // 二枚目が大きい場合は一枚目を読み込まない
                                    _CurrentImage2 = await MakeBitmapImageAsync(imageSource1, canvasWidth, canvasHeight, ct); ;
                                    if (_CurrentImage2.PixelHeight < _CurrentImage2.PixelWidth)
                                    {
                                        // 横長の場合はSingleViewで表示させるためsecondIamgeSourceは読み込まない
                                        _DoubleViewSpecialProcessPage.Add(index);
                                        _doubleImageViewSepecialProcessManager.SetSpecialProcessPage(_currentFolderItem.Path, _DoubleViewSpecialProcessPage);
                                    }
                                    else if (!isNeedSingleView && (imageSource2 = Images.ElementAtOrDefault(index - 1)) != null)
                                    {
                                        // 二枚目を読み込む場合は後方向にインデックスを補正する
                                        _CurrentImage = await MakeBitmapImageAsync(imageSource2, canvasWidth, canvasHeight, ct);
                                        if (_CurrentImage.PixelHeight < _CurrentImage.PixelWidth)
                                        {
                                            // 横長の場合はSingleViewで表示させるためsecondIamgeSourceは読み込まない
                                            _DoubleViewSpecialProcessPage.Add(index - 1);
                                            _doubleImageViewSepecialProcessManager.SetSpecialProcessPage(_currentFolderItem.Path, _DoubleViewSpecialProcessPage);
                                            _CurrentImage = null;
                                        }
                                        else
                                        {
                                            _CurrentImageIndex -= 1;
                                            _thenDoubleViewUpdated_Backward = true;
                                        }
                                    }
                                }

                                if (ct.IsCancellationRequested) { return; }
                            }

                            NowDoubleImageView = _CurrentImage != null && CurrentImage2 != null;
                            RaisePropertyChanged(nameof(CurrentImage));
                            RaisePropertyChanged(nameof(CurrentImage2));                            
                        }
                    }
                    catch (OperationCanceledException) { }
#if DEBUG
                    Debug.WriteLine($"w={CurrentImage?.PixelWidth:F2}, h={CurrentImage?.PixelHeight:F2}");
#endif
                })
                .AddTo(_navigationDisposables);

            await base.OnNavigatedToAsync(parameters);
        }

        bool _thenDoubleViewUpdated_Forward;
        bool _thenDoubleViewUpdated_Backward;
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

                _DoubleViewSpecialProcessPage = _doubleImageViewSepecialProcessManager.GetSpecialProcessPages(_currentFolderItem.Path);
                _UserInputDoubleViewSpecialProcessPage = _doubleImageViewSepecialProcessManager.GetUserINputSpecialProcessPages(_currentFolderItem.Path);

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
            CurrentImageIndex += 1;

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }

        private bool CanGoNextCommand()
        {
            return CurrentImageIndex + 1 < Images?.Length;
        }

        private DelegateCommand _GoPrevImageCommand;
        public DelegateCommand GoPrevImageCommand =>
            _GoPrevImageCommand ??= new DelegateCommand(ExecuteGoPrevImageCommand, CanGoPrevCommand) { IsActive = true };

        private void ExecuteGoPrevImageCommand()
        {
            _nowMovePrev = true;
            CurrentImageIndex -= 1;

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }

        private bool CanGoPrevCommand()
        {
            return CurrentImageIndex >= 1 && Images?.Length > 0;
        }

        private DelegateCommand _SizeChangedCommand;
        public DelegateCommand SizeChangedCommand =>
            _SizeChangedCommand ??= new DelegateCommand(async () =>
            {
                if (!(Images?.Any() ?? false)) { return; }

                await Task.Delay(50);

                RaisePropertyChanged(nameof(CurrentImageIndex));
            });

        private DelegateCommand<string> _changePageFolderCommand;
        public DelegateCommand<string> ChangePageFolderCommand =>
            _changePageFolderCommand ?? (_changePageFolderCommand = new DelegateCommand<string>(ExecuteChangePageFolderCommand));

        void ExecuteChangePageFolderCommand(string pageName)
        {
            var pageFirstItem = Images.FirstOrDefault(x => x.Name.Contains(pageName));
            if (pageFirstItem == null) { return; }

            var pageFirstItemIndex = Images.IndexOf(pageFirstItem);
            CurrentImageIndex = pageFirstItemIndex;
        }

        private DelegateCommand<double?> _ChangePageCommand;
        public DelegateCommand<double?> ChangePageCommand =>
            _ChangePageCommand ?? (_ChangePageCommand = new DelegateCommand<double?>(ExecuteChangePageCommand));

        void ExecuteChangePageCommand(double? parameter)
        {
            var page = (int)parameter.Value;
            CurrentImageIndex = page;
        }




        private DelegateCommand _AddSpecialProcessPageCommand;
        public DelegateCommand AddSpecialProcessPageCommand =>
            _AddSpecialProcessPageCommand ?? (_AddSpecialProcessPageCommand = new DelegateCommand(ExecuteAddSpecialProcessPageCommand));

        void ExecuteAddSpecialProcessPageCommand()
        {
            var index = _CurrentImageIndex;
            if (_thenDoubleViewUpdated_Backward)
            {
                index++;
            }
            if (_thenDoubleViewUpdated_Forward)
            {
                index--;
            }

            if (_DoubleViewSpecialProcessPage.Contains(index)) { return; }
            if (_UserInputDoubleViewSpecialProcessPage.Contains(index)) { return; }

            _UserInputDoubleViewSpecialProcessPage.Add(index);
            _doubleImageViewSepecialProcessManager.SetUserInputSpecialProcessPage(_currentFolderItem.Path, _UserInputDoubleViewSpecialProcessPage);
            RaisePropertyChanged(nameof(CurrentImageIndex));
        }


        private DelegateCommand _RemoveSpecialProcessPageCommand;
        public DelegateCommand RemoveSpecialProcessPageCommand =>
            _RemoveSpecialProcessPageCommand ?? (_RemoveSpecialProcessPageCommand = new DelegateCommand(ExecuteRemoveSpecialProcessPageCommand));

        void ExecuteRemoveSpecialProcessPageCommand()
        {
            var index = _CurrentImageIndex;
            if (_thenDoubleViewUpdated_Backward)
            {
                index++;
            }
            if (_thenDoubleViewUpdated_Forward)
            {
                index--;
            }

            if (!_UserInputDoubleViewSpecialProcessPage.Contains(index)) { return; }

            _UserInputDoubleViewSpecialProcessPage.Remove(index);
            _doubleImageViewSepecialProcessManager.SetUserInputSpecialProcessPage(_currentFolderItem.Path, _UserInputDoubleViewSpecialProcessPage);
            RaisePropertyChanged(nameof(CurrentImageIndex));
        }


        #endregion

        #region Single/Double View

        private bool _nowCanDoubleImageView;
        public bool NowCanDoubleImageView
        {
            get { return _nowCanDoubleImageView; }
            private set { SetProperty(ref _nowCanDoubleImageView, value); }
        }


        private bool _NowDoubleImageView;
        public bool NowDoubleImageView
        {
            get { return _NowDoubleImageView; }
            set { SetProperty(ref _NowDoubleImageView, value); }
        }

        public void SetSingleImageView()
        {
            NowCanDoubleImageView = false;

            if (Images?.Any() == false)
            {
                return;
            }

            CurrentImage2 = null;
        }

        public void SetDoubleImageView()
        {
            NowCanDoubleImageView = true;

            if ((Images?.Any() ?? false) == false)
            {
                return;
            }

            // 奇数ページを表示している場合は偶数ページ始まりになるように
            if (CurrentImageIndex % 2 == 1)
            {
                CurrentImageIndex -= 1;
            }
            else
            {
                RaisePropertyChanged(nameof(CurrentImageIndex));
            }
        }

        #endregion


    }



}
