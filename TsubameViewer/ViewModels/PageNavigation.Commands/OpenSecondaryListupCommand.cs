using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using DryIoc;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;
using StorageItemTypes = TsubameViewer.Core.Models.StorageItemTypes;
#nullable enable
namespace TsubameViewer.ViewModels.PageNavigation.Commands;

public sealed class OpenSecondaryListupCommand : CommandBase
{
    readonly IMessenger _messenger;
    readonly FolderContainerTypeManager _folderContainerTypeManager;
    private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    private readonly SourceChoiceCommand _sourceChoiceCommand;
    private readonly AlbamCreateCommand _albamCreateCommand;

    public OpenSecondaryListupCommand(
        IMessenger messenger,
        FolderContainerTypeManager folderContainerTypeManager,
        DisplaySettingsByPathRepository displaySettingsByPathRepository,
        SourceChoiceCommand sourceChoiceCommand,
        AlbamCreateCommand albamCreateCommand
        )
    {
        _messenger = messenger;
        _folderContainerTypeManager = folderContainerTypeManager;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        _sourceChoiceCommand = sourceChoiceCommand;
        _albamCreateCommand = albamCreateCommand;
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
        var listUpAnimationFactory = () => new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight };
        var imageViewerAnimationFactory = () => new DrillInNavigationTransitionInfo();

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

            var type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(imageSource);
            if (type == StorageItemTypes.Archive)
            {
                var imageCollectionManager = Ioc.Default.GetService<ImageCollectionManager>();
                CancellationToken ct = CancellationToken.None;
                var collectionContext = await _messenger.WorkWithBusyWallAsync(ct => imageCollectionManager.GetArchiveImageCollectionContextAsync((imageSource.FlattenAlbamItemInnerImageSource() as StorageItemImageSource).StorageItem as StorageFile, null, ct), ct);
                try
                {
                    if (await collectionContext.IsExistImageFileAsync(ct))
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                        var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                    }
                    else
                    {
                        var leaves = await collectionContext.GetLeafFoldersAsync(ct).ToListAsync(ct);                      
                        if (leaves.Count == 0)
                        {
                            var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                            var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                        }
                        else if (leaves.Count == 1)
                        {
                            var leaf = leaves[0] as ArchiveDirectoryImageSource;
                            var parameters = new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(imageSource.Path)));
                            var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                        }
                        else
                        {
                            // 圧縮フォルダにスキップ可能なルートフォルダを含んでいる場合
                            var distinct = leaves.Cast<IArchiveEntryImageSource>().Select(x => new string(x.EntryKey.TakeWhile(c => c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar).ToArray())).Distinct().ToList();
                            if (distinct.Count == 1)
                            {
                                var parameters = new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(imageSource.Path)));
                                var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                            }
                            else
                            {
                                var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                                var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                            }
                        }
                    }
                }
                finally
                {
                    (collectionContext as IDisposable)?.Dispose();
                }
            }
            else if (type == StorageItemTypes.Folder)
            {
                var folder = (StorageFolder)((StorageItemImageSource)imageSource.FlattenAlbamItemInnerImageSource()).StorageItem;
                var parentSettings = _displaySettingsByPathRepository.GetFileParentSettingsUpStreamToRoot(folder.Path);
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
                    var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                }
                else
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                }

                if (setting?.ListupMode == null)
                {
                    _displaySettingsByPathRepository.SetFolderAndArchiveSettings(folder.Path, currentListupMode);
                }
            }
            else if (type == StorageItemTypes.ArchiveFolder)
            {
                if (imageSource is ArchiveDirectoryImageSource archiveFolderItem)
                {
                    if (archiveFolderItem.IsContainsSubDirectory())
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(archiveFolderItem);
                        var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                    }
                    else
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(archiveFolderItem);
                        var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }                    
            }
            else if (type == StorageItemTypes.EBook)
            {
                var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
            }
            else if (imageSource is AlbamImageSource albam)
            {
                var parameters = PageTransitionHelper.CreatePageParameter(albam);
                var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
            }
            else if (imageSource is AlbamItemImageSource albamItem)
            {
                var parameters = PageTransitionHelper.CreatePageParameter(albamItem);
                var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
            }
        }
    }
}
