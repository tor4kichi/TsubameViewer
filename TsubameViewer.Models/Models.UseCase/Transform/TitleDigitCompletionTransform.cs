using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.ImageViewer;
using Windows.Storage;
using TsubameViewer.Models.Domain;
using System.IO;
using SharpCompress.Archives;
using SharpCompress.Writers;

namespace TsubameViewer.Models.UseCase.Transform
{
    public static class TitleDigitCompletionTransform
    {

        // 桁数、必ず１以上を返す
        public static int GetDigitCount(int number)
        {
            if (number == 0) { return 1; }

            int tempNum = number;
            int digitCount = 0;
            while (tempNum > 0)
            {
                tempNum /= 10;
                digitCount++;
            }
            return digitCount;
        }

        /// <summary>
        /// フォルダに含まれるファイルを対象にファイル名の桁数補完処理を実行する。<br />
        /// 拡張子を除くファイル名として "1" と "10" がある場合 "1" → "01" とリネームする <br />
        /// フォルダ内の全ファイルを走査して最大桁数を求めてから補完すべき桁数を決定する。 <br />
        /// 例えば 最大数が 1000 だった場合、 "1" は "0001" とリネームされる。
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="digitCompletionChar"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static async Task<bool> TransformFolderFilesAsync(StorageFolder folder, char digitCompletionChar, Action<(string Old, string New)>? onRenamed, CancellationToken ct)
        {
            var query = folder.CreateFileQueryWithOptions(FolderImageCollectionContext.CreateDefaultImageFileSearchQueryOptions(Domain.FolderItemListing.FileSortType.TitleDecending));
            uint fileCount = await query.GetItemCountAsync();
            
            if (fileCount == 0) { return false; }

            SortedDictionary<int, StorageFile> files = new SortedDictionary<int, StorageFile>();            
            await foreach (var file in query.ToAsyncEnumerable(ct))
            {
                if (int.TryParse(System.IO.Path.GetFileNameWithoutExtension(file.Name), out int num))
                {
                    files.Add(num, file);
                }
            }

            if (files.Any() is false) { return false; }

            int maxNumber = files.Keys.Max();
            int digitCount = GetDigitCount(maxNumber);
            // digitCount == 4 のとき
            // m[0] = ""
            // m[1] = "0"
            // m[2] = "00"
            // m[3] = "000"                        
            string[] m = Enumerable.Range(0, digitCount).Select(x => new string(Enumerable.Range(0, x).Select(_ => digitCompletionChar).ToArray())).ToArray();
            foreach (var (num, file) in files)
            {
                string prependStr = m[digitCount - GetDigitCount(num)];
                string ext = Path.GetExtension(file.Name);
                string newName = $"{prependStr}{num}{ext}";
                if (file.Name != newName)
                {
                    await file.RenameAsync(newName, NameCollisionOption.FailIfExists);
                    onRenamed?.Invoke((file.Name, newName));
                }                
            }

            return true;
        }



        // Archiveを開くためにも表示用に作られたStorageItemViewModelを閉じる必要がある
        // 処理用のページを開いた上で操作しないといけない

        /// <summary>
        /// アーカイブ内エントリの
        /// </summary>
        /// <param name="srcArchive"></param>
        /// <param name="digitCompletionChar"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static (bool Result, IWritableArchive Archive) TransformArchive(IArchive srcArchive, char digitCompletionChar, Action<(string Old, string New)>? onRenamed, CancellationToken ct)
        {
            // アーカイブ内でフォルダが別れている場合がある
            // 各アーカイブ内フォルダごとに数字の最大数を出して処理する必要があるけど
            // 面倒なので最大桁数は全体で統一して扱う
           
            int maxNumber = srcArchive.Entries.Select(item => int.TryParse(Path.GetFileNameWithoutExtension(item.Key), out int num) ? num : 0).Max();

            if (maxNumber == 0)
            {
                return (false, null);
            }
            int digitCount =  GetDigitCount(maxNumber);
            // digitCount == 4 のとき
            // m[0] = ""
            // m[1] = "0"
            // m[2] = "00"
            // m[3] = "000"                        
            string[] m = Enumerable.Range(0, digitCount).Select(x => new string(Enumerable.Range(0, x).Select(_ => digitCompletionChar).ToArray())).ToArray();

            var destArchive = SharpCompress.Archives.ArchiveFactory.Create(srcArchive.Type);

            // 並びはそのまま維持して桁数補完できるアイテムのみを
            foreach (var item in srcArchive.Entries)
            {
                var memoryStream = new MemoryStream();
                using (var entryStream = item.OpenEntryStream())
                {
                    entryStream.CopyTo(memoryStream);
                }

                string NameWOExt = Path.GetFileNameWithoutExtension(item.Key);
                if (item.IsDirectory is false
                        && int.TryParse(NameWOExt, out int num))
                {
                    int currentDigitCount = GetDigitCount(num);
                    string prependStr = m[digitCount - currentDigitCount];
                    string directoryName = Path.GetDirectoryName(item.Key);
                    string ext = Path.GetExtension(item.Key);
                    string newKey = Path.Combine(directoryName, $"{prependStr}{num}{ext}");
                    destArchive.AddEntry(newKey, memoryStream, true);

                    onRenamed?.Invoke((item.Key, newKey));
                }
                else
                {
                    destArchive.AddEntry(item.Key, memoryStream, true);
                }
            }

            return (true, destArchive);
        }

        public static async Task<bool> TransformArchiveFileAsync(StorageFile file, char digitCompletionChar, SharpCompress.Common.CompressionType compressionType, Action<(string Old, string New)>? onRenamed, CancellationToken ct)
        {
            if (SupportedFileTypesHelper.IsSupportedMangaFile(file) is false) { throw new NotSupportedException(); }

            IWritableArchive destArchive;
            using (var stream = await file.OpenStreamForReadAsync())
            using (var srcArchive = ArchiveFactory.Open(stream))
            {
                (var result, destArchive) = TransformArchive(srcArchive, digitCompletionChar, onRenamed, ct);
                if (result is false)
                {
                    return false;
                }
            }

            using (var stream = await file.OpenStreamForWriteAsync())
            using (destArchive)
            {
                stream.SetLength(0);
                destArchive.SaveTo(stream, new WriterOptions(compressionType));
            }

            return true;
        }
    }
}
