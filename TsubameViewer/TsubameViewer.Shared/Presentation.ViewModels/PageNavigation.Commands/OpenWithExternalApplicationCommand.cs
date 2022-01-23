using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using Windows.Storage;
using Windows.System;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
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
                if (type is Models.Domain.StorageItemTypes.Image
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
