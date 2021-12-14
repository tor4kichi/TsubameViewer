using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using TsubameViewer.Presentation.Views;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    public static class PageNavigationConstants
    {
        public const string Path = "path";
        public const string PageName = "pageName";
        public const string ArchiveFolderName = "archiveFolderName";
        public const string Restored = "__restored";


        public readonly static Type HomePageType = typeof(SourceStorageItemsPage);
        public static string HomePageName => HomePageType.Name;

    }
}
