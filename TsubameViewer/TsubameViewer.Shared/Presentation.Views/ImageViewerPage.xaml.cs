using Microsoft.Toolkit.Uwp.UI.Animations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Presentation.ViewModels;
using Uno;
using Uno.Disposables;
using Uno.Threading;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class ImageViewerPage : Page
    {
        public ImageViewerPage()
        {
            this.InitializeComponent();

            Loaded += ImageViewerPage_Loaded;
        }

        private async void ImageViewerPage_Loaded(object sender, RoutedEventArgs e)
        {
            using (var cts = new CancellationTokenSource(5000))
            {
                Image.Opacity = 0.0;
                while (Image.Source == null)
                {
                    await Task.Delay(50, cts.Token);
                }

                Image.Fade(1.0f, 175f)
                    .Start();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            Window.Current.SetTitleBar(DraggableTitleBarArea_Desktop);
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Collapsed;

            var appView = ApplicationView.GetForCurrentView();
            appView.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appView.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x7f, 0xff, 0xff, 0xff);
            appView.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0x3f, 0xff, 0xff, 0xff);
            appView.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(0xaf, 0xff, 0xff, 0xff);

            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = false;
            Window.Current.SetTitleBar(null);
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Visible;

            var appView = ApplicationView.GetForCurrentView();
            appView.TitleBar.ButtonBackgroundColor = null;
            appView.TitleBar.ButtonHoverBackgroundColor = null;
            appView.TitleBar.ButtonInactiveBackgroundColor = null;
            appView.TitleBar.ButtonPressedBackgroundColor = null;

            base.OnNavigatingFrom(e);
        }
    }
}
