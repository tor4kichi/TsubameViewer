using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Presentation.Views;
using Windows.UI.Xaml.Media.Animation;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    public static class PageTransisionHelper
    {
        public const string ImageJumpConnectedAnimationName = "ImageJumpInAnimation";

        private readonly static DrillInNavigationTransitionInfo _viewerTransison = new DrillInNavigationTransitionInfo();
        private readonly static SlideNavigationTransitionInfo _listupTransison = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight };
        private readonly static SlideNavigationTransitionInfo _searchTransison = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromBottom };
        private readonly static SlideNavigationTransitionInfo _settingsTransison = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromBottom };
        private readonly static SuppressNavigationTransitionInfo _otherTransison = new SuppressNavigationTransitionInfo();
        
        public static NavigationTransitionInfo MakeNavigationTransitionInfoFromPageName(string pageName)
        {
            return pageName switch
            {
                nameof(ImageViewerPage) => _viewerTransison,
                nameof(EBookReaderPage) => _viewerTransison,
                nameof(FolderListupPage) => _listupTransison,
                nameof(ImageListupPage) => _listupTransison,
                nameof(SearchResultPage) => _searchTransison,
                nameof(SettingsPage) => _settingsTransison,
                _ => _otherTransison,
            };
        }
    }
}
