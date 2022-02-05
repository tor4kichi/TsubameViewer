using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using Windows.Storage;
using Windows.System;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class OpenWithExplorerCommand : ImageSourceCommandBase
    {
        protected override bool CanExecute(IImageSource imageSource)
        {
            return FlattenAlbamItemInnerImageSource(imageSource) is StorageItemImageSource;
        }

        protected override async void Execute(IImageSource imageSource)
        {
            imageSource = FlattenAlbamItemInnerImageSource(imageSource);
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
        }

        protected override bool CanExecute(IEnumerable<IImageSource> imageSources)
        {
            var sample = imageSources.First();
            var firstItemDirectoryName = Path.GetDirectoryName(sample.Path);
            return FlattenAlbamItemInnerImageSource(imageSources).All(x => x is StorageItemImageSource item && Path.GetDirectoryName(item.Path) == firstItemDirectoryName);
        }

        protected override async void Execute(IEnumerable<IImageSource> imageSources)
        {
            var flattenImageSources = FlattenAlbamItemInnerImageSource(imageSources);
            var sample = flattenImageSources.First();
            var firstItemDirectoryName = Path.GetDirectoryName(sample.Path);
            var options = new FolderLauncherOptions();
            foreach (var storageItem in flattenImageSources.Select(x => x.StorageItem))
            {
                options.ItemsToSelect.Add(storageItem);
            }

            await Launcher.LaunchFolderPathAsync(firstItemDirectoryName, options);
        }
    }
}
