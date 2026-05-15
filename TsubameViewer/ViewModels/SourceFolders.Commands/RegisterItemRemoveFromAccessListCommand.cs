using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Services;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.ViewModels.PageNavigation;

namespace TsubameViewer.ViewModels.SourceFolders.Commands;

internal class RegisterItemRemoveFromAccessListCommand : IRelayCommand<IStorageItemViewModel>
{
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly IMessageDialogService _messageDialogService;

    public RegisterItemRemoveFromAccessListCommand(
        SourceStorageItemsRepository sourceStorageItemsRepository,
        IMessageDialogService messageDialogService)
    {
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _messageDialogService = messageDialogService;
    }

    public event EventHandler CanExecuteChanged;

    public bool CanExecute(IStorageItemViewModel parameter)
    {
        return parameter?.IsSourceStorageItem ?? false;
    }

    public bool CanExecute(object parameter)
    {
        return CanExecute(parameter as IStorageItemViewModel);
    }

    public async void Execute(IStorageItemViewModel parameter)
    {
        if (!parameter.IsSourceStorageItem) { return; }
        var messenger = Ioc.Default.GetService<IMessenger>();
        if (!await _messageDialogService.ShowMessageDialogAsync(
            "ConfirmRemoveSourceFolderFromAppDescription".Translate(),
            "Delete".Translate(),
            "Cancel".Translate(),
            false,
            "ConfirmRemoveSourceFolderFromAppWithFolderName".Translate(parameter.Name)
            ))
        {
            return;
        }
        var (token, item) = await _sourceStorageItemsRepository.GetSourceStorageItem(parameter.Path);
        if (item.Path is { } path)
        {
            var deleteResult = await messenger.WorkWithBusyWallAsync(
                async (ct) => await messenger.Send(new SourceStorageItemIgnoringRequestMessage(path)), CancellationToken.None);
        }

        messenger.Send(new RemoveSourceStorageItemFromAppMessage(parameter));
    }

    public void Execute(object parameter)
    {
        Execute(parameter as IStorageItemViewModel);
    }

    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
