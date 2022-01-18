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
    public sealed class OpenWithExternalApplicationCommand : DelegateCommandBase
    {
        protected override bool CanExecute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            return parameter is IImageSource imageSource 
                && SupportedFileTypesHelper.StorageItemToStorageItemTypes(imageSource) is not Models.Domain.StorageItemTypes.Folder and not Models.Domain.StorageItemTypes.AddFolder and not Models.Domain.StorageItemTypes.AddAlbam
                ;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            if (parameter is IImageSource imageSource)
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
