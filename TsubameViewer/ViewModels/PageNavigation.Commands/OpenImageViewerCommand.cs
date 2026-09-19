using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Services;
using TsubameViewer.Services.Navigation;
using TsubameViewer.Views;
using Windows.UI.Xaml.Media.Animation;
#nullable enable
namespace TsubameViewer.ViewModels.PageNavigation.Commands;

public sealed class OpenImageViewerCommand : CommandBase
{
    readonly IMessenger _messenger;
    private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    private readonly SecondaryWindowService _secondaryWindowService;
    private readonly ViewerSettings _viewerSettings;

    public OpenImageViewerCommand(
        IMessenger messenger,
        DisplaySettingsByPathRepository displaySettingsByPathRepository,
        SecondaryWindowService secondaryWindowService,
        ViewerSettings viewerSettings
        )
    {
        _messenger = messenger;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        _secondaryWindowService = secondaryWindowService;
        _viewerSettings = viewerSettings;
    }

    public override bool CanExecute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;
        }

        return parameter is IImageSource;
    }

    public override async void Execute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;
        }

        if (parameter is IImageSource imageSource)
        {
            await imageSource.ThrowIfImageSourceStorageItemNotFound(_messenger);

            // ファイル・フォルダの差分検出処理を止める
            try
            {
                _messenger.Send<PreNavigationNotifyMessage>();
            }
            catch { }

            if (_viewerSettings.IsViewerOpenWithSecondaryWindow)
            {
                try
                {
                    await _secondaryWindowService.OpenViewerAsync(imageSource, false);
                }
                catch
                {
                    _messenger.SendShowTextNotificationMessage("OpenImageViewer_Failed".Translate());                    
                }
            }
            else
            {
                INavigationResult? result = null;
                var type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(imageSource);
                if (type is StorageItemTypes.Image
                    or Core.Models.StorageItemTypes.EBook
                    or StorageItemTypes.Archive
                    or StorageItemTypes.Folder
                    or StorageItemTypes.Albam
                    or StorageItemTypes.AlbamImage)
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    result = await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
                }
                else if (type == StorageItemTypes.EBook)
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    result = await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
                }
                else if (type == StorageItemTypes.Movie)
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    result = await _messenger.NavigateAsync(nameof(MovieViewerPage), parameters);
                }

                if (result?.IsSuccess is false)
                {
                    _messenger.SendShowTextNotificationMessage("OpenImageViewer_Failed".Translate());
                }
            }
        }
    }
}
