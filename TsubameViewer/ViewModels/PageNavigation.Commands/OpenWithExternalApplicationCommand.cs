using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using Windows.Storage;
using Windows.System;

namespace TsubameViewer.ViewModels.PageNavigation.Commands
{
    public sealed class OpenWithExternalApplicationCommand : ImageSourceCommandBase
    {
        protected override bool CanExecute(IImageSource imageSource)
        {
            return FlattenAlbamItemInnerImageSource(imageSource) is StorageItemImageSource;
        }

        protected override bool CanExecute(IEnumerable<IImageSource> imageSources)
        {
            return false;
        }

        protected override void Execute(IImageSource imageSource)
        {
            if (FlattenAlbamItemInnerImageSource(imageSource) is StorageItemImageSource storageItem)
            {
                var type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(imageSource);
                if (type is Core.Models.StorageItemTypes.Image
                    && imageSource is ArchiveEntryImageSource or PdfPageImageSource)
                {
                    return;
                }

                if (imageSource.StorageItem is StorageFile file)
                {
                    _ = Launcher.LaunchFileAsync(file, new LauncherOptions() { DisplayApplicationPicker = true });
                }
            }
        }
    }
}
