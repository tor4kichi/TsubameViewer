using CommunityToolkit.WinUI.Helpers;
using CommunityToolkit.WinUI.UI.Animations;
using DryIoc;
using LiteDB;
using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Models.UseCase.Maintenance;
using TsubameViewer.Models.UseCase.Migrate;
using TsubameViewer.Presentation.Services;
using TsubameViewer.Presentation.Services.UWP;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.Views;
using TsubameViewer.Presentation.Views.Dialogs;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TsubameViewer
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {

        /// <summary>
        /// Gets the current <see cref="App"/> instance in use
        /// </summary>
        public new static App Current => (App)Application.Current;

        private WindowSettings _windowSettings;
        private Window _window;
        private Presentation.Views.SplashScreen _splashScreen;

        public Window Window => _window;

        public XamlRoot XamlRoot => _window.Content.XamlRoot;

        public Container Container { get; private set; }

        public void InitializeWithWindow(object dialog)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);

            // Associate the HWND with the file picker
            WinRT.Interop.InitializeWithWindow.Initialize(dialog, hwnd);
        }

        private AppWindow _appWindow;
        public AppWindow AppWindow => _appWindow ??= GetAppWindowForCurrentWindow();

        // see@ https://docs.microsoft.com/ja-jp/windows/apps/windows-app-sdk/windowing/windowing-overview
        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(myWndId);
        }

        public Windows.UI.ViewManagement.UIViewSettings GetUIViewSettings()
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            return Windows.UI.ViewManagement.UIViewSettingsInterop.GetForWindow(hWnd);
        }

        public Windows.UI.ViewManagement.InputPane GetInputPane()
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            return Windows.UI.ViewManagement.InputPaneInterop.GetForWindow(hWnd);
        }


        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            this.UnhandledException += App_UnhandledException;


            // ローカリゼーション用のライブラリを初期化
            try
            {
                I18NPortable.I18N.Current
#if DEBUG
                    //.SetLogger(text => System.Diagnostics.Debug.WriteLine(text))
                    .SetNotFoundSymbol("🍣")
#endif
                    .SetFallbackLocale("en")
                    .Init(GetType().Assembly);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = e.Exception is OperationCanceledException;
            if (e.Handled is false)
            {
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.Exception.ToString());
            }
        }


        /// <summary>
        /// Configures the services for the application.
        /// </summary>
        private static Container ConfigureServices()
        {
            var rules = Rules.Default
                .WithAutoConcreteTypeResolution()
                .With(Made.Of(FactoryMethod.ConstructorWithResolvableArguments))
                .WithoutThrowOnRegisteringDisposableTransient()
                .WithFuncAndLazyWithoutRegistration()
                .WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Replace);

            var container = new Container(rules);

            RegisterRquiredTypes(container);
            RegisterTypes(container);

            Ioc.Default.ConfigureServices(container);
            return container;
        }

        static void RegisterRquiredTypes(Container container)
        {
            container.RegisterInstance<ILiteDatabase>(new LiteDatabase($"Filename={Path.Combine(ApplicationData.Current.LocalFolder.Path, "tsubame.db")}; Async=false;"));

            container.RegisterInstance<ILiteDatabase>(new LiteDatabase($"Filename={Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "tsubame_temp.db")}; Async=false;"), serviceKey: "TemporaryDb");
            container.Register<ThumbnailManager>(made: Parameters.Of.Name("temporaryDb", serviceKey: "TemporaryDb"));

            container.RegisterInstance<IScheduler>(new SynchronizationContextScheduler(DispatcherQueueSynchronizationContext.Current));

            container.Register<IViewLocator, ViewLocator>();
            
            container.Register<PrimaryWindowCoreLayout>(reuse: new SingletonReuse());
            container.Register<SourceStorageItemsPage>();
            container.Register<ImageListupPage>();
            container.Register<FolderListupPage>();
            container.Register<ImageViewerPage>();
            container.Register<EBookReaderPage>();
            container.Register<SettingsPage>();
            container.Register<SearchResultPage>();
            container.Register<AlbamListupPage>();
        }

        public static void RegisterTypes(Container container)
        {
            container.RegisterInstance<IMessenger>(WeakReferenceMessenger.Default);
            container.Register<Models.Domain.ImageViewer.ImageViewerSettings>(reuse: new SingletonReuse());
            container.Register<FolderListingSettings>(reuse: new SingletonReuse());
            container.Register<FileControlSettings>(reuse: new SingletonReuse());


            container.Register<Presentation.Services.UWP.SecondaryTileManager>(reuse: new SingletonReuse());
            container.RegisterDelegate<Presentation.Services.WindowsTriggers>(() => new WindowsTriggers(App.Current.Window), reuse: new SingletonReuse());

            container.Register<Models.UseCase.Maintenance.CacheDeletionWhenSourceStorageItemIgnored>(reuse: new SingletonReuse());


            
            container.Register<SourceStorageItemsPageViewModel>(reuse: new SingletonReuse());
            //container.Register<ImageListupPageViewModel>(reuse: new SingletonReuse());
            //container.Register<FolderListupPageViewModel>(reuse: new SingletonReuse());
            container.Register<ImageViewerPageViewModel>(reuse: new SingletonReuse());
            container.Register<EBookReaderPageViewModel>(reuse: new SingletonReuse());
            //container.Register<SearchResultPageViewModel>(reuse: new SingletonReuse());

            // Services
            container.RegisterDelegate<IStorageItemDeleteConfirmation>(x =>
            {
                var dialog = container.Resolve<StorageItemDeleteConfirmDialog>();
                dialog.XamlRoot = App.Current.XamlRoot;
                return dialog;
            });

            container.RegisterDelegate<IStorageItemDeleteConfirmation>(x =>
            {
                var dialog = container.Resolve<StorageItemDeleteConfirmDialog>();
                dialog.XamlRoot = App.Current.XamlRoot;
                return dialog;
            });
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // If this is the first instance launched, then register it as the "main" instance.
            // If this isn't the first instance launched, then "main" will already be registered,
            // so retrieve it.
            var mainInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey("main");

            // If the instance that's executing the OnLaunched handler right now
            // isn't the "main" instance.
            if (!mainInstance.IsCurrent)
            {
                // Redirect the activation (and args) to the "main" instance, and exit.
                var activatedEventArgs =
                    Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
                await mainInstance.RedirectActivationToAsync(activatedEventArgs);
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }

            var actiavatedEventArgs = AppInstance.GetActivatedEventArgs();
            if (_isInitialized is false)
            {
                Container = ConfigureServices();

                var applicationSettings = Container.Resolve<Models.Domain.ApplicationSettings>();
                try
                {
                    I18NPortable.I18N.Current.Locale = applicationSettings.Locale ?? I18NPortable.I18N.Current.Languages.FirstOrDefault(x => x.Locale.StartsWith(CultureInfo.CurrentCulture.Name))?.Locale;
                }
                catch
                {
                    I18NPortable.I18N.Current.Locale = "en-US";
                }

#if DEBUG
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    this.DebugSettings.EnableFrameRateCounter = false;
                }
#endif
                Resources["DebugTVMode"] = Container.Resolve<ApplicationSettings>().ForceXboxAppearanceModeEnabled;
                
                SystemInformation.Instance.TrackAppUse(actiavatedEventArgs);

                _windowSettings = Ioc.Default.GetService<WindowSettings>();
                _window = new Window();

                _splashScreen = new Presentation.Views.SplashScreen();

                var rootGrid = new Grid()
                {
                    Children =
                    {
                        _splashScreen,
                    }
                };
                
                _window.Content = rootGrid;
                SetWindowTitle();
                _isInitialized = true;

                if (_windowSettings.LastWindowPresenterKind is not AppWindowPresenterKind.Default and not AppWindowPresenterKind.Overlapped)
                {
                    AppWindow.SetPresenter(_windowSettings.LastWindowPresenterKind);
                }                

                if (AppWindow.Presenter.Kind != AppWindowPresenterKind.FullScreen)
                {
                    RestoreWindowPositionAndSize();
                }

                _lastWindowPosition = AppWindow.Position;
                await Task.Delay(5);
                _window.Activate();

                AppWindow.Changed += AppWindow_Changed;
                AppWindow.Closing += AppWindow_Closing;

                await InitializeAsync();

                _primaryWindowCoreLayout = Ioc.Default.GetRequiredService<PrimaryWindowCoreLayout>();
                rootGrid.Children.Insert(0, _primaryWindowCoreLayout);
            }
            else
            {
                AppWindow.Show();
            }

            await OnActivatedAsync(actiavatedEventArgs);

            if (_splashScreen.Opacity == 1.0)
            {
                AnimationBuilder.Create()
                    .Opacity(0.0, duration: TimeSpan.FromSeconds(0.25), easingType: EasingType.Cubic)
                    .Start(_splashScreen, () => _splashScreen.Visibility = Visibility.Collapsed);
            }
        }


        #region Window Position and Size

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            SaveWindowPositionAndSize();
        }

        public void RestoreWindowPositionAndSize()
        {
            if (AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped)
            {
                if (_windowSettings.LastOverlappedWindowPosition is not { X: 0, Y: 0 } && _windowSettings.LastOverlappedWindowSize is not { Width: 0, Height: 0 })
                {
                    AppWindow.MoveAndResize(new RectInt32(
                        (int)_windowSettings.LastOverlappedWindowPosition.X,
                        (int)_windowSettings.LastOverlappedWindowPosition.Y,
                        (int)_windowSettings.LastOverlappedWindowSize.Width,
                        (int)_windowSettings.LastOverlappedWindowSize.Height
                        ));
                }
            }
            else if (AppWindow.Presenter.Kind == AppWindowPresenterKind.CompactOverlay)
            {
                if (_windowSettings.LastCompactOverlayWindowPosition is not { X: 0, Y: 0 } && _windowSettings.LastCompactOverlayWindowSize is not { Width: 0, Height: 0 })
                {
                    AppWindow.MoveAndResize(new RectInt32(
                    (int)_windowSettings.LastCompactOverlayWindowPosition.X,
                    (int)_windowSettings.LastCompactOverlayWindowPosition.Y,
                    (int)_windowSettings.LastCompactOverlayWindowSize.Width,
                    (int)_windowSettings.LastCompactOverlayWindowSize.Height
                    ));
                }
            }
        }

        public void SaveWindowPositionAndSize()
        {
            var windowSettings = Ioc.Default.GetService<WindowSettings>();
            var pos = AppWindow.Position;
            var size = AppWindow.Size;

            if (AppWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped)
            {
                windowSettings.LastOverlappedWindowPosition = new System.Windows.Point(pos.X, pos.Y);
                windowSettings.LastOverlappedWindowSize = new System.Windows.Size(size.Width, size.Height);
            }
            else if (AppWindow.Presenter.Kind == AppWindowPresenterKind.CompactOverlay)
            {
                windowSettings.LastCompactOverlayWindowPosition = new System.Windows.Point(pos.X, pos.Y);
                windowSettings.LastCompactOverlayWindowSize = new System.Windows.Size(size.Width, size.Height);
            }

            windowSettings.LastWindowPresenterKind = AppWindow.Presenter.Kind;
        }

        private int MinWindowWidth = 500;
        private int MinWindowHeight = 500;

        private PointInt32 _lastWindowPosition;
        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidSizeChange)
            {
                if (sender.Size.Width < MinWindowWidth || sender.Size.Height < MinWindowHeight)
                {
                    var newSize = new SizeInt32(Math.Max(MinWindowWidth, sender.Size.Width), Math.Max(MinWindowHeight, sender.Size.Height));
                    if (args.DidPositionChange)
                    {
                        sender.MoveAndResize(new RectInt32(_lastWindowPosition.X, _lastWindowPosition.Y, newSize.Width, newSize.Height));
                    }
                    else
                    {
                        sender.Resize(newSize);
                    }
                }                
            }

            if (args.DidPositionChange)
            {
                _lastWindowPosition = sender.Position;
            }
        }

        #endregion Window Position and Size

        public void SetWindowTitle(string? title = null)
        {
            var appWindow = GetAppWindowForCurrentWindow();
            if (appWindow.TitleBar != null)
            {
                appWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
            }

            appWindow.Title = !string.IsNullOrEmpty(title) ? $"{title} - {nameof(TsubameViewer)}" : nameof(TsubameViewer);
        }

        bool _isInitialized = false;

        bool isRestored = false;
        private PrimaryWindowCoreLayout _primaryWindowCoreLayout;

        private async Task OnActivatedAsync(IActivatedEventArgs args)
        {
            PageNavigationInfo pageNavigationInfo = null;
            if (args is Windows.ApplicationModel.Activation.LaunchActivatedEventArgs launchActivatedEvent)
            {
                if (launchActivatedEvent.PrelaunchActivated == false)
                {
                    // Ensure the current window is active
                    _window.Activate();

                    if (!string.IsNullOrEmpty(launchActivatedEvent.Arguments))
                    {

                        try
                        {
                            var tileArgs = SecondaryTileManager.DeserializeSecondaryTileArguments(launchActivatedEvent.Arguments);
                            pageNavigationInfo = SecondatyTileArgumentToNavigationInfo(tileArgs);
                        }
                        catch { }
                    }
                }
            }
            else if (args is FileActivatedEventArgs fileActivatedEventArgs)
            {
                try
                {
                    pageNavigationInfo = await FileActivatedArgumentToNavigationInfo(fileActivatedEventArgs);
                }
                catch { }
            }

            if (pageNavigationInfo != null)
            {
                IMessenger messenger = Container.Resolve<IMessenger>();
                var result = await NavigateAsync(pageNavigationInfo, messenger);
                if (result.IsSuccess)
                {
                    isRestored = true;
                }
            }

            if (isRestored is false)
            {
                await _primaryWindowCoreLayout.RestoreNavigationStack();
            }

            _window.Activate();
        }


        private async Task InitializeAsync()
        {
            

#if DEBUG
            foreach (var collectionName in Container.Resolve<ILiteDatabase>().GetCollectionNames())
            {
                Debug.WriteLine(collectionName);
            }
#endif

            await UpdateMigrationAsync();

            await MaintenanceAsync();

            //Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);

#if WINDOWS_UWP || WINDOWS
            //Resources.MergedDictionaries.Add(new Microsoft.UI.Xaml.Controls.XamlControlsResources());
#endif

            Resources["Strings"] = I18NPortable.I18N.Current;

            Resources["SmallImageWidth"] = ListingImageConstants.SmallFileThumbnailImageWidth;
            Resources["SmallImageHeight"] = ListingImageConstants.SmallFileThumbnailImageHeight;
            Resources["SmallImageRect"] = new Rect(new Point(), new Point(ListingImageConstants.SmallFileThumbnailImageHeight, ListingImageConstants.SmallFileThumbnailImageHeight));
            Resources["MidiumImageWidth"] = ListingImageConstants.MidiumFileThumbnailImageWidth;
            Resources["MidiumImageHeight"] = ListingImageConstants.MidiumFileThumbnailImageHeight;
            Resources["MidiumImageRect"] = new Rect(new Point(), new Point(ListingImageConstants.MidiumFileThumbnailImageHeight, ListingImageConstants.MidiumFileThumbnailImageHeight));
            Resources["LargeImageWidth"] = ListingImageConstants.LargeFileThumbnailImageWidth;
            Resources["LargeImageHeight"] = ListingImageConstants.LargeFileThumbnailImageHeight;
            Resources["LargeImageRect"] = new Rect(new Point(), new Point(ListingImageConstants.LargeFileThumbnailImageHeight, ListingImageConstants.LargeFileThumbnailImageHeight));

            UpdateFolderItemSizingResourceValues();

            //App.Current.Window.Content = Container.Resolve<PrimaryWindowCoreLayout>();
            //App.Current.Window.Activate();
        }

        public void UpdateFolderItemSizingResourceValues()
        {
            var folderListingSettings = Container.Resolve<Models.Domain.FolderItemListing.FolderListingSettings>();
            Resources["FolderItemTitleHeight"] = folderListingSettings.FolderItemTitleHeight;
            Resources["FolderGridViewItemWidth"] = folderListingSettings.FolderItemThumbnailImageSize.Width;
            Resources["FolderGridViewItemHeight"] = folderListingSettings.FolderItemThumbnailImageSize.Height;
        }



        private async ValueTask UpdateMigrationAsync()
        {
            if (SystemInformation.Instance.IsAppUpdated)
            {
                // Note: IsFirstRunを条件とした場合、設定をクリアされた結果として次回起動時が必ずIsFirstRunに引っかかってしまうことに注意

                Type[] migraterTypes = new[]
                {
                    typeof(DropSearchIndexDb),
                    typeof(DropPathReferenceCountDb),
                    typeof(DropIgnoreStorageItemDbWhenIdNotString),
                    typeof(MigrateAsyncStorageApplicationPermissionToDb),
                    typeof(MigrateLocalStorageHelperToApplicationDataStorageHelper),
                    typeof(DeleteThumbnailImagesOnTemporaryFolder),
                    typeof(DropFileDisplaySettingsWhenSortTypeAreUpdateTimeDescThenTitleAsc),
                };

                List<Exception> exceptions = new List<Exception>();
                await Task.Run(async () =>
                {
                    foreach (var migratorType in migraterTypes)
                    {
                        try
                        {
                            var migratorInstance = Container.Resolve(migratorType);
                            if (migratorInstance is IMigrater migrater && migrater.IsRequireMigrate)
                            {
                                Debug.WriteLine($"Start migrate: {migratorType.Name}");

                                migrater.Migrate();

                                Debug.WriteLine($"Done migrate: {migratorType.Name}");
                            }
                            else if (migratorInstance is IAsyncMigrater asyncMigrater && asyncMigrater.IsRequireMigrate)
                            {
                                Debug.WriteLine($"Start migrate: {migratorType.Name}");

                                await asyncMigrater.MigrateAsync();

                                Debug.WriteLine($"Done migrate: {migratorType.Name}");
                            }
                            else
                            {
                                Debug.WriteLine($"Skip migrate: {migratorType.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    }

                    if (exceptions.Any())
                    {
                        throw new AggregateException(exceptions);
                    }
                });
            }


        }

        public async Task MaintenanceAsync()
        {

            Type[] launchTimeMaintenanceTypes = new[]
            {
                typeof(SecondaryTileMaintenance),

                // v1.4.0 以前に 外部リンクをアプリにD&Dしたことがある場合、
                // StorageItem.Path == string.Empty となるためアプリの挙動が壊れてしまっていた問題に対処する
                typeof(RemoveSourceStorageItemWhenPathIsEmpty),

                // ソース管理に変更が加えられて、新規に管理するストレージアイテムが増えた・減った際に
                // ローカルDBや画像サムネイルの破棄などを行う
                // 単にソース管理が消されたからと破棄処理をしてしまうと包含関係のフォルダ追加を許容できなくなるので
                // 包含関係のフォルダに関するキャッシュの削除をスキップするような動作が含まれる
                typeof(CacheDeletionWhenSourceStorageItemIgnored),

                // 1.5.1以降に追加したお気に入り用のDB項目の存在を確実化
                typeof(EnsureFavoriteAlbam),
            };

            await Task.Run(async () =>
            {
                foreach (var maintenanceType in launchTimeMaintenanceTypes)
                {
                    var instance = Container.Resolve(maintenanceType);
                    if (instance is ILaunchTimeMaintenance restorable)
                    {
                        Debug.WriteLine($"Start maintenance: {maintenanceType.Name}");

                        try
                        {
                            restorable.Maintenance();
                            Debug.WriteLine($"Done maintenance: {maintenanceType.Name}");
                        }
                        catch
                        {
                            Debug.WriteLine($"Failed maintenance: {maintenanceType.Name}");
                        }
                    }
                    else if (instance is ILaunchTimeMaintenanceAsync restorableAsync)
                    {
                        Debug.WriteLine($"Start maintenance: {maintenanceType.Name}");

                        try
                        {
                            await restorableAsync.MaintenanceAsync();
                            Debug.WriteLine($"Done maintenance: {maintenanceType.Name}");
                        }
                        catch
                        {
                            Debug.WriteLine($"Failed maintenance: {maintenanceType.Name}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Skip maintenance: {maintenanceType.Name}");
                    }
                }
            });
        }






        public class PageNavigationInfo
        {
            public string Path { get; set; }

            public string PageName { get; set; }
        }

        async Task<INavigationResult> NavigateAsync(PageNavigationInfo info, IMessenger messenger = null)
        {
            messenger ??= Container.Resolve<IMessenger>();
            var sourceFolderRepository = Container.Resolve<SourceStorageItemsRepository>();

            NavigationParameters parameters = new NavigationParameters();


            Guard.IsNotNullOrEmpty(info.Path, nameof(info.Path));

            if (!string.IsNullOrEmpty(info.PageName))
            {
                parameters.Add(PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(PageNavigationConstants.MakeStorageItemIdWithPage(info.Path, info.PageName)));
            }
            else
            {
                parameters.Add(PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(info.Path));
            }

            var item = await sourceFolderRepository.GetStorageItemFromPath(info.Path);

            if (item is StorageFolder itemFolder)
            {
                var containerTypeManager = Container.Resolve<FolderContainerTypeManager>();
                if (await containerTypeManager.GetFolderContainerTypeWithCacheAsync(itemFolder, CancellationToken.None) == FolderContainerType.OnlyImages)
                {
                    return await messenger.NavigateAsync(nameof(Presentation.Views.ImageViewerPage), parameters, isForgetNavigation: true);
                }
                else
                {
                    return await messenger.NavigateAsync(nameof(Presentation.Views.FolderListupPage), parameters, isForgetNavigation: true);
                }
            }
            else if (item is StorageFile file)
            {
                // ファイル
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType)
                    || SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType)
                    )
                {
                    return await messenger.NavigateAsync(nameof(Presentation.Views.ImageViewerPage), parameters, isForgetNavigation: true);
                }
                else if (SupportedFileTypesHelper.IsSupportedEBookFileExtension(file.FileType))
                {
                    return await messenger.NavigateAsync(nameof(Presentation.Views.EBookReaderPage), parameters, isForgetNavigation: true);
                }
            }

            return null;
        }

        private PageNavigationInfo SecondatyTileArgumentToNavigationInfo(SecondaryTileArguments args)
        {
            var info = new PageNavigationInfo();
            info.Path = args.Path;
            info.PageName = args.PageName;

            return info;
        }



        async Task<PageNavigationInfo> FileActivatedArgumentToNavigationInfo(FileActivatedEventArgs args)
        {
            // 渡されたストレージアイテムをアプリ内部の管理ファイル・フォルダとして登録する
            var sourceStroageItemsRepo = Container.Resolve<SourceStorageItemsRepository>();
            string path = null;
            foreach (var item in args.Files)
            {
                if (item is StorageFile file)
                {
                    if (string.IsNullOrEmpty(file.Path))
                    {
                        continue;
                    }
                    else if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType))
                    {
                    }
                    else if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType))
                    {
                        NeighboringFilesQueryCache.AddNeighboringFilesQuery(file.Path, args.NeighboringFilesQuery);
                    }
                    else if (SupportedFileTypesHelper.IsSupportedEBookFileExtension(file.FileType))
                    {
                    }
                    else { continue; }

                    await sourceStroageItemsRepo.AddFileTemporaryAsync(file, SourceOriginConstants.FileActivation);
                    path = file.Path;
                    break;
                }
            }

            // 渡された先頭のストレージアイテムのみを画像ビューワーページで開く
            if (path != null)
            {
                return new PageNavigationInfo()
                {
                    Path = path,
                };
            }

            return null;
        }
    }
}
