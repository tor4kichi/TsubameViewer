using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using I18NPortable;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Contracts.Services;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Helpers;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using static TsubameViewer.Core.Models.SourceFolders.SourceStorageItemsRepository;

namespace TsubameViewer.ViewModels.SourceFolders.Commands;

internal class SendToOtherFolderCommand : CommandBase
{
    private readonly TokenToPathEntry _entry;
    private readonly SourceStorageItemsRepository _sourceRepo;
    private readonly IMessenger _messenger;

    public SendToOtherFolderCommand(
        TokenToPathEntry entry, 
        SourceStorageItemsRepository sourceRepo, 
        IMessenger messenger)
    {
        _entry = entry;
        _sourceRepo = sourceRepo;
        _messenger = messenger;
    }
    public override bool CanExecute(object parameter)
    {
        // アーカイブ内部のフォルダ、ファイル以外は受付可能
        return parameter is IStorageItemViewModel itemVM
            && itemVM.Type is not Core.Models.StorageItemTypes.ArchiveFolder;
    }

    public override async void Execute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            if (itemVM.Type is Core.Models.StorageItemTypes.ArchiveFolder) { return; }

            if (itemVM.Item.StorageItem is StorageFile file)
            {
                var toFolder = (StorageFolder)await _sourceRepo.GetSourceStorageItemAsync(_entry);
                try
                {
                    await file.MoveAsync(toFolder, file.Name, NameCollisionOption.FailIfExists);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    // ダイアログで上書き確認
                    if (Ioc.Default.GetService<IMessageDialogService>() is { } dialogService)
                    {
                        if (!await dialogService.ShowMessageDialogAsync(
                            "SendToOtherFolder_ConfirmOverwrite".Translate(),
                            "Overwrite".Translate(),
                            "Cancel".Translate()
                            )) { return; }

                        await file.MoveAsync(toFolder, file.Name, NameCollisionOption.ReplaceExisting);
                    }
                }
                Debug.WriteLine($"SendToOtherFolder {file.Name} move to {toFolder.Name}");
                _messenger.Send(new SendToOtherFolderMessage(_entry, itemVM.Path));
                _messenger.SendShowTextNotificationMessage("SendToOtherFolder_MoveItem0to1".Translate(file.Name, toFolder.Name));
            }
            else if (itemVM.Item.StorageItem is StorageFolder folder)
            {
                var toFolder = (StorageFolder)await _sourceRepo.GetSourceStorageItemAsync(_entry);
                try
                {                    
                    await folder.MoveAsync(toFolder, CreationCollisionOption.OpenIfExists, NameCollisionOption.FailIfExists);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    // ダイアログで上書き確認
                    if (Ioc.Default.GetService<IMessageDialogService>() is { } dialogService)
                    {
                        if (!await dialogService.ShowMessageDialogAsync(
                            "SendToOtherFolder_ConfirmOverwrite".Translate(),
                            "Overwrite".Translate(),
                            "Cancel".Translate()
                            )) { return; }

                        await folder.MoveAsync(toFolder, CreationCollisionOption.OpenIfExists, NameCollisionOption.ReplaceExisting);
                    }
                }
                Debug.WriteLine($"SendToOtherFolder {folder.Name} move to {toFolder.Name}");
                _messenger.Send(new SendToOtherFolderMessage(_entry, itemVM.Path));
                _messenger.SendShowTextNotificationMessage("SendToOtherFolder_MoveItem0to1".Translate(folder.Name, toFolder.Name));
            }
        }
    }
}

public sealed record SendToOtherFolderMessageData(TokenToPathEntry DestFolderEntry, string SourceItemPath);
public sealed class SendToOtherFolderMessage : ValueChangedMessage<SendToOtherFolderMessageData>
{
    public SendToOtherFolderMessage(TokenToPathEntry destFolderEntry, string sourceItemPath) : base(new(destFolderEntry, sourceItemPath))
    {
    }

    public SendToOtherFolderMessage(SendToOtherFolderMessageData data) : base(data)
    {
    }
}
