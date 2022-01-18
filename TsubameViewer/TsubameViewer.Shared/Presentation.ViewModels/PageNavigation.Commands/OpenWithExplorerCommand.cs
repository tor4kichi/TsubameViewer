using Prism.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using Windows.Storage;
using Windows.System;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class OpenWithExplorerCommand : DelegateCommandBase
    {
        protected override bool CanExecute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            return parameter is IImageSource;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            if (parameter is IImageSource imageSource)
            {
                if (imageSource is StorageItemImageSource)
                {
                    if (imageSource.StorageItem is StorageFolder folder)
                    {
                        await Launcher.LaunchFolderAsync(folder);
                    }
                    else if (imageSource.StorageItem is StorageFile file)
                    {
                        await Launcher.LaunchFolderPathAsync(Path.GetDirectoryName(file.Path), new FolderLauncherOptions() { ItemsToSelect = { file } });
//                        await Launcher.LaunchFolderAsync(await file.GetParentAsync(), new FolderLauncherOptions() { ItemsToSelect = { file } });
                    }
                }
                else if (imageSource is AlbamItemImageSource albamItemImageSource)
                {
                    await Launcher.LaunchFolderPathAsync(Path.GetDirectoryName(albamItemImageSource.Path), new FolderLauncherOptions() { ItemsToSelect = { albamItemImageSource.StorageItem } });
                }
            }
        }
    }
}
