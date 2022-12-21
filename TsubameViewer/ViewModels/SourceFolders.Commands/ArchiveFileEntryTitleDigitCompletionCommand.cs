using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.UseCases.Transform;
using TsubameViewer.ViewModels.PageNavigation;
using Windows.Storage;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Services;

namespace TsubameViewer.ViewModels.SourceFolders.Commands
{
    public sealed class ArchiveFileEntryTitleDigitCompletionCommand : ImageSourceCommandBase
    {
        private readonly IMessenger _messenger;
        private readonly ArchiveFileInnerStructureCache _archiveFileInnerStructureCache;
        private readonly AlbamRepository _albamRepository;

        public ArchiveFileEntryTitleDigitCompletionCommand(
            IMessenger messenger,
            ArchiveFileInnerStructureCache archiveFileInnerStructureCache,
            AlbamRepository albamRepository
            )
        {
            _messenger = messenger;
            _archiveFileInnerStructureCache = archiveFileInnerStructureCache;
            _albamRepository = albamRepository;
        }

        protected override bool CanExecute(IImageSource imageSource)
        {
            return imageSource is StorageItemImageSource storageIS
                && storageIS.ItemTypes is Core.Models.StorageItemTypes.Archive or Core.Models.StorageItemTypes.Folder
                ;
        }

        protected override async void Execute(IImageSource imageSource)
        {
            if (imageSource is StorageItemImageSource storageIS)
            {
                if (storageIS.StorageItem is StorageFile archiveFile)
                {
                    void NoticeName(string oldName, string newName)
                    {
                        var oldPath = PageNavigationConstants.MakeStorageItemIdWithPage(archiveFile.Path, oldName);
                        var newPath = PageNavigationConstants.MakeStorageItemIdWithPage(archiveFile.Path, newName);
                        _albamRepository.PathChanged(oldPath, newPath);
                    }

                    var result = await _messenger.WorkWithBusyWallAsync(async ct => await TitleDigitCompletionTransform.TransformArchiveFileAsync(archiveFile, '0', SharpCompress.Common.CompressionType.None, (e) => NoticeName(e.Old, e.New), ct), System.Threading.CancellationToken.None);
                    _archiveFileInnerStructureCache.Delete(storageIS.Path);
                }
                else if (storageIS.StorageItem is StorageFolder folder)
                {
                    void NoticeName(string oldName, string newName)
                    {
                        _albamRepository.PathChanged(oldName, newName);
                    }

                    var result = await _messenger.WorkWithBusyWallAsync(async ct => await TitleDigitCompletionTransform.TransformFolderFilesAsync(folder, '0', (e) => NoticeName(e.Old, e.New), ct), System.Threading.CancellationToken.None);
                }

                // TODO: ブックマークやアルバムへの登録がある場合に新しいKey/Nameへの更新が必要
            }
        }        
    }
}
