using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TsubameViewer.Presentation.ViewModels;
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
    public sealed partial class AlbamListupPage : Page
    {
        public AlbamListupPage()
        {
            this.InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var oldViewModel = _vm;
            _vm = args.NewValue as AlbamListupPageViewModel;
            if (_vm != null && oldViewModel != _vm)
            {
                this.Bindings.Update();
            }
        }

        private AlbamListupPageViewModel _vm;
    }


    public sealed class AlbamDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate CreateNew { get; set; }
        public DataTemplate Albam { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return this.SelectTemplateCore(item, null);
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is CreateNewAlbamViewModel)
            {
                return CreateNew;
            }
            else if (item is ViewModels.Albam.AlbamViewModel)
            {
                return Albam;
            }

            return base.SelectTemplateCore(item, container);
        }
    }

    public sealed class AlbamStyleSelector : Windows.UI.Xaml.Controls.StyleSelector
    {
        public Style CreateNew { get; set; }
        public Style Albam { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            if (item is CreateNewAlbamViewModel)
            {
                return CreateNew;
            }
            else if (item is ViewModels.Albam.AlbamViewModel)
            {
                return Albam;
            }

            return base.SelectStyleCore(item, container);
        }
    }


}
