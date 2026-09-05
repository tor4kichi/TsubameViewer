using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Input;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Services;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views;
using VersOne.Epub;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;
using StorageItemTypes = TsubameViewer.Core.Models.StorageItemTypes;
#nullable enable
namespace TsubameViewer.ViewModels.PageNavigation.Commands;

public sealed class OpenFolderItemCommand : CommandBase
{
    readonly IMessenger _messenger;
    readonly FolderContainerTypeManager _folderContainerTypeManager;
    readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    readonly SourceChoiceCommand _sourceChoiceCommand;
    readonly AlbamCreateCommand _albamCreateCommand;
    private readonly SecondaryWindowService _secondaryWindowService;
    private readonly ViewerSettings _viewerSettings;

    public OpenFolderItemCommand(
        IMessenger messenger,
        FolderContainerTypeManager folderContainerTypeManager,
        DisplaySettingsByPathRepository displaySettingsByPathRepository,
        SourceChoiceCommand sourceChoiceCommand,
        AlbamCreateCommand albamCreateCommand,
        SecondaryWindowService secondaryWindowService,
        ViewerSettings viewerSettings
        )
    {
        _messenger = messenger;
        _folderContainerTypeManager = folderContainerTypeManager;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        _sourceChoiceCommand = sourceChoiceCommand;
        _albamCreateCommand = albamCreateCommand;
        _secondaryWindowService = secondaryWindowService;
        _viewerSettings = viewerSettings;
    }

    public override bool CanExecute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;

            if (itemVM.Type == StorageItemTypes.AddFolder)
            {
                return true;
            }
            else if (itemVM.Type == StorageItemTypes.AddAlbam)
            {
                return true;
            }
        }

        return parameter is IImageSource;
    }

    public override async void Execute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;

            if (itemVM.Type == StorageItemTypes.AddFolder)
            {
                ((ICommand)_sourceChoiceCommand).Execute(null);
                return;
            }
            else if (itemVM.Type == StorageItemTypes.AddAlbam)
            {
                ((ICommand)_albamCreateCommand).Execute(null);
                return;
            }
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

            INavigationResult? viewerResult = null;
            var type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(imageSource);
            if (type is StorageItemTypes.Image or StorageItemTypes.AlbamImage
                or StorageItemTypes.Archive or StorageItemTypes.ArchiveFolder)
            {
                if (_viewerSettings.IsViewerOpenWithSecondaryWindow)
                {
                    await _secondaryWindowService.OpenViewerAsync(imageSource, false);
                }
                else
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    viewerResult = await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
                }
            }
            else if (type is StorageItemTypes.Albam)
            {
                var albamImageSource = imageSource as AlbamImageSource;
                if (await albamImageSource.IsExistFolderOrArchiveFileAsync())
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                }
                else
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    viewerResult = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                }
            }
            else if (type == StorageItemTypes.Folder)
            {
                var folder = (StorageFolder)((StorageItemImageSource)imageSource.FlattenAlbamItemInnerImageSource()).StorageItem;
                var parentSettings = _displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(folder.Path);
                var openMode = parentSettings?.ChildImagesFolderOpenMode ?? DisplaySettingsByPathRepository.DefaultChildImagesFolderOpenMode;
                if (openMode == DefaultFolderOrArchiveOpenMode.Viewer
                    && await _messenger.WorkWithBusyWallAsync(async ct => await _folderContainerTypeManager.IsAvairableImagesAsync(folder, ct), CancellationToken.None))
                {
                    if (_viewerSettings.IsViewerOpenWithSecondaryWindow)
                    {
                        await _secondaryWindowService.OpenViewerAsync(imageSource, false);
                    }
                    else
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                        viewerResult = await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
                    }
                }
                else
                {
                    var setting = _displaySettingsByPathRepository.GetFolderAndArchiveSettings(folder.Path);
                    DefaultFolderListupMode currentListupMode = DefaultFolderListupMode.Images;
                    if (setting?.ListupMode is { } listupMode)
                    {
                        currentListupMode = listupMode;
                    }
                    else if (parentSettings?.LastSelectedListupMode is { } lastListupMode)
                    {
                        // 兄弟フォルダで選択された状態を一旦優先してアイテムの確認を行う
                        // もしフォルダアイテムがあればフォルダ一覧を、無ければ画像一覧を暫定のデフォルト表示方法として設定し
                        // 次回以降は兄弟フォルダ設定ではなく各フォルダ設定から直接表示先が選択されるように
                        if (lastListupMode == DefaultFolderListupMode.FolderOrContents
                            && await _messenger.WorkWithBusyWallAsync(async ct => await _folderContainerTypeManager.IsAvairableFolderOrContentsAsync(folder, ct), CancellationToken.None))
                        {
                            currentListupMode = DefaultFolderListupMode.FolderOrContents;
                        }
                        else
                        {
                            currentListupMode = DefaultFolderListupMode.Images;
                        }
                    }
                    else if (await _messenger.WorkWithBusyWallAsync(async ct => await _folderContainerTypeManager.IsAvairableFolderOrContentsAsync(folder, ct), CancellationToken.None))
                    {
                        currentListupMode = DefaultFolderListupMode.FolderOrContents;
                        
                    }
                    else
                    {
                        currentListupMode = DefaultFolderListupMode.Images;
                    }

                    if (currentListupMode == DefaultFolderListupMode.FolderOrContents)
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                        await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                    }
                    else
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                        await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                    }

                    if (setting?.ListupMode == null)
                    {
                        _displaySettingsByPathRepository.SetFolderAndArchiveSettings(folder.Path, currentListupMode);
                    }
                }
            }
            else if (type == StorageItemTypes.EBook)
            {
                if (_viewerSettings.IsViewerOpenWithSecondaryWindow)
                {
                    await _secondaryWindowService.OpenViewerAsync(imageSource, false);
                }
                else
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    viewerResult = await _messenger.NavigateAsync(nameof(EBookViewerPage), parameters);
                }
            }
            else if (type == StorageItemTypes.Movie)
            {
                if (_viewerSettings.IsViewerOpenWithSecondaryWindow)
                {
                    await _secondaryWindowService.OpenViewerAsync(imageSource, false);
                }
                else
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    viewerResult = await _messenger.NavigateAsync(nameof(MovieViewerPage), parameters);
                }
            }
            else if (type == StorageItemTypes.AddFolder)
            {
                ((ICommand)_sourceChoiceCommand).Execute(null);
            }
            else if (type == StorageItemTypes.AddAlbam)
            {
                ((ICommand)_albamCreateCommand).Execute(null);
            }

            if (viewerResult?.IsSuccess == false)
            {
                _messenger.SendShowTextNotificationMessage("OpenImageViewer_Failed".Translate());
            }
        }
    }
}
