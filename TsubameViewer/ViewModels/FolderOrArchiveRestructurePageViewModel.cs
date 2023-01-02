using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using Microsoft.Toolkit.Uwp.UI;
using Reactive.Bindings;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Models.Transform;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.PageNavigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;

namespace TsubameViewer.ViewModels
{

    public enum SplitImageProcessKind
    {
        None,
        Cover,
        SplitTwo_LeftBinding,
        SplitTwo_RightBinding,
    }


    partial class FolderOrArchiveRestructurePageViewModel : NavigationAwareViewModelBase
    {
        private readonly IMessenger _messenger;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly SplitImageTransform _splitImageTransform;

        public AdvancedCollectionView Items { get; }

        private ObservableCollection<IPathRestructure> _RawItems = new ();

        [ObservableProperty]
        private IStorageItem _sourceStorageItem;

        public List<string> DirectoryPaths { get; } = new List<string> ();

        public FolderOrArchiveRestructurePageViewModel(
            IMessenger messenger,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            SplitImageTransform splitImageTransform
            )
        {
            _messenger = messenger;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _splitImageTransform = splitImageTransform;
            Items = new AdvancedCollectionView(_RawItems);
            //Items.Filter = x => string.IsNullOrEmpty(SearchText) ? true : (x as IPathRestructure).EditPath.Contains(SearchText);
            Items.SortDescriptions.Add(new SortDescription(nameof(IPathRestructure.SourcePath), SortDirection.Ascending));
        }


        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            SourceStorageItem = null;
            _RawItems.Clear();
            DirectoryPaths.Clear();
            IsUnavairableOverwrite = false;

            base.OnNavigatedFrom(parameters);
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            await base.OnNavigatedToAsync(parameters);
            
            if (parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string path))
            {
                var unescapedPath = Uri.UnescapeDataString(path);
                var item = await _sourceStorageItemsRepository.TryGetStorageItemFromPath(unescapedPath);

                SourceStorageItem = item;                
                if (item is StorageFile file 
                    && file.IsSupportedMangaFile()
                    )
                {                    
                    using var fileStream = await FileRandomAccessStream.OpenAsync(unescapedPath, FileAccessMode.Read);
                    using var archive = ArchiveFactory.Open(fileStream.AsStreamForRead());
                    var items = await _messenger.WorkWithBusyWallAsync((ct) => Task.Run(() => ToPathRestructureItems(archive).ToList()), CancellationToken.None);
                    foreach (var pathRestructure in items)
                    {
                        pathRestructure.EditPath = pathRestructure.EditPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                    }
                    using (Items.DeferRefresh())
                    {
                        foreach (var itemVM in items)
                        {
                            _RawItems.Add(itemVM);
                        }
                    }

                    IsUnavairableOverwrite = await file.GetParentAsync() == null;
                }
                else if (item is StorageFolder folder)
                {
                    var items = await _messenger.WorkWithBusyWallAsync((ct) => Task.Run(async () => await ToPathRestructureItemsAsync(folder).ToListAsync()), CancellationToken.None);
                    foreach (var pathRestructure in items)
                    {
                        pathRestructure.EditPath = pathRestructure.EditPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                    }
                    using (Items.DeferRefresh())
                    {
                        foreach (var itemVM in items)
                        {
                            _RawItems.Add(itemVM);
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException(unescapedPath);
                }
            }
        }

        private IEnumerable<IPathRestructure> ToPathRestructureItems(IArchive archive)
        {
            foreach (var entry in archive.Entries)
            {
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(entry.Key))
                {
                    yield return new PathRestructure_ArchiveEntry(entry.Key) { IsOutput = entry.IsDirectory is false };
                }
            }
        }

        private async IAsyncEnumerable<IPathRestructure> ToPathRestructureItemsAsync(StorageFolder folder)
        {
            var query = folder.CreateFileQueryWithOptions(new Windows.Storage.Search.QueryOptions(Windows.Storage.Search.CommonFileQuery.OrderByName, fileTypeFilter: SupportedFileTypesHelper.SupportedImageFileExtensions) { FolderDepth = Windows.Storage.Search.FolderDepth.Deep });
            await foreach (var item in query.ToAsyncEnumerable())
            {
                var relativePath = item.Path.Substring(folder.Path.Length + 1);
                yield return new PathRestructure_StorageFile(relativePath, item);
            }
        }

        [ObservableProperty]
        private IPathRestructure _selectedItem;

        public List<IPathRestructure> SelectedItems { get; } = new List<IPathRestructure>();


        public SplitImageProcessKind[] SplitImageProcessKinds { get; } = new[] 
        {
            SplitImageProcessKind.None,
            SplitImageProcessKind.Cover,
            SplitImageProcessKind.SplitTwo_LeftBinding,
            SplitImageProcessKind.SplitTwo_RightBinding,
        };

        [ObservableProperty]
        private double _splitImageAspectRatio = 0.7;

        [ObservableProperty]
        private bool _isSplitWithLeftBinding = true;


        [ObservableProperty]
        private string _searchText;

        [ObservableProperty]
        private string _replaceText;

        [ObservableProperty]
        private bool _nowProcessOutput;

        [ObservableProperty]
        private string _outputErrorMessage;

        [ObservableProperty]
        private bool _isUnavairableOverwrite;

        [RelayCommand]
        private void SearchForward()
        {
            if (string.IsNullOrEmpty(_searchText))
            {
                SelectedItem = null;               
            }
            else if (_selectedItem == null)
            {
                SelectedItem = Items.Cast<IPathRestructure>().FirstOrDefault(x => x.EditPath.Contains(_searchText));
            }
            else
            {
                var lastSelectedItem = SelectedItem;
                SelectedItem = Items.Cast<IPathRestructure>().SkipWhile(x => x != _selectedItem).Skip(1).FirstOrDefault(x => x.EditPath.Contains(_searchText));
                if (SelectedItem == null)
                {
                    SelectedItem = Items.Cast<IPathRestructure>().TakeWhile(x => x != _selectedItem).FirstOrDefault(x => x.EditPath.Contains(_searchText));
                }
            }
        }

        internal List<IPathRestructure> SearchAll(string searchText)
        {
            if (string.IsNullOrEmpty(searchText)) { return new List<IPathRestructure>(); }

            return Items.Cast<IPathRestructure>().Where(x => x.EditPath.Contains(searchText)).ToList();
        }

        /// <summary>
        /// ファイル名の最初に現れる数値に対して桁数補完をしてリネームする。
        /// </summary>
        [RelayCommand]
        private void DigitCompletionAll()
        {
            ProcessDigitCompletion(Items.Cast<IPathRestructure>());
        }


        [RelayCommand]
        private void DigitCompletionSelectedItems()
        {
            ProcessDigitCompletion(SelectedItems);
        }



        private void ProcessDigitCompletion(IEnumerable<IPathRestructure> sourceItems)
        {
            // フォルダ毎にアイテムを分ける
            Dictionary<string, List<IPathRestructure>> itemPerFolder = new();
            foreach (var item in sourceItems)
            {
                string folderName = Path.GetDirectoryName(item.EditPath);
                if (itemPerFolder.TryGetValue(folderName, out var list) is false)
                {
                    list = new List<IPathRestructure>()
                    {
                        item
                    };
                    itemPerFolder.Add(folderName, list);
                }
                else
                {
                    list.Add(item);
                }
            }

            Regex firstRegex = new Regex(@"(\d+(?=\.))");

            // フォルダ毎のアイテムの必要な桁数を求めて、ファイル名のEditPathを置き換える
            foreach (var (folderName, items) in itemPerFolder)
            {
                int maxDigit = items.Max(x =>
                {
                    string name = Path.GetFileName(x.EditPath);
                    var match = firstRegex.Match(name);
                    if (match.Success)
                    {
                        return TitleDigitCompletionTransform.GetDigitCount(int.Parse(match.Value));
                    }
                    else
                    {
                        return 0;
                    }
                });

                if (maxDigit == 0)
                {
                    // 数字を含むファイル名が見つからない
                    continue;
                }

                Regex secondRegex = new Regex($"0*(\\d{{{maxDigit},}})");

                string digitCompletion = new string(Enumerable.Range(0, maxDigit).Select(_ => '0').ToArray());
                string firstReplace = $"{digitCompletion}$1";
                foreach (var item in items)
                {
                    if (firstRegex.IsMatch(item.EditPath))
                    {
                        var replaced = firstRegex.Replace(item.EditPath, firstReplace);
                        item.EditPath = secondRegex.Replace(replaced, "$1");
                    }
                }
            }
        }



        [RelayCommand]
        private void ToggleIsSplitImage()
        {
            if (SelectedItems.All(x => x.IsSplitImage))
            {
                SelectedItems.ForEach(x => x.IsSplitImage = false);
            }
            else
            {
                SelectedItems.ForEach(x => x.IsSplitImage = true);
            }
        }




        [RelayCommand]
        private void ReplaceAll()
        {
            if (string.IsNullOrEmpty(_searchText)) { return; }

            string replaceText = ReplaceText == null ? string.Empty : ReplaceText;
            foreach (var item in Items.Cast<IPathRestructure>().Where(x => x.EditPath.Contains(_searchText)))
            {
                item.EditPath = item.EditPath.Replace(_searchText, replaceText);
            }
        }

        [RelayCommand]
        private void ReplaceNext()
        {
            if (string.IsNullOrEmpty(_searchText)) { return; }

            SearchForward();

            if (SelectedItem != null)
            {
                string replaceText = ReplaceText == null ? string.Empty : ReplaceText;
                var item = SelectedItem;
                item.EditPath = item.EditPath.Replace(_searchText, replaceText);
            }
        }

        [RelayCommand]
        private void ToggleIsOutputSelectedItems()
        {
            if (SelectedItems.Any(x => x.IsOutput is false))
            {
                SelectedItems.ForEach(x => x.IsOutput = true);
            }
            else
            {
                SelectedItems.ForEach(x => x.IsOutput = false);
            }
        }

        [RelayCommand]
        private async Task OverwriteSaveAsync()
        {
            if (_sourceStorageItem is StorageFile outputFile)
            {
                OutputErrorMessage = null;
                NowProcessOutput = true;

                string fileName = outputFile.Name;
                StorageFolder parentFolder = await outputFile.GetParentAsync();                

                var newOutputFile = await parentFolder.CreateFileAsync(Path.GetRandomFileName());
                try
                {
                    await OutputToArchiveAsync(newOutputFile);

                    string ext = newOutputFile.FileType;
                    await outputFile.DeleteAsync(StorageDeleteOption.Default);
                    string newName = Path.ChangeExtension(fileName, ext);
                    if (newOutputFile.Name != newName)
                    {
                        await newOutputFile.RenameAsync(newName);
                    }
                }
                catch
                {
                    await newOutputFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
                finally
                {
                    NowProcessOutput = false;
                }
            }
            else if (_sourceStorageItem is StorageFolder outputFolder)
            {
                OutputErrorMessage = null;
                NowProcessOutput = true;

                try
                {
                    await OutputToFolderAsync(outputFolder);
                }
                catch (OperationCanceledException)
                {
                    // 半端に残ったフォルダを消すかどうかはユーザーに任せる
                    await Launcher.LaunchFolderAsync(outputFolder);
                }
                catch (UnauthorizedAccessException ex)
                {
                    var path = ex.Message;
                    var fileType = Path.GetExtension(path);
                    OutputErrorMessage = "RestructurePage_InvalidFileType".Translate(path);
                    SelectedItem = _RawItems.FirstOrDefault(x => x.EditPath == path);
                    foreach (var item in _RawItems.Where(x => x.EditPath.EndsWith(fileType)))
                    {
                        item.IsOutput = false;
                    }
                }
                finally
                {
                    NowProcessOutput = false;
                }
            }
        }

        [RelayCommand]
        private async Task OutputToArchiveFileAsync()
        {
            OutputErrorMessage = null;
            NowProcessOutput = true;

            var filePicker = new FileSavePicker()
            {
                SuggestedFileName = Path.ChangeExtension(SourceStorageItem.Name, "zip"),
                FileTypeChoices =
                    {
                        { "zip", new List<string> { ".zip" } }
                    },
                DefaultFileExtension = ".zip",
            };

            var outputFile = await filePicker.PickSaveFileAsync();
            if (outputFile == null)
            {
                return;
            }

            try
            {
                await OutputToArchiveAsync(outputFile);
            }
            finally
            {
                NowProcessOutput = false;
            }
        }

        private async Task OutputToArchiveAsync(StorageFile outputFile)
        {
            await _messenger.WorkWithBusyWallAsync((ct) => Task.Run(async () =>
            {
                using var outputArchive = ZipArchive.Create();

                ValueTask ProcessOutput(string path, Stream contentStream, CancellationToken ct)
                {
                    outputArchive.AddEntry(path, contentStream, closeStream: true);
                    return new ValueTask();
                }

                string outputFileName = null;
                if (SourceStorageItem is StorageFile file
                    && file.IsSupportedMangaFile())
                {
                    outputFileName = file.Name;
                    await OutputArchiveFileAsync_Internal(GetOutputTargetItems(), file, ProcessOutput, ct);
                }
                else if (SourceStorageItem is StorageFolder folder)
                {
                    outputFileName = folder.Name;
                    await OutputFolderAsync_Internal(GetOutputTargetItems(), ProcessOutput, ct);
                }
                else { throw new InvalidOperationException(); }

                var tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(Path.GetRandomFileName(), CreationCollisionOption.GenerateUniqueName);
                try
                {
                    using (var writeStream = await tempFile.OpenStreamForWriteAsync())
                    using (var bufferedStream = new BufferedStream(writeStream))
                    {
                        writeStream.SetLength(0);
                        outputArchive.SaveTo(bufferedStream, new SharpCompress.Writers.WriterOptions(SharpCompress.Common.CompressionType.Deflate));
                        bufferedStream.Flush();
                    }

                    await tempFile.MoveAndReplaceAsync(outputFile);
                    if (outputFile.FileType != ".zip")
                    {
                        await outputFile.RenameAsync(Path.ChangeExtension(outputFileName, "zip"), NameCollisionOption.GenerateUniqueName);
                    }
                }
                catch
                {
                    // MoveAndReplaceAsyncするとtempFile == outputFileになる
                    await tempFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }

            }), CancellationToken.None);
        }


        [RelayCommand]
        private async Task OutputToFolderAsync()
        {
            OutputErrorMessage = null;
            NowProcessOutput = true;

            var folderPicker = new FolderPicker()
            {
                ViewMode = PickerViewMode.List,
            };

            var outputFolder = await folderPicker.PickSingleFolderAsync();
            if (outputFolder == null)
            {
                return;
            }
            
            try
            {
                await OutputToFolderAsync(outputFolder);
            }
            catch (OperationCanceledException)
            {
                // 半端に残ったフォルダを消すかどうかはユーザーに任せる
                await Launcher.LaunchFolderAsync(outputFolder);
            }
            catch (UnauthorizedAccessException ex)
            {
                var path = ex.Message;
                var fileType = Path.GetExtension(path);
                OutputErrorMessage = "RestructurePage_InvalidFileType".Translate(path);
                SelectedItem = _RawItems.FirstOrDefault(x => x.EditPath == path);
                foreach (var item in _RawItems.Where(x => x.EditPath.EndsWith(fileType)))
                {
                    item.IsOutput = false;
                }
            }
            finally
            {
                NowProcessOutput = false;
            }
        }

        private async Task OutputToFolderAsync(StorageFolder outputFolder)
        {
            var items = GetOutputTargetItems();

            await _messenger.WorkWithBusyWallAsync((ct) => Task.Run(async () =>
            {
                async ValueTask ProcessOutput(string path, Stream contentStream, CancellationToken ct)
                {
                    try
                    {
                        using (contentStream)
                        {
                            var outputFile = await outputFolder.DigStorageFileFromPathAsync(path, CreationCollisionOption.ReplaceExisting, ct);
                            using (var fileStream = await outputFile.OpenStreamForWriteAsync())
                            {
                                await contentStream.CopyToAsync(fileStream, 81920, ct);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new UnauthorizedAccessException(path, ex);
                    }
                }

                if (SourceStorageItem is StorageFile file
                        && file.IsSupportedMangaFile())
                {
                    await OutputArchiveFileAsync_Internal(items, file, ProcessOutput, ct);
                }
                else if (SourceStorageItem is StorageFolder)
                {
                    await OutputFolderAsync_Internal(items, ProcessOutput, ct);
                }
                else { throw new InvalidOperationException(); }

            }), CancellationToken.None);
        }

        private List<IPathRestructure> GetOutputTargetItems()
        {
            bool ValidateNotDupulicateEditPath(IEnumerable<IPathRestructure> items)
            {
                HashSet<string> keys = new();
                foreach (var item in items)
                {
                    if (keys.Contains(item.EditPath))
                    {
                        OutputErrorMessage = "RestructurePage_DupulicatePath".Translate(item.EditPath);
                        SelectedItem = item;
                        return false;
                    }
                    keys.Add(item.EditPath);
                }

                return true;
            }

            bool ValidateNotContainsInvalidPathCharactersEditPath(IEnumerable<IPathRestructure> items)
            {
                var invalidPathChar = Path.GetInvalidPathChars().ToHashSet();
                foreach (var item in items)
                {
                    var invalidChar = item.EditPath.FirstOrDefault(c => invalidPathChar.Contains(c));
                    if (invalidChar != default(char))
                    {
                        OutputErrorMessage = "RestructurePage_InvalidPath".Translate(invalidChar);
                        SelectedItem = item;
                        return false;
                    }
                }

                return true;
            }

            var items = Items.Cast<IPathRestructure>().Where(x => x.IsOutput).ToList();

            foreach (var item in items)
            {
                item.EditPath = item.EditPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            if (ValidateNotDupulicateEditPath(items) is false) { throw new InvalidOperationException(); }
            if (ValidateNotContainsInvalidPathCharactersEditPath(items) is false) { throw new InvalidOperationException(); }

            return items;
        }

        async Task OutputFolderAsync_Internal(IEnumerable<IPathRestructure> items, Func<string, Stream, CancellationToken, ValueTask> processOutput, CancellationToken ct)
        {
            foreach (var item in items.Cast<PathRestructure_StorageFile>())
            {
                if (Path.HasExtension(item.EditPath) is false) { continue; }

                if (item is PathRestructure_StorageFile fileItem)
                {
                    if (item.IsSplitImage is false)
                    {
                        await processOutput(item.EditPath, await fileItem.File.OpenStreamForReadAsync(), ct);
                    }
                    //else if ()
                    //{
                    //    var fileStream = await fileItem.File.OpenStreamForReadAsync();
                    //    try
                    //    {
                    //        var (clipImage, ext) = await _splitImageTransform.ClipCoverImageAsync(_splitImageAspectRatio, null, fileStream, ct);
                    //        await processOutput(Path.ChangeExtension(item.EditPath, ext), clipImage, ct);
                    //    }
                    //    finally
                    //    {
                    //        fileStream.Dispose();
                    //    }
                    //}
                    else
                    {
                        if (IsSplitWithLeftBinding)
                        {
                            var fileStream = await fileItem.File.OpenStreamForReadAsync();

                            try
                            {
                                var (leftImage, rightImage, ext) = await _splitImageTransform.SplitTwoImageAsync(_splitImageAspectRatio, null, fileStream, ct);

                                var dir = Path.GetDirectoryName(item.EditPath);
                                var nameWoExt = Path.GetFileNameWithoutExtension(item.EditPath);
                                await processOutput(Path.Combine(dir, $"{nameWoExt}_0.{ext}"), rightImage, ct);
                                await processOutput(Path.Combine(dir, $"{nameWoExt}_1.{ext}"), leftImage, ct);
                            }
                            finally
                            {
                                fileStream.Dispose();
                            }
                        }
                        else
                        {
                            var fileStream = await fileItem.File.OpenStreamForReadAsync();

                            try
                            {
                                var (leftImage, rightImage, ext) = await _splitImageTransform.SplitTwoImageAsync(_splitImageAspectRatio, null, fileStream, ct);

                                var dir = Path.GetDirectoryName(item.EditPath);
                                var nameWoExt = Path.GetFileNameWithoutExtension(item.EditPath);
                                await processOutput(Path.Combine(dir, $"{nameWoExt}_0.{ext}"), leftImage, ct);
                                await processOutput(Path.Combine(dir, $"{nameWoExt}_1.{ext}"), rightImage, ct);
                            }
                            finally
                            {
                                fileStream.Dispose();
                            }
                        }
                    }
                }
            }
        }

        async Task OutputArchiveFileAsync_Internal(IEnumerable<IPathRestructure> items, StorageFile file, Func<string, Stream, CancellationToken, ValueTask> processOutput, CancellationToken ct)
        {
            using (var fileStream = await file.OpenStreamForReadAsync())
            using (var archive = ArchiveFactory.Open(fileStream))
            {
                var keyToEntry = archive.Entries.ToDictionary(x => x.Key);
                foreach (var item in items)
                {
                    var entry = keyToEntry[item.SourcePath];
                    if (entry.IsDirectory) { continue; }
                    if (Path.HasExtension(item.EditPath) is false) { continue; }

                    var memoryStream = new MemoryStream();
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        await entryStream.CopyToAsync(memoryStream, 81920, ct);
                        memoryStream.Seek(0, SeekOrigin.Begin);
                    }

                    if (item.IsSplitImage is false)
                    {
                        await processOutput(item.EditPath, memoryStream, ct);
                    }
                    else
                    {
                        if (IsSplitWithLeftBinding)
                        {
                            var (leftImage, rightImage, ext) = await _splitImageTransform.SplitTwoImageAsync(_splitImageAspectRatio, null, memoryStream, ct);

                            var dir = Path.GetDirectoryName(item.EditPath);
                            var nameWoExt = Path.GetFileNameWithoutExtension(item.EditPath);
                            await processOutput(Path.Combine(dir, $"{nameWoExt}_0.{ext}"), rightImage, ct);
                            await processOutput(Path.Combine(dir, $"{nameWoExt}_1.{ext}"), leftImage, ct);
                        }
                        else
                        {
                            var (leftImage, rightImage, ext) = await _splitImageTransform.SplitTwoImageAsync(_splitImageAspectRatio, null, memoryStream, ct);

                            var dir = Path.GetDirectoryName(item.EditPath);
                            var nameWoExt = Path.GetFileNameWithoutExtension(item.EditPath);
                            await processOutput(Path.Combine(dir, $"{nameWoExt}_0.{ext}"), leftImage, ct);
                            await processOutput(Path.Combine(dir, $"{nameWoExt}_1.{ext}"), rightImage, ct);
                        }
                    }
                    /*
                    else if (item.IsSplitImage == SplitImageProcessKind.Cover)
                    {
                        try
                        {
                            var (clipImage, ext) = await _splitImageTransform.ClipCoverImageAsync(_splitImageAspectRatio, null, memoryStream, ct);
                            await processOutput(Path.ChangeExtension(item.EditPath, ext), clipImage, ct);
                        }
                        finally
                        {
                            memoryStream.Dispose();
                        }
                    }
                    */                    
                }
            }

            // TODO: file.Pathに対するキャッシュ情報をクリアする
        }

        [RelayCommand]
        private async Task OutputToArchiveSplitWithPartAsync()
        {
            OutputErrorMessage = null;
            NowProcessOutput = true;

            var folderPicker = new FolderPicker()
            {
                ViewMode = PickerViewMode.List,
            };

            var outputFolder = await folderPicker.PickSingleFolderAsync();
            if (outputFolder == null)
            {
                return;
            }

            var items = GetOutputTargetItems();
            Dictionary<string, List<IPathRestructure>> itemsPerArchive = new();
            {
                Dictionary<int, HashSet<string>> dirNamesByFolderDepth = new();
                foreach (var path in items.Select(x => x.EditPath))
                {
                    var names = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < names.Length - 1; i++)
                    {
                        dirNamesByFolderDepth.TryGetValue(i, out var dirNames);
                        if (dirNames == null)
                        {
                            dirNames = new HashSet<string>();
                            dirNamesByFolderDepth.Add(i, dirNames);
                        }

                        dirNames.Add(names[i]);
                    }
                }

                if (dirNamesByFolderDepth.Count == 0)
                {
                    OutputErrorMessage = "";
                    return;
                }

                string baseDir = string.Empty;
                var rootDepth = 0;
                for (int i = 0; i < dirNamesByFolderDepth.Count; i++)
                {
                    if (dirNamesByFolderDepth[i].Count > 1)
                    {
                        rootDepth = i;
                        break;
                    }

                    baseDir = Path.Combine(baseDir, dirNamesByFolderDepth[i].First());
                }

                dirNamesByFolderDepth.TryGetValue(rootDepth, out var targetDir);
                foreach (var dir in targetDir)
                {
                    var fileName = Path.Combine(baseDir, dir);
                    var dirItems = items.Where(x => x.EditPath.StartsWith(fileName)).ToList();
                    itemsPerArchive.Add(fileName, dirItems);
                }
            }

            try
            {
                await _messenger.WorkWithBusyWallAsync((ct) => Task.Run(async () =>
                {
                    foreach (var (fileName, items) in itemsPerArchive)
                    {
                        using var outputArchive = ZipArchive.Create();

                        ValueTask ProcessOutput(string path, Stream contentStream, CancellationToken ct)
                        {
                            outputArchive.AddEntry(path, contentStream, closeStream: true);
                            return new ValueTask();
                        }

                        string outputFileName = null;
                        if (SourceStorageItem is StorageFile file
                            && file.IsSupportedMangaFile())
                        {
                            outputFileName = file.Name;
                            await OutputArchiveFileAsync_Internal(items, file, ProcessOutput, ct);
                        }
                        else if (SourceStorageItem is StorageFolder folder)
                        {
                            outputFileName = folder.Name;
                            await OutputFolderAsync_Internal(items, ProcessOutput, ct);
                        }
                        else { throw new InvalidOperationException(); }

                        var tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(Path.GetRandomFileName(), CreationCollisionOption.GenerateUniqueName);
                        try
                        {
                            using (var writeStream = await tempFile.OpenStreamForWriteAsync())
                            using (var bufferedStream = new BufferedStream(writeStream))
                            {
                                writeStream.SetLength(0);
                                outputArchive.SaveTo(bufferedStream, new SharpCompress.Writers.WriterOptions(SharpCompress.Common.CompressionType.Deflate));
                                bufferedStream.Flush();
                            }

                            var outputFile = await outputFolder.DigStorageFileFromPathAsync(fileName + ".zip", CreationCollisionOption.ReplaceExisting, ct);
                            await tempFile.MoveAndReplaceAsync(outputFile);
                        }
                        catch
                        {
                            // MoveAndReplaceAsyncするとtempFile == outputFileになる
                            await tempFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        }
                    }
                }), CancellationToken.None);
            }
            finally
            {
                NowProcessOutput = false;
            }
        }
    }

    public interface IPathRestructure
    {
        bool IsOutput { get; set; }
        string SourcePath { get; }
        string EditPath { get; set; }

        bool IsSplitImage { get; set; }

        void Reset();
    }



    partial class PathRestructure_ArchiveEntry : ObservableObject, IPathRestructure
    {
        public PathRestructure_ArchiveEntry(string relativePath)
        {
            SourcePath = relativePath;
            _EditPath = relativePath;
        }

        public string SourcePath { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEdited))]
        private string _EditPath;

        [ObservableProperty]
        private bool _IsOutput = true;

        public bool IsEdited => _EditPath != SourcePath;

        [ObservableProperty]
        private bool _isSplitImage;


        public void Reset()
        {
            EditPath = SourcePath;
        }
    }

    partial class PathRestructure_StorageFile : ObservableObject, IPathRestructure
    {
        public PathRestructure_StorageFile(string relativePath, StorageFile file)
        {
            SourcePath = relativePath;
            File = file;
            _EditPath = relativePath;
        }

        public StorageFile File { get; }

        public string SourcePath { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEdited))]
        private string _EditPath;

        [ObservableProperty]
        private bool _IsOutput = true;

        public bool IsEdited => _EditPath != SourcePath;

        [ObservableProperty]
        private bool _isSplitImage;


        public void Reset()
        {
            EditPath = SourcePath;
        }    
    }
}
