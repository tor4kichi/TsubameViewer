using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using Windows.Storage;
using Windows.System;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class OpenWithExternalApplicationCommand : DelegateCommandBase
    {
        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel item 
                && item.Type is not Models.Domain.StorageItemTypes.Folder and not Models.Domain.StorageItemTypes.AddFolder and not Models.Domain.StorageItemTypes.AddAlbam
                ;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                if (itemVM.Type is Models.Domain.StorageItemTypes.Image
                    && itemVM.Item is ArchiveEntryImageSource or PdfPageImageSource)
                {
                    return;
                }

                if (itemVM.Item.StorageItem is StorageFile file)
                {
                    _ = Launcher.LaunchFileAsync(file, new LauncherOptions() { DisplayApplicationPicker = true });
                }
            }
        }
    }
}
