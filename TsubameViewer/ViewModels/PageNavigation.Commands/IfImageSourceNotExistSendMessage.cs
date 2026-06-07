using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.ImageViewer;
using Windows.Storage;
#nullable enable
namespace TsubameViewer.ViewModels.PageNavigation.Commands;

internal static class IfImageSourceNotExistSendMessage
{
    public static async Task ThrowIfImageSourceStorageItemNotFound(this IImageSource imageSource, IMessenger? messenger = null)
    {
        messenger ??= Ioc.Default.GetRequiredService<IMessenger>();
        if (imageSource is AlbamImageSource albamImageSource) { return; }
        var type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(imageSource);
        if (type == Core.Models.StorageItemTypes.Folder)
        {
            try
            {
                await StorageFolder.GetFolderFromPathAsync(imageSource.StorageItem.Path);
            }
            catch
            {
                messenger.Send(new StorageItemNotFoundMessage(imageSource.Path));
                throw;
            }
        }
        else
        {
            try
            {
                await StorageFile.GetFileFromPathAsync(imageSource.StorageItem.Path);
            }
            catch
            {
                messenger.Send(new StorageItemNotFoundMessage(imageSource.Path));
                throw;
            }
        }


    }
}
