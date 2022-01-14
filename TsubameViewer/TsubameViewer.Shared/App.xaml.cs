using LiteDB;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.Helpers;
using Prism;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Unity;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Models.UseCase;
using TsubameViewer.Models.UseCase.Maintenance;
using TsubameViewer.Models.UseCase.Migrate;
using TsubameViewer.Presentation.Services.UWP;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.Views;
using Unity;
using Uno.Extensions;
using Uno.Threading;
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

namespace TsubameViewer
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : PrismApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            ConfigureFilters(global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory);

            this.InitializeComponent();
            this.Suspending += OnSuspending;
            UnhandledException += App_UnhandledException;

            RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;
            
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            ConnectedAnimationService.GetForCurrentView().DefaultDuration = TimeSpan.FromMilliseconds(150);
        }

        Models.Infrastructure.AsyncLock _InitializeLock = new ();

        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            Debug.WriteLine(e.Message);
            Debug.WriteLine(e.Exception.ToString());
        }

        public override void ConfigureViewModelLocator()
        {
            ViewModelLocationProvider.SetDefaultViewTypeToViewModelTypeResolver(viewType =>
            {
                var pageToken = viewType.Name;

                if (pageToken.EndsWith("_TV"))
                {
                    pageToken = pageToken.Remove(pageToken.IndexOf("_TV"));
                }
                else if (pageToken.EndsWith("_Mobile"))
                {
                    pageToken = pageToken.Remove(pageToken.IndexOf("_Mobile"));
                }

                var assemblyQualifiedAppType = viewType.AssemblyQualifiedName;

                var pageNameWithParameter = assemblyQualifiedAppType.Replace(viewType.FullName, "TsubameViewer.Presentation.ViewModels.{0}ViewModel");

                var viewModelFullName = string.Format(CultureInfo.InvariantCulture, pageNameWithParameter, pageToken);
                var viewModelType = Type.GetType(viewModelFullName);

                if (viewModelType == null)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, pageToken, this.GetType().Namespace + ".ViewModels"),
                        "pageToken");
                }

                return viewModelType;

            });
            base.ConfigureViewModelLocator();
        }

        protected override void RegisterRequiredTypes(IContainerRegistry container)
        {
            var unityContainer = container.GetContainer();
            container.RegisterInstance<ILiteDatabase>(new LiteDatabase($"Filename={Path.Combine(ApplicationData.Current.LocalFolder.Path, "tsubame.db")}; Async=false;"));

            unityContainer.RegisterInstance<ILiteDatabase>("TemporaryDb", new LiteDatabase($"Filename={Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "tsubame_temp.db")}; Async=false;"));

            unityContainer.RegisterInstance<IScheduler>(new SynchronizationContextScheduler(System.Threading.SynchronizationContext.Current));
            
            base.RegisterRequiredTypes(container);
        }

        

        public override void RegisterTypes(IContainerRegistry container)
        {
            container.RegisterInstance<IMessenger>(WeakReferenceMessenger.Default);
            container.RegisterSingleton<Models.Domain.ImageViewer.ImageViewerSettings>();
            container.RegisterSingleton<Models.Domain.FolderItemListing.FolderListingSettings>();

            container.RegisterSingleton<Presentation.Services.UWP.SecondaryTileManager>();

            container.RegisterSingleton<Models.UseCase.Maintenance.CacheDeletionWhenSourceStorageItemIgnored>();
            
            container.RegisterSingleton<SourceStorageItemsPageViewModel>();
            //container.RegisterSingleton<ImageListupPageViewModel>();
            //container.RegisterSingleton<FolderListupPageViewModel>();
            container.RegisterSingleton<ImageViewerPageViewModel>();
            container.RegisterSingleton<EBookReaderPageViewModel>();
            //container.RegisterSingleton<SearchResultPageViewModel>();

            container.RegisterForNavigation<SourceStorageItemsPage>();
            container.RegisterForNavigation<ImageListupPage>();
            container.RegisterForNavigation<FolderListupPage>();
            container.RegisterForNavigation<ImageViewerPage>();
            container.RegisterForNavigation<EBookReaderPage>();
            container.RegisterForNavigation<SettingsPage>();
            container.RegisterForNavigation<SearchResultPage>();
            container.RegisterForNavigation<AlbamListupPage>();
        }

        bool isRestored = false;
        public override async Task OnStartAsync(StartArgs args)
        {
            using var releaser = await _InitializeLock.LockAsync(default);
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif
            Resources["DebugTVMode"] = Container.Resolve<ApplicationSettings>().ForceXboxAppearanceModeEnabled;

            if (args.Arguments is IActivatedEventArgs activated)
            {
                SystemInformation.Instance.TrackAppUse(activated);
            }

            PageNavigationInfo pageNavigationInfo = null;
            if (args.Arguments is LaunchActivatedEventArgs launchActivatedEvent)
            {
                if (launchActivatedEvent.PrelaunchActivated == false)
                {
                    // Ensure the current window is active
                    Windows.UI.Xaml.Window.Current.Activate();

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
            else if (args.Arguments is FileActivatedEventArgs fileActivatedEventArgs)
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
                if (result.Success)
                {
                    isRestored = true;
                    
                }
            }

            if (isRestored is false)
            {
                var shell = Window.Current.Content as PrimaryWindowCoreLayout;
                await shell.RestoreNavigationStack();
            }

            await base.OnStartAsync(args);
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

        public override async void OnInitialized()
        {
            using var releaser = await _InitializeLock.LockAsync(default);

#if DEBUG
            Container.Resolve<ILiteDatabase>().GetCollectionNames().ForEach((string x) => Debug.WriteLine(x));
#endif

            await UpdateMigrationAsync();

            await MaintenanceAsync();

            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);

#if WINDOWS_UWP
            Resources.MergedDictionaries.Add(new Microsoft.UI.Xaml.Controls.XamlControlsResources());
#endif
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

            var applicationSettings = Container.Resolve<Models.Domain.ApplicationSettings>();
            try
            {
                I18NPortable.I18N.Current.Locale = applicationSettings.Locale ?? I18NPortable.I18N.Current.Languages.FirstOrDefault(x => x.Locale.StartsWith(CultureInfo.CurrentCulture.Name))?.Locale;
            }
            catch 
            {
                I18NPortable.I18N.Current.Locale = "en-US";
            }

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

            Window.Current.Content = Container.Resolve<PrimaryWindowCoreLayout>();
            Window.Current.Activate();

            // セカンダリタイル管理の初期化
            _ = Container.Resolve<Presentation.Services.UWP.SecondaryTileManager>().InitializeAsync().ConfigureAwait(false);
                        
            base.OnInitialized();
        }

        public void UpdateFolderItemSizingResourceValues()
        {
            var folderListingSettings = Container.Resolve<Models.Domain.FolderItemListing.FolderListingSettings>();
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


        /// <summary>
        /// Configures global logging
        /// </summary>
        /// <param name="factory"></param>
        static void ConfigureFilters(ILoggerFactory factory)
        {
            factory
                .WithFilter(new FilterLoggerSettings
                    {
                        { "Uno", LogLevel.Warning },
                        { "Windows", LogLevel.Warning },

						// Debug JS interop
						// { "Uno.Foundation.WebAssemblyRuntime", LogLevel.Debug },

						// Generic Xaml events
						// { "Windows.UI.Xaml", LogLevel.Debug },
						// { "Windows.UI.Xaml.VisualStateGroup", LogLevel.Debug },
						// { "Windows.UI.Xaml.StateTriggerBase", LogLevel.Debug },
						// { "Windows.UI.Xaml.UIElement", LogLevel.Debug },

						// Layouter specific messages
						// { "Windows.UI.Xaml.Controls", LogLevel.Debug },
						// { "Windows.UI.Xaml.Controls.Layouter", LogLevel.Debug },
						// { "Windows.UI.Xaml.Controls.Panel", LogLevel.Debug },
						// { "Windows.Storage", LogLevel.Debug },

						// Binding related messages
						// { "Windows.UI.Xaml.Data", LogLevel.Debug },

						// DependencyObject memory references tracking
						// { "ReferenceHolder", LogLevel.Debug },

						// ListView-related messages
						// { "Windows.UI.Xaml.Controls.ListViewBase", LogLevel.Debug },
						// { "Windows.UI.Xaml.Controls.ListView", LogLevel.Debug },
						// { "Windows.UI.Xaml.Controls.GridView", LogLevel.Debug },
						// { "Windows.UI.Xaml.Controls.VirtualizingPanelLayout", LogLevel.Debug },
						// { "Windows.UI.Xaml.Controls.NativeListViewBase", LogLevel.Debug },
						// { "Windows.UI.Xaml.Controls.ListViewBaseSource", LogLevel.Debug }, //iOS
						// { "Windows.UI.Xaml.Controls.ListViewBaseInternalContainer", LogLevel.Debug }, //iOS
						// { "Windows.UI.Xaml.Controls.NativeListViewBaseAdapter", LogLevel.Debug }, //Android
						// { "Windows.UI.Xaml.Controls.BufferViewCache", LogLevel.Debug }, //Android
						// { "Windows.UI.Xaml.Controls.VirtualizingPanelGenerator", LogLevel.Debug }, //WASM
					}
                )
#if DEBUG
				.AddConsole(LogLevel.Debug);
#else
                .AddConsole(LogLevel.Information);
#endif
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
            else if  (item is StorageFile file)
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
