using LiteDB;
using Microsoft.Extensions.Logging;
using MonkeyCache;
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
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Presentation.Views;
using Unity;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
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
        }

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

            MonkeyCache.BarrelUtils.SetBaseCachePath(ApplicationData.Current.LocalFolder.Path);

            var unityContainer = container.GetContainer();
            var thumbnailBarell = MonkeyCache.LiteDB.Barrel.Create(Path.Combine(ApplicationData.Current.LocalFolder.Path, "thumbnail_cache.db"), true);
            unityContainer.RegisterInstance<IBarrel>("ThumbnailBarrel", thumbnailBarell);

            unityContainer.RegisterInstance<IScheduler>(new SynchronizationContextScheduler(System.Threading.SynchronizationContext.Current));
            
            base.RegisterRequiredTypes(container);
        }

        

        public override void RegisterTypes(IContainerRegistry container)
        {
            container.RegisterSingleton<Models.Domain.ImageViewer.ImageViewerSettings>();
            container.RegisterSingleton<Models.Domain.FolderItemListing.FolderListingSettings>();

            container.RegisterForNavigation<SourceFoldersPage>();
            container.RegisterForNavigation<FolderListupPage>();
            container.RegisterForNavigation<ImageViewerPage>();
            container.RegisterForNavigation<CollectionPage>();
            container.RegisterForNavigation<SettingsPage>();
        }

        public override Task OnStartAsync(StartArgs args)
        {
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
                }
            }
            else if (args.Arguments is FileActivatedEventArgs fileActivatedEventArgs)
            {
                OnFileActivated(fileActivatedEventArgs);
            }

            return base.OnStartAsync(args);
        }



        public override void OnInitialized()
        {
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
                    .SetFallbackLocale("ja")
                    .Init(GetType().Assembly);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
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

            ns.NavigateAsync(nameof(SourceFoldersPage));

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



        private void OnFileActivated(FileActivatedEventArgs args)
        {
            // TODO: Handle file activation
            // The number of files received is args.Files.Size
            // The name of the first file is args.Files[0].Name
            //args.NeighboringFilesQuery
        }
    }
}
