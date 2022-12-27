using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Services.Navigation;
using TsubameViewer.Views;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;
using StorageItemTypes = TsubameViewer.Core.Models.StorageItemTypes;

namespace TsubameViewer.ViewModels.PageNavigation.Commands
{
    public sealed class OpenListupCommand : CommandBase
    {
        private readonly IMessenger _messenger;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;

        public OpenListupCommand(
            IMessenger messenger,
            FolderContainerTypeManager folderContainerTypeManager
            )
        {
            _messenger = messenger;
            _folderContainerTypeManager = folderContainerTypeManager;
        }

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
            var listUpAnimationFactory = () => new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight };
            var imageViewerAnimationFactory = () => new DrillInNavigationTransitionInfo();

            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            if (parameter is IImageSource imageSource)
            {
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
                    var containerType = await _messenger.WorkWithBusyWallAsync(async ct => await _folderContainerTypeManager.GetFolderContainerTypeWithCacheAsync((imageSource.FlattenAlbamItemInnerImageSource() as StorageItemImageSource).StorageItem as StorageFolder, ct), CancellationToken.None);
                    if (containerType == FolderContainerType.Other)
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                        var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                    }
                    else
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                        var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
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
                else if (imageSource is IAlbamImageSource albam)
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
}
