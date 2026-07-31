using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using I18NPortable;
using System;
using System.IO;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Contracts.Services;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Dialogs;
using Windows.Storage;
using Windows.UI.Xaml.Controls;


#nullable enable
namespace TsubameViewer.ViewModels.SourceFolders.Commands;

public sealed class ThumbnailImageUpdateRequestMessage : ValueChangedMessage<string>
{
    public ThumbnailImageUpdateRequestMessage(string value) : base(value)
    {
    }
}

public sealed class ChangeStorageItemThumbnailImageCommand : CommandBase
{
    readonly IMessenger _messenger;
    private readonly IMessageDialogService _dialogService;
    readonly ThumbnailImageManager _thumbnailManager;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    
    public bool IsArchiveThumbnailSetToFile { get; set; }

    public ChangeStorageItemThumbnailImageCommand(
        IMessenger messenger,
        IMessageDialogService dialogService,
        ThumbnailImageManager thumbnailManager,
        SourceStorageItemsRepository sourceStorageItemsRepository
        ) 
    {
        _messenger = messenger;
        _dialogService = dialogService;
        _thumbnailManager = thumbnailManager;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
    }

    public override bool CanExecute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;
        }
        
        return parameter is IImageSource imageSource
            && imageSource is StorageItemImageSource;
    }

    public override async void Execute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;
        }

        if (parameter is IImageSource imageSource)
        {
            try
            {
                var folderStorageItem = await _sourceStorageItemsRepository.TryGetStorageItemFromPath(Path.GetDirectoryName(imageSource.Path));
                if (folderStorageItem is not StorageFolder folder) { throw new InvalidOperationException(); }

                StorageFile? existFile = null;
                try
                {
                    existFile = await folder.GetFileAsync(ThumbnailImageManager.DefaultCoverImageFileName);
                }
                catch (FileNotFoundException) { }
                if (existFile != null)
                {
                    if (await _dialogService.ShowMessageDialogAsync(
                        "SetToParentFolderThumbnailImage".Translate(),
                        "Overwrite".Translate(),
                        "Cancel".Translate(),
                        title:"SetThumbnailImage".Translate()) is false) { return; }

                    try
                    {
                        await existFile.DeleteAsync(StorageDeleteOption.Default);
                    }
                    catch { }
                }

                await _thumbnailManager.PrepareToParentFolderThumbnailImageAsync(imageSource);
                _messenger.SendShowTextNotificationMessage("ThumbnailImageChanged".Translate());
                _messenger.Send(new ThumbnailImageUpdateRequestMessage(Path.GetDirectoryName(imageSource.Path)));
            }
            catch
            {
                //_messenger.SendShowTextNotificationMessage("ThumbnailImageChanged".Translate());
                throw;
            }
        }
    }
}
