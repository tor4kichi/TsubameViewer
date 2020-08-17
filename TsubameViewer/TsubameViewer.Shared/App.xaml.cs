using LiteDB;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
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
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.Services.UWP;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.Views;
using Unity;
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
        }

        FastAsyncLock _InitializeLock = new FastAsyncLock();

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
            string connectionString = $"Filename={Path.Combine(ApplicationData.Current.LocalFolder.Path, "tsubame.db")}; Async=false;";
            var db = new LiteDatabase(connectionString);
            container.RegisterInstance<ILiteDatabase>(db);

            var unityContainer = container.GetContainer();
            unityContainer.RegisterInstance<IScheduler>(new SynchronizationContextScheduler(System.Threading.SynchronizationContext.Current));
            
            base.RegisterRequiredTypes(container);
        }

        

        public override void RegisterTypes(IContainerRegistry container)
        {
            container.RegisterSingleton<Models.Domain.ImageViewer.ImageViewerSettings>();
            container.RegisterSingleton<Models.Domain.FolderItemListing.FolderListingSettings>();

            container.RegisterSingleton<Presentation.Services.UWP.SecondaryTileManager>();

            container.RegisterSingleton<Models.UseCase.ApplicationDataUpdateWhenPathReferenceCountChanged>();
            container.RegisterSingleton<Models.UseCase.PathReferenceCountUpdateWhenSourceManagementChanged>();
            container.RegisterInstance(new RecyclableMemoryStreamManager());

            container.RegisterForNavigation<SourceStorageItemsPage>();
            container.RegisterForNavigation<FolderListupPage>();
            container.RegisterForNavigation<ImageViewerPage>();
            container.RegisterForNavigation<EBookReaderPage>();
            container.RegisterForNavigation<CollectionPage>();
            container.RegisterForNavigation<SettingsPage>();
            container.RegisterForNavigation<SearchResultPage>();

        }

        bool isRestored = false;
        public override async Task OnStartAsync(StartArgs args)
        {
            using var releaser = await _InitializeLock.LockAsync(default);
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif



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
                            var navigationInfo = SecondatyTileArgumentToNavigationInfo(tileArgs);
                            var navigationService = Container.Resolve<INavigationService>("PrimaryWindowNavigationService");
                            await navigationService.NavigateAsync(nameof(Presentation.Views.SourceStorageItemsPage));
                            var result = await NavigateAsync(navigationInfo);
                            if (result.Success)
                            {
                                isRestored = true;
                            }
                        }
                        catch { }
                    }
                }
            }
            else if (args.Arguments is FileActivatedEventArgs fileActivatedEventArgs)
            {
                try
                {
                    var navigationInfo = await FileActivatedArgumentToNavigationInfo(fileActivatedEventArgs);

                    if (navigationInfo != null)
                    {
                        var navigationService = Container.Resolve<INavigationService>("PrimaryWindowNavigationService");
                        await navigationService.NavigateAsync(nameof(Presentation.Views.SourceStorageItemsPage));
                        var result = await NavigateAsync(navigationInfo);
                        if (result.Success)
                        {
                            isRestored = true;
                        }
                    }
                }
                catch { }
            }

            if (!isRestored)
            {
                isRestored = true;
                var shell = Window.Current.Content as  PrimaryWindowCoreLayout;
                await shell.RestoreNavigationStack();
            }

            await base.OnStartAsync(args);
        }

        public override async void OnInitialized()
        {
            using var releaser = await _InitializeLock.LockAsync(default);

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
            Resources["MidiumImageWidth"] = ListingImageConstants.MidiumFileThumbnailImageWidth;
            Resources["MidiumImageHeight"] = ListingImageConstants.MidiumFileThumbnailImageHeight;
            Resources["LargeImageWidth"] = ListingImageConstants.LargeFileThumbnailImageWidth;
            Resources["LargeImageHeight"] = ListingImageConstants.LargeFileThumbnailImageHeight;

            var shell = Container.Resolve<PrimaryWindowCoreLayout>();
            var ns = shell.GetNavigationService();
            var unityContainer = Container.GetContainer();
            unityContainer.RegisterInstance<INavigationService>("PrimaryWindowNavigationService", ns);

            Window.Current.Content = shell;
            Window.Current.Activate();

            // セカンダリタイル管理の初期化
            _ = Container.Resolve<Presentation.Services.UWP.SecondaryTileManager>().InitializeAsync().ConfigureAwait(false);
            
            // ソース管理に変更が加えられた時にアプリが把握しているストレージアイテムのパスと
            // そのストレージアイテムを引く際に必要なフォルダアクセス権とを紐付ける
            Container.Resolve<Models.UseCase.PathReferenceCountUpdateWhenSourceManagementChanged>().Initialize();

            // ソース管理に変更が加えられて、新規に管理するストレージアイテムが増えた・減った際に
            // ローカルDBや画像サムネイルの破棄などを行う
            // 単にソース管理が消されたからと破棄処理をしてしまうと包含関係のフォルダ追加を許容できなくなるので
            // ストレージアイテムのパスごとに参照数を管理し、参照がゼロになった時に限って破棄するような仕組みにしている
            Container.Resolve<Models.UseCase.ApplicationDataUpdateWhenPathReferenceCountChanged>();
            
            base.OnInitialized();
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

        async Task<INavigationResult> NavigateAsync(PageNavigationInfo info)
        {
            var navigationService = Container.Resolve<INavigationService>("PrimaryWindowNavigationService");
            var sourceFolderRepository = Container.Resolve<SourceStorageItemsRepository>();
            var referenceCountRepository = Container.Resolve<PathReferenceCountManager>();

            string tokenGettingPath = info.Path;
            if (SupportedFileTypesHelper.IsSupportedImageFileExtension(Path.GetExtension(info.Path)))
            {
                tokenGettingPath = Path.GetDirectoryName(info.Path);
            }

            var token = referenceCountRepository.GetTokens(tokenGettingPath)?.FirstOrDefault();
           
            if (token == null) { return null; }

            NavigationParameters parameters = new NavigationParameters();


            if (string.IsNullOrEmpty(info.Path))
            {
                throw new NullReferenceException("PageNavigationInfo.Path is can not be null.");
            }

            parameters.Add(PageNavigationConstants.Path, info.Path);

            if (!string.IsNullOrEmpty(info.PageName))
            {
                parameters.Add(PageNavigationConstants.PageName, info.PageName);
            }

            

            var item = await sourceFolderRepository.GetStorageItemFromPath(token, info.Path);
            if (item is StorageFolder itemFolder)
            {
                var containerTypeManager = Container.Resolve<FolderContainerTypeManager>();
                if (await containerTypeManager.GetFolderContainerType(itemFolder) == FolderContainerType.OnlyImages)
                {
                    return await navigationService.NavigateAsync(nameof(Presentation.Views.ImageViewerPage), parameters, new SuppressNavigationTransitionInfo());
                }
                else
                {
                    return await navigationService.NavigateAsync(nameof(Presentation.Views.FolderListupPage), parameters, new DrillInNavigationTransitionInfo());
                }
            }
            else if  (item is StorageFile file)
            {
                // ファイル
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType)
                    || SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType)
                    )
                {
                    return await navigationService.NavigateAsync(nameof(Presentation.Views.ImageViewerPage), parameters, new SuppressNavigationTransitionInfo());
                }
                else if (SupportedFileTypesHelper.IsSupportedEBookFileExtension(file.FileType))
                {
                    return await navigationService.NavigateAsync(nameof(Presentation.Views.EBookReaderPage), parameters, new SuppressNavigationTransitionInfo());
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
                    if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType))
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
            // TODO: FileActivationで開いた画像ビューワーページのバックナビゲーション先をSourceStorageItemsPageにする？
            // TODO: ファイルアクティべーション時、フォルダを渡された際、フォルダ内が画像のみなら画像ビューワーで開きたい
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
