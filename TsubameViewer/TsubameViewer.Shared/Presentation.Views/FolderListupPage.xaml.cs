using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Toolkit.Uwp.UI.Animations.Effects;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Xamarin.Essentials;
using Microsoft.Toolkit.Uwp.UI.Extensions;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Reactive.Bindings.Extensions;
using Uno.Extensions;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FolderListupPage : Page
    {
        public FolderListupPage()
        {
            this.InitializeComponent();

            Loaded += FolderListupPage_Loaded;
            _ViewPortChangeThrottling = new BehaviorSubject<Unit>(Unit.Default);
            FileItemsRepeater.BringIntoViewRequested += FileItemsRepeater_BringIntoViewRequested;
           
        }

        private void FileItemsRepeater_BringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs args)
        {
            Debug.WriteLine("FileItemsRepeater_BringIntoViewRequested");
        }

        private void FolderListupPage_Loaded(object sender, RoutedEventArgs e)
        {
            _PageVM = DataContext as ViewModels.FolderListupPageViewModel;

            var dispatcher = Dispatcher;
            _ViewPortChangeThrottling.Throttle(TimeSpan.FromMilliseconds(100))
               .Subscribe(async _ =>
               {
                   if (_PageVM.FileItems.Count == 0) { return; }

                   _nowThrottling = false; Debug.WriteLine("Throttling disable.");

                    await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                   {
                       _PageVM.FileItems.Take(10).ForEach(x => x.Initialize());
                    });
               });
            _nowThrottling = true;
            _PageVM.FileItemsView.ObserveProperty(x => x.Count)
                .Subscribe(_ =>
                {
                    _nowThrottling = true;
                });
        }

        ViewModels.FolderListupPageViewModel _PageVM;
        

        System.Reactive.Subjects.ISubject<System.Reactive.Unit> _ViewPortChangeThrottling;
        bool _nowThrottling = true;

        private void FileItem_EffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            if (_nowThrottling)
            {
                _ViewPortChangeThrottling.OnNext(Unit.Default);
                return;
            }

            if (_PageVM.NowProcessing) { return; }

            if (args.BringIntoViewDistanceY < (sender.ActualHeight + 80))
            {
                var itemVM = sender.DataContext as ViewModels.PageNavigation.StorageItemViewModel;
                itemVM.Initialize();
//                Debug.WriteLine($"Item: {sender.Tag} has {sender.ActualHeight - args.BringIntoViewDistanceY} pixels within the viewport");
            }
            else
            {
                var itemVM = sender.DataContext as ViewModels.PageNavigation.StorageItemViewModel;
                itemVM.StopImageLoading();
//                Debug.WriteLine($"Item: {sender.Tag} has {args.BringIntoViewDistanceY - sender.ActualHeight} pixels to go before it is even partially visible");
            }
        }

        private void Image_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var item = sender as FrameworkElement;
            item.Scale(1.020f, 1.020f, centerX: (float)item.ActualWidth * 0.5f, centerY: (float)item.ActualHeight * 0.5f, duration: 50)
                .Start();
        }

        private void Image_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var item = sender as FrameworkElement;
            item.Scale(1.0f, 1.0f, centerX: (float)item.ActualWidth * 0.5f, centerY: (float)item.ActualHeight * 0.5f, duration: 50)
                .Start();
        }
    }
}
