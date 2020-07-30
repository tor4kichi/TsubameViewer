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
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    }
}
