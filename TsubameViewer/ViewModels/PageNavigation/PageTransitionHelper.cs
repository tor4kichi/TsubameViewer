using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Navigations;
using TsubameViewer.Views;
using Windows.UI.Xaml.Media.Animation;

namespace TsubameViewer.ViewModels.PageNavigation
{
    public static class PageTransitionHelper
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
                nameof(FolderListupPage) => _listupTransison,
                nameof(ImageListupPage) => _listupTransison,
                nameof(SearchResultPage) => _searchTransison,
                nameof(SettingsPage) => _settingsTransison,
                _ => _otherTransison,
            };
        }



        #region Navigation Parameters


        

        public static NavigationParameters CreatePageParameter(IImageSource imageSource)
        {
            var type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(imageSource);
            if (imageSource is ArchiveEntryImageSource archiveEntyrImageSource)
            {
                return CreatePageParameter(archiveEntyrImageSource);
            }
            else if (type == StorageItemTypes.Image)
            {
                return new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(PageNavigationConstants.MakeStorageItemIdWithPage(imageSource.StorageItem.Path, imageSource.Name))));
            }
            else if (imageSource is ArchiveDirectoryImageSource archiveFolderImageSource)
            {
                return CreatePageParameter(archiveFolderImageSource);
            }
            else if (imageSource is AlbamImageSource albam)
            {
                return CreatePageParameter(albam);
            }
            else if (imageSource is AlbamItemImageSource albamItem)
            {
                if (albamItem.GetAlbamItemType() == AlbamItemType.Image)
                {
                    return CreatePageParameter(albamItem);
                }
                else
                {
                    return CreatePageParameter(albamItem.InnerImageSource);
                }
            }
            else
            {
                return new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(imageSource.StorageItem.Path)));
            }
        }

        public static NavigationParameters CreatePageParameter(ArchiveDirectoryImageSource archiveFolderImageSource)
        {
            return new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(archiveFolderImageSource.Path)));
        }

        public static NavigationParameters CreatePageParameter(ArchiveEntryImageSource archiveEntryImageSource)
        {
            return new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(archiveEntryImageSource.Path)));
        }

        public static NavigationParameters CreatePageParameter(AlbamImageSource albam)
        {
            return new NavigationParameters((PageNavigationConstants.AlbamPathKey, Uri.EscapeDataString(albam.AlbamId.ToString())));
        }

        public static NavigationParameters CreatePageParameter(AlbamItemImageSource albamItem)
        {
            return new NavigationParameters((PageNavigationConstants.AlbamPathKey, Uri.EscapeDataString(PageNavigationConstants.MakeStorageItemIdWithPage(albamItem.AlbamId.ToString(), albamItem.Path))));
        }

        #endregion
    }
}
