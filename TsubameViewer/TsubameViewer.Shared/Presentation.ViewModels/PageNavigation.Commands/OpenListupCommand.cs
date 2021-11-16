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

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class OpenListupCommand : DelegateCommandBase
    {
        private INavigationService _navigationService;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;

        public OpenListupCommand(
            INavigationService navigationService,
            FolderContainerTypeManager folderContainerTypeManager
            )
        {
            _navigationService = navigationService;
            _folderContainerTypeManager = folderContainerTypeManager;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel item)
            {
                if (item.Type == StorageItemTypes.Archive)
                {
                    var imageCollectionManager = App.Current.Container.Resolve<ImageCollectionManager>();
                    CancellationToken ct = default;
                    var collectionContext = await imageCollectionManager.GetArchiveImageCollectionContextAsync((item.Item as StorageItemImageSource).StorageItem as StorageFile, null, ct);
                    try
                    {
                        if (await collectionContext.IsExistImageFileAsync(ct))
                        {
                            var parameters = StorageItemViewModel.CreatePageParameter(item);
                            var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageListupPage), parameters, new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            var leaves = await collectionContext.GetLeafFoldersAsync(ct);                      
                            if (leaves.Count == 0)
                            {
                                var parameters = StorageItemViewModel.CreatePageParameter(item);
                                var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageListupPage), parameters, new DrillInNavigationTransitionInfo());
                            }
                            else if (leaves.Count == 1)
                            {
                                var leaf = leaves[0] as ArchiveDirectoryImageSource;
                                var parameters = StorageItemViewModel.CreateArchiveFolderPageParameter(Uri.EscapeDataString(item.Path), Uri.EscapeDataString(leaf.Path));
                                var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageListupPage), parameters, new DrillInNavigationTransitionInfo());
                            }
                            else
                            {
                                // 圧縮フォルダにスキップ可能なルートフォルダを含んでいる場合
                                var distinct = leaves.Select(x => new string(x.Path.TakeWhile(c => c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar).ToArray())).Distinct().ToList();
                                if (distinct.Count == 1)
                                {
                                    var parameters = StorageItemViewModel.CreateArchiveFolderPageParameter(Uri.EscapeDataString(item.Path), Uri.EscapeDataString(distinct[0]));
                                    var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.FolderListupPage), parameters, new DrillInNavigationTransitionInfo());
                                }
                                else
                                {
                                    var parameters = StorageItemViewModel.CreatePageParameter(item);
                                    var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.FolderListupPage), parameters, new DrillInNavigationTransitionInfo());
                                }
                            }
                        }
                    }
                    finally
                    {
                        collectionContext.TryDispose();
                    }
                }
                else if (item.Type == StorageItemTypes.Folder)
                {
                    var containerType = await _folderContainerTypeManager.GetFolderContainerTypeWithCacheAsync((item.Item as StorageItemImageSource).StorageItem as StorageFolder);
                    if (containerType == FolderContainerType.Other)
                    {
                        var parameters = StorageItemViewModel.CreatePageParameter(item);
                        var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.FolderListupPage), parameters, new DrillInNavigationTransitionInfo());
                    }
                    else
                    {
                        var parameters = StorageItemViewModel.CreatePageParameter(item);
                        var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageListupPage), parameters, new SuppressNavigationTransitionInfo());
                    }
                }
                else if (item.Type == StorageItemTypes.ArchiveFolder)
                {
                    if (item.Item is ArchiveDirectoryImageSource archiveFolderItem)
                    {
                        if (archiveFolderItem.IsContainsSubDirectory())
                        {
                            var parameters = StorageItemViewModel.CreatePageParameter(item);
                            var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.FolderListupPage), parameters, new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            var parameters = StorageItemViewModel.CreatePageParameter(item);
                            var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageListupPage), parameters, new DrillInNavigationTransitionInfo());
                        }
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }                    
                }
                else if (item.Type == StorageItemTypes.EBook)
                {

                }
                else if (item.Type == StorageItemTypes.None)
                {
                }
            }
        }
    }
}
