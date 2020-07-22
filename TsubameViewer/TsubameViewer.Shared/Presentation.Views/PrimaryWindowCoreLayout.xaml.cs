using Prism.Commands;
using Prism.Navigation;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PrimaryWindowCoreLayout : Page
    {
        public PrimaryWindowCoreLayout(PrimaryWindowCoreLayoutViewModel viewModel)
        {
            this.InitializeComponent();

            ContentFrame.Navigated += Frame_Navigated;
            DataContext = _viewModel = viewModel;
        }

        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {
            IsDisplayMenu = e.SourcePageType != typeof(ImageCollectionViewerPage);
        }



        public bool IsDisplayMenu
        {
            get { return (bool)GetValue(IsDisplayMenuProperty); }
            set { SetValue(IsDisplayMenuProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsDisplayMenu.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsDisplayMenuProperty =
            DependencyProperty.Register("IsDisplayMenu", typeof(bool), typeof(PrimaryWindowCoreLayout), new PropertyMetadata(true));
        private readonly PrimaryWindowCoreLayoutViewModel _viewModel;
        IPlatformNavigationService _navigationService;
        public IPlatformNavigationService GetNavigationService()
        {
            return _navigationService ??= NavigationService.Create(this.ContentFrame, Gestures.Back, Gestures.Refresh);
        }


        private DelegateCommand _BackCommand;
        public DelegateCommand BackCommand =>
            _BackCommand ??= new DelegateCommand(() => _ = _navigationService?.GoBackAsync());

    }
}
