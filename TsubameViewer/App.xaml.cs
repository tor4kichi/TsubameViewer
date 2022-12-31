using DryIoc;
using I18NPortable;
using LiteDB;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Core.UseCases.Maintenance;
using TsubameViewer.Core.UseCases.Migrate;
using TsubameViewer.Services.Navigation;
using TsubameViewer.Services;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views;
using TsubameViewer.Views.Dialogs;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Maintenance;
using TsubameViewer.Core.Contracts.Maintenance;
using TsubameViewer.Core.Services;
using TsubameViewer.Core.Service;
using TsubameViewer.Contracts.Services;
using TsubameViewer.Contracts.Navigation;

namespace TsubameViewer;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
sealed partial class App : Application
{
    public new static App Current => (App)Application.Current;

    public Container Container { get; }

    Core.AsyncLock _InitializeLock = new();


    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
        this.Suspending += OnSuspending;
        UnhandledException += App_UnhandledException;

        RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;
        
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        ConnectedAnimationService.GetForCurrentView().DefaultDuration = TimeSpan.FromMilliseconds(150);

        Container = ConfigureService();

#if DEBUG
        if (System.Diagnostics.Debugger.IsAttached)
        {
            this.DebugSettings.EnableFrameRateCounter = false;
        }
#endif
        // App.xamlで宣言してるコントロール内でローカライズ処理が走るため、それより先に初期化したい
        InitializeLocalization();

        EnsureFavoriteAlbam.FavoriteAlbamTitle = "FavoriteAlbam".Translate();
        RecentlyAccessService.MaxRecordCount = 100;
    }


    private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        Debug.WriteLine(e.Message);
        Debug.WriteLine(e.Exception.ToString());
    }

    private Container ConfigureService()
    {
        var rules = Rules.Default
            .WithAutoConcreteTypeResolution()
            .With(Made.Of(FactoryMethod.ConstructorWithResolvableArguments))
            .WithoutThrowOnRegisteringDisposableTransient()
            .WithFuncAndLazyWithoutRegistration()
            .WithDefaultIfAlreadyRegistered(IfAlreadyRegistered.Replace);

        var container = new Container(rules);

        RegisterRequiredTypes(container);
        RegisterTypes(container);

        Ioc.Default.ConfigureServices(container);
        return container;
    }

    private void RegisterRequiredTypes(Container container)
    {
        container.RegisterInstance<ILiteDatabase>(new LiteDatabase($"Filename={Path.Combine(ApplicationData.Current.LocalFolder.Path, "tsubame.db")}; Async=false;"));
        container.RegisterInstance<ILiteDatabase>(new LiteDatabase($"Filename={Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "tsubame_temp.db")}; Async=false;"), serviceKey: "TemporaryDb");

        container.RegisterInstance<IStorageHelper>(new BytesApplicationDataStorageHelper(ApplicationData.Current, new BinaryJsonObjectSerializer()));
        container.RegisterInstance<IScheduler>(new SynchronizationContextScheduler(System.Threading.SynchronizationContext.Current));
        container.Register<IViewLocator, ViewLocator>();
        container.Register<IImageCodecService, ImageCodecService>(made: Parameters.Of.Name("assetUrl", x => new Uri("ms-appx:///Assets/ImageCodecExtensions.json")));
        container.Register<ISplitImageInputDialogService, SplitImageInputDialogService>();
        container.Register<IBookmarkService, LocalBookmarkService>(reuse: new SingletonReuse());
        container.Register<IRestoreNavigationService, RestoreNavigationService>(reuse: new SingletonReuse());
        container.Register<IFolderLastIntractItemService, FolderLastIntractItemService>(reuse: new SingletonReuse());
        container.Register<IRecentlyAccessService, RecentlyAccessService>(reuse: new SingletonReuse());
        container.Register<ThumbnailImageService>(reuse: new SingletonReuse(), made: Parameters.Of.Name("temporaryDb", serviceKey: "TemporaryDb"));
        container.RegisterMapping<IThumbnailImageService, ThumbnailImageService>();
        container.RegisterMapping<ISecondaryTileThumbnailImageService, ThumbnailImageService>();
        container.RegisterMapping<IThumbnailImageMaintenanceService, ThumbnailImageService>();
        
        container.Register<PrimaryWindowCoreLayout>(reuse: new SingletonReuse());
        container.Register<SourceStorageItemsPage>();
        container.Register<ImageListupPage>();
        container.Register<FolderListupPage>();
        container.Register<ImageViewerPage>();
        container.Register<EBookReaderPage>();
        container.Register<SettingsPage>();
        container.Register<SearchResultPage>();
        container.Register<AlbamListupPage>();
        container.Register<FolderOrArchiveRestructurePage>();
    }

    private void RegisterTypes(Container container)
    {
        container.RegisterInstance<IMessenger>(WeakReferenceMessenger.Default);
        container.Register<Core.Models.ImageViewer.ImageViewerSettings>(reuse: new SingletonReuse());
        container.Register<Core.Models.FolderItemListing.FolderListingSettings>(reuse: new SingletonReuse());
        container.Register<FileControlSettings>(reuse: new SingletonReuse());
        container.Register<ApplicationSettings>(reuse: new SingletonReuse());

        container.Register<ISecondaryTileManager, SecondaryTileManager>(reuse: new SingletonReuse());

        container.Register<SecondaryTileMaintenance>();
        container.Register<RemoveSourceStorageItemWhenPathIsEmpty>();
        container.Register<CacheDeletionWhenSourceStorageItemIgnored>();
        container.Register<EnsureFavoriteAlbam>();
        container.RegisterMapping<ILaunchTimeMaintenanceAsync, SecondaryTileMaintenance>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
        container.RegisterMapping<ILaunchTimeMaintenanceAsync, RemoveSourceStorageItemWhenPathIsEmpty>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
        container.RegisterMapping<ILaunchTimeMaintenance, CacheDeletionWhenSourceStorageItemIgnored>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
        container.RegisterMapping<ILaunchTimeMaintenance, EnsureFavoriteAlbam>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
        
        container.Register<ILaunchTimeMaintenance, RecentlyAccessService>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);

        container.Register<IAsyncMigrater, DropSearchIndexDb>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
        container.Register<IAsyncMigrater, DropPathReferenceCountDb>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
        container.Register<IAsyncMigrater, DropIgnoreStorageItemDbWhenIdNotString>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
        container.Register<IAsyncMigrater, MigrateAsyncStorageApplicationPermissionToDb>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
        container.Register<IAsyncMigrater, MigrateLocalStorageHelperToApplicationDataStorageHelper>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
        container.Register<IAsyncMigrater, DeleteThumbnailImagesOnTemporaryFolder>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
        container.Register<IAsyncMigrater, DropFileDisplaySettingsWhenSortTypeAreUpdateTimeDescThenTitleAsc>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
#if DEBUG
        container.Register<IAsyncMigrater, DummyMigrator>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
#endif


        container.Register<SourceStorageItemsPageViewModel>(reuse: new SingletonReuse());
        //container.Register<ImageListupPageViewModel>(reuse: new SingletonReuse());
        //container.Register<FolderListupPageViewModel>(reuse: new SingletonReuse());
        container.Register<ImageViewerPageViewModel>(reuse: new SingletonReuse());
        container.Register<EBookReaderPageViewModel>(reuse: new SingletonReuse());
        //container.Register<SearchResultPageViewModel>(reuse: new SingletonReuse());

        // Services
        container.Register<IMessageDialogService, MessageDialogService>();
        container.Register<IFileControlDialogService, FileControlDialogService>();
        container.Register<IAlbamDialogService, AlbamDialogService>();
        container.Register<IStorageItemDeleteConfirmation, StorageItemDeleteConfirmDialog>();
        container.Register<ISplitImageInputDialogService, SplitImageInputDialogService>();
    }
#if DEBUG
    class DummyMigrator : IAsyncMigrater
    {
        Version IAsyncMigrater.TargetVersion { get; } = new Version(1, 8, 6);

        ValueTask IAsyncMigrater.MigrateAsync()
        {
            return new ValueTask();
        }
    }
#endif

    protected override async void OnFileActivated(FileActivatedEventArgs args)
    {
        await InitializeAsync();
        await OnActivationAsync(args);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await InitializeAsync();
        await OnActivationAsync(args);
    }

    void InitializeLocalization()
    {
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

        var applicationSettings = Ioc.Default.GetService<Core.Models.ApplicationSettings>();
        try
        {
            I18NPortable.I18N.Current.Locale = applicationSettings.Locale ?? I18NPortable.I18N.Current.Languages.FirstOrDefault(x => x.Locale.StartsWith(CultureInfo.CurrentCulture.Name))?.Locale;
        }
        catch
        {
            I18NPortable.I18N.Current.Locale = "en-US";
        }
    }        

    bool _isRestored = false;
    public async Task OnActivationAsync(IActivatedEventArgs args)
    {
        using var releaser = await _InitializeLock.LockAsync(default);

        if (args is IActivatedEventArgs activated)
        {
            SystemInformation.Instance.TrackAppUse(activated);
        }
        
        if (args is LaunchActivatedEventArgs launchActivatedEvent
            && launchActivatedEvent.PrelaunchActivated == false
            && !string.IsNullOrEmpty(launchActivatedEvent.Arguments)
            )
        {
            // Ensure the current window is active
            Windows.UI.Xaml.Window.Current.Activate();

            try
            {
                if (!SecondaryTileManager.DeserializeSecondaryTileArguments(launchActivatedEvent.Arguments, out var tileArgs))
                {
                    return;
                }

                if (await SecondatyTileArgumentNavigationAsync(tileArgs) is not null and var result && result.IsSuccess)
                {
                    _isRestored = true;
                }
            }
            catch
            {
                return;
            }
        }
        else if (args is FileActivatedEventArgs fileActivatedEventArgs)
        {
            try
            {
                if (await FileActivatedNavigationAsync(fileActivatedEventArgs) is not null and var result && result.IsSuccess)
                {
                    _isRestored = true;
                }
            }
            catch 
            {
                return;
            }
        }       

        if (_isRestored is false)
        {
            var shell = Window.Current.Content as PrimaryWindowCoreLayout;
            shell.RestoreNavigationStack();
        }
    }


    private async ValueTask UpdateMigrationAsync()
    {
        if (SystemInformation.Instance.IsAppUpdated)
        {
            // Note: IsFirstRunを条件とした場合、設定をクリアされた結果として次回起動時が必ずIsFirstRunに引っかかってしまうことに注意

            List<Exception> exceptions = new List<Exception>();
            await Task.Run(async () =>
            {
                Version prevVersion = new Version(SystemInformation.Instance.PreviousVersionInstalled.ToFormattedString(3));
                foreach (var migrater in Container.ResolveMany<IAsyncMigrater>())
                {
                    try
                    {
                        var migratorType = migrater.GetType();
                        if (migrater.TargetVersion == null || migrater.TargetVersion >= prevVersion)
                        {
                            Debug.WriteLine($"Start migrate: {migratorType.Name}");

                            await migrater.MigrateAsync();

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
        foreach (var maintenanceInst in Container.ResolveMany<ILaunchTimeMaintenance>())
        {
            var maintenanceType = maintenanceInst.GetType();
            Debug.WriteLine($"Start maintenance: {maintenanceType.Name}");

            try
            {
                maintenanceInst.Maintenance();
                Debug.WriteLine($"Done maintenance: {maintenanceType.Name}");
            }
            catch
            {
                Debug.WriteLine($"Failed maintenance: {maintenanceType.Name}");
            } 
        }

        await Task.Run(async () =>
        {
            foreach (var maintenanceInst in Container.ResolveMany<ILaunchTimeMaintenanceAsync>())
            {
                var maintenanceType = maintenanceInst.GetType();
                Debug.WriteLine($"Start maintenance: {maintenanceType.Name}");

                try
                {
                    await maintenanceInst.MaintenanceAsync();
                    Debug.WriteLine($"Done maintenance: {maintenanceType.Name}");
                }
                catch
                {
                    Debug.WriteLine($"Failed maintenance: {maintenanceType.Name}");
                }
            }               
        });
    }

    bool _isInitialized = false;

    private async Task InitializeAsync()
    {
        using var releaser = await _InitializeLock.LockAsync(default);

        if (_isInitialized) { return; }

        _isInitialized = true;
#if DEBUG
        Resources["DebugTVMode"] = Ioc.Default.GetService<ApplicationSettings>().ForceXboxAppearanceModeEnabled;
#else
        Resources["DebugTVMode"] = false;
#endif

#if DEBUG
        foreach (var collectionName in Ioc.Default.GetService<ILiteDatabase>().GetCollectionNames())
        {
            Debug.WriteLine(collectionName);
        }
#endif

        await UpdateMigrationAsync();

        await MaintenanceAsync();

        Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);


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

        Window.Current.Content = Ioc.Default.GetService<PrimaryWindowCoreLayout>();
        Window.Current.Activate();
    }

    public void UpdateFolderItemSizingResourceValues()
    {
        var folderListingSettings = Ioc.Default.GetService<Core.Models.FolderItemListing.FolderListingSettings>();
        Resources["FolderItemTitleHeight"] = folderListingSettings.FolderItemTitleHeight;
        Resources["FolderGridViewItemWidth"] = folderListingSettings.FolderItemThumbnailImageSize.Width;
        Resources["FolderGridViewItemHeight"] = folderListingSettings.FolderItemThumbnailImageSize.Height;
    }



    /// <summary>
    /// Invoked when application execution is being suspended.  Application state is saved
    /// without knowing whether the application will be terminated or resumed with the contents
    /// of memory still intact.
    /// </summary>
    /// <param name="sender">The source of the suspend request.</param>
    /// <param name="e">Details about the suspend request.</param>
    private void OnSuspending(object sender, SuspendingEventArgs e)
    {
        var deferral = e.SuspendingOperation.GetDeferral();
        deferral.Complete();
    }


    static async Task<INavigationResult> NavigateAsync(string path, string pageName, IMessenger messenger = null)
    {
        Guard.IsNotNullOrEmpty(path, nameof(path));

        messenger ??= Ioc.Default.GetService<IMessenger>();
        var sourceFolderRepository = Ioc.Default.GetService<SourceStorageItemsRepository>();

        NavigationParameters parameters = new NavigationParameters();
        
        if (!string.IsNullOrEmpty(pageName))
        {
            parameters.Add(PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(PageNavigationConstants.MakeStorageItemIdWithPage(path, pageName)));
        }
        else
        {
            parameters.Add(PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(path));
        }

        var item = await sourceFolderRepository.TryGetStorageItemFromPath(path);
        if (item is StorageFolder itemFolder)
        {
            var containerTypeManager = Ioc.Default.GetService<FolderContainerTypeManager>();
            if (await containerTypeManager.GetFolderContainerTypeWithCacheAsync(itemFolder, CancellationToken.None) == FolderContainerType.OnlyImages)
            {
                return await messenger.NavigateAsync(nameof(Views.ImageViewerPage), parameters, isForgetNavigation: true);
            }
            else
            {
                return await messenger.NavigateAsync(nameof(Views.FolderListupPage), parameters, isForgetNavigation: true);
            }
        }
        else if  (item is StorageFile file)
        {
            // ファイル
            if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType)
                || SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType)
                )
            {
                return await messenger.NavigateAsync(nameof(Views.ImageViewerPage), parameters, isForgetNavigation: true);
            }
            else if (SupportedFileTypesHelper.IsSupportedEBookFileExtension(file.FileType))
            {
                return await messenger.NavigateAsync(nameof(Views.EBookReaderPage), parameters, isForgetNavigation: true);
            }
        }

        return null;
    }

    static async Task<INavigationResult> NavigateAsync(IStorageItem storageItem, IMessenger messenger = null)
    {
        messenger ??= Ioc.Default.GetService<IMessenger>();
        var sourceFolderRepository = Ioc.Default.GetService<SourceStorageItemsRepository>();

        NavigationParameters parameters = new NavigationParameters();
        parameters.Add(PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(storageItem.Path));

        if (storageItem is StorageFolder folder)
        {
            var containerTypeManager = Ioc.Default.GetService<FolderContainerTypeManager>();
            if (await containerTypeManager.GetFolderContainerTypeWithCacheAsync(folder, CancellationToken.None) == FolderContainerType.OnlyImages)
            {
                return await messenger.NavigateAsync(nameof(Views.ImageViewerPage), parameters, isForgetNavigation: true);
            }
            else
            {
                return await messenger.NavigateAsync(nameof(Views.FolderListupPage), parameters, isForgetNavigation: true);
            }
        }
        else if (storageItem is StorageFile file)
        {
            if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType)
                || SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType)
                )
            {
                return await messenger.NavigateAsync(nameof(Views.ImageViewerPage), parameters, isForgetNavigation: true);
            }
            else if (SupportedFileTypesHelper.IsSupportedEBookFileExtension(file.FileType))
            {
                return await messenger.NavigateAsync(nameof(Views.EBookReaderPage), parameters, isForgetNavigation: true);
            }
        }
        
        return null;
    }

    static async Task<INavigationResult> SecondatyTileArgumentNavigationAsync(SecondaryTileArguments args)
    {
        return await NavigateAsync(args.Path, args.PageName);
    }



    static async Task<INavigationResult> FileActivatedNavigationAsync(FileActivatedEventArgs args)
    {
        // 渡されたストレージアイテムをアプリ内部の管理ファイル・フォルダとして登録する
        var sourceStroageItemsRepo = Ioc.Default.GetService<SourceStorageItemsRepository>();
        IStorageItem? firstItem = null;
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
                firstItem ??= file;
                break;
            }           
        }

        // 渡された先頭のストレージアイテムのみを画像ビューワーページで開く
        if (firstItem != null)
        {
            return await NavigateAsync(firstItem);
        }
        else
        {
            return new NavigationResult() { IsSuccess = true };
        }
    }
}
