using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;
using Prism.Ioc;
using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;
using System.Threading;
using Uno.Disposables;
using System.Linq;
using System.IO;
using Microsoft.Toolkit.Mvvm.Messaging;
using System.Threading.Tasks;
using TsubameViewer.Presentation.Views;
using TsubameViewer.Models.Domain.Albam;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class OpenListupCommand : DelegateCommandBase
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
                    var imageCollectionManager = App.Current.Container.Resolve<ImageCollectionManager>();
                    CancellationToken ct = CancellationToken.None;
                    var collectionContext = await _messenger.WorkWithBusyWallAsync(ct => imageCollectionManager.GetArchiveImageCollectionContextAsync((imageSource as StorageItemImageSource).StorageItem as StorageFile, null, ct), ct);
                    try
                    {
                        if (await collectionContext.IsExistImageFileAsync(ct))
                        {
                            var parameters = StorageItemViewModel.CreatePageParameter(imageSource);
                            var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                        }
                        else
                        {
                            var leaves = await collectionContext.GetLeafFoldersAsync(ct).ToListAsync(ct);                      
                            if (leaves.Count == 0)
                            {
                                var parameters = StorageItemViewModel.CreatePageParameter(imageSource);
                                var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                            }
                            else if (leaves.Count == 1)
                            {
                                var leaf = leaves[0] as ArchiveDirectoryImageSource;
                                var parameters = new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(PageNavigationConstants.MakeStorageItemIdWithArchiveFolder(imageSource.Path, leaf.Path))));
                                var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                            }
                            else
                            {
                                // 圧縮フォルダにスキップ可能なルートフォルダを含んでいる場合
                                var distinct = leaves.Select(x => new string(x.Path.TakeWhile(c => c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar).ToArray())).Distinct().ToList();
                                if (distinct.Count == 1)
                                {
                                    var parameters = new NavigationParameters((PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(PageNavigationConstants.MakeStorageItemIdWithArchiveFolder(imageSource.Path, distinct[0]))));
                                    var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                                }
                                else
                                {
                                    var parameters = StorageItemViewModel.CreatePageParameter(imageSource);
                                    var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                                }
                            }
                        }
                    }
                    finally
                    {
                        collectionContext.TryDispose();
                    }
                }
                else if (type == StorageItemTypes.Folder)
                {
                    var containerType = await _messenger.WorkWithBusyWallAsync(async ct => await _folderContainerTypeManager.GetFolderContainerTypeWithCacheAsync((imageSource as StorageItemImageSource).StorageItem as StorageFolder, ct), CancellationToken.None);
                    if (containerType == FolderContainerType.Other)
                    {
                        var parameters = StorageItemViewModel.CreatePageParameter(imageSource);
                        var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                    }
                    else
                    {
                        var parameters = StorageItemViewModel.CreatePageParameter(imageSource);
                        var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                    }
                }
                else if (type == StorageItemTypes.ArchiveFolder)
                {
                    if (imageSource is ArchiveDirectoryImageSource archiveFolderItem)
                    {
                        if (archiveFolderItem.IsContainsSubDirectory())
                        {
                            var parameters = StorageItemViewModel.CreatePageParameter(archiveFolderItem);
                            var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                        }
                        else
                        {
                            var parameters = StorageItemViewModel.CreatePageParameter(archiveFolderItem);
                            var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                        }
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }                    
                }
                else if (imageSource is AlbamImageSource albam)
                {
                    var parameters = StorageItemViewModel.CreatePageParameter(albam);
                    var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                }
                else if (imageSource is AlbamItemImageSource albamItem)
                {
                    var parameters = StorageItemViewModel.CreatePageParameter(albamItem);
                    var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                }
            }
        }
    }
}
