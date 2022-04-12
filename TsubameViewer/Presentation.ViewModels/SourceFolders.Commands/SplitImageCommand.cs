using I18NPortable;
using Microsoft.IO;
using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Messaging;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Models.UseCase.Transform;
using TsubameViewer.Presentation.Services;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

namespace TsubameViewer.Presentation.ViewModels.SourceFolders.Commands
{
    public sealed class SplitImageCommand : ImageSourceCommandBase
    {
        private readonly IMessenger _messenger;
        private readonly SplitImageTransform _splitImageTransform;
        private readonly ISplitImageInputDialogService _numberInputDialogService;
        private readonly ThumbnailManager _thumbnailManager;

        public SplitImageCommand(
            IMessenger messenger,
            SplitImageTransform splitImageTransform,
            ISplitImageInputDialogService numberInputDialogService,
            ThumbnailManager thumbnailManager
            )
        {
            _messenger = messenger;
            _splitImageTransform = splitImageTransform;
            _numberInputDialogService = numberInputDialogService;
            _thumbnailManager = thumbnailManager;
        }

        protected override bool CanExecute(IImageSource imageSource)
        {
            return SupportedFileTypesHelper.StorageItemToStorageItemTypesWithFlattenAlbamItem(imageSource) is StorageItemTypes.Archive or StorageItemTypes.Folder;
        }

        // AB判（一番横長のページ）を横二枚並べた時の縦横比
        const double TargetAspectRatio = 1.0; //0.611904;

        protected override async void Execute(IImageSource imageSource)
        {
            var flattenImageSource = FlattenAlbamItemInnerImageSource(imageSource);
            if (flattenImageSource is StorageItemImageSource s)
            {
                // 同じフォルダで縦横比が1.0以下の画像がないか、生成済みサムネイル情報から検索
                // その縦横比の情報をデフォルトの入力情報として渡す

                // 式計算可能な数値入力ダイアログを表示
                var (isConfirm, pageAspectRatio, bindingDirection, encoderId) =
                    await _numberInputDialogService.GetSplitImageInputAsync();

                if (isConfirm is false) { return; }

                Guard.IsInRange(pageAspectRatio.Value, 0.1, 2.0, nameof(pageAspectRatio));

                if (s.StorageItem is StorageFile file
                    && SupportedFileTypesHelper.IsSupportedMangaFile(file))
                {
                    // 出力先のファイルを選択させる
                    var filePicker = new FileSavePicker()
                    {
                        SuggestedFileName = Path.ChangeExtension(file.Name, "zip"),
                        FileTypeChoices =
                        {
                            { "zip", new List<string> { ".zip" } }
                        },
                        DefaultFileExtension = ".zip",
                    };

                    var outputFile = await filePicker.PickSaveFileAsync();
                    if (outputFile == null) { return; }

                    using (var inputFile = await file.OpenStreamForReadAsync())
                    using (var archive = ArchiveFactory.Open(inputFile))
                    {
                        await _messenger.WorkWithBusyWallAsync(async (ct) =>
                        {
                            await Task.Run(async () => 
                            {
                                var tempOutputFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(Path.GetRandomFileName(), CreationCollisionOption.GenerateUniqueName).AsTask(ct);
                                var outputArchive = await _splitImageTransform.SplitImageOutputToArchiveFileAsync(archive, TargetAspectRatio, pageAspectRatio: pageAspectRatio, isLeftBinding: bindingDirection is Views.Dialogs.BookBindingDirection.Left, encoderId,  ct);

                                using (var fileStream = await tempOutputFile.OpenStreamForWriteAsync())
                                {
                                    outputArchive.SaveTo(fileStream, new SharpCompress.Writers.WriterOptions(SharpCompress.Common.CompressionType.None));
                                }

                                await _thumbnailManager.DeleteThumbnailFromPathAsync(outputFile.Path);
                                await tempOutputFile.MoveAndReplaceAsync(outputFile).AsTask(ct);
                            }, ct);
                        }, CancellationToken.None);
                    }
                }
                else if (s.StorageItem is StorageFolder folder)
                {
                    // 出力先のフォルダを選択させる                    
                    var folderPicker = new FolderPicker()
                    {
                        
                    };

                    var outputFolder = await folderPicker.PickSingleFolderAsync();
                    if (outputFolder == null) { return; }

                    await _messenger.WorkWithBusyWallAsync(async (ct) =>
                    {
                        await Task.Run(async () =>
                        {
                            await _thumbnailManager.DeleteThumbnailFromPathAsync(outputFolder.Path);
                            await _splitImageTransform.SplitImageOutputToFolderAsync(folder, outputFolder, TargetAspectRatio, pageAspectRatio: pageAspectRatio, isLeftBinding: bindingDirection is Views.Dialogs.BookBindingDirection.Left, encoderId, ct);
                        }, ct);
                    }, CancellationToken.None);
                }
            }
        }


        
    }
}
