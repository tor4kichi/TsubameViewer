using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.UseCase.Transform;
using Windows.Storage;

namespace TsubameViewer.Models.Test
{
    [TestClass]
    public class TitleDigitCompletionTransformTest
    {
        [TestCleanup]
        public async Task CleanUpFiles()
        {
            var files = await ApplicationData.Current.TemporaryFolder.GetFilesAsync();
            foreach (var file in files)
            {
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }

        [TestMethod]
        public async Task FilesTest()
        {
            await CleanUpFiles();

            string[] titles = new string[]
            {
                "0.jpg",
                "1.jpg",
                "10.jpg",
                "11.jpg",
                "100.jpg",
                "999.jpg",
            };

            foreach (var title in titles)
            {
                await ApplicationData.Current.TemporaryFolder.CreateFileAsync(title);
            }

            await TitleDigitCompletionTransform.TransformFolderFilesAsync(ApplicationData.Current.TemporaryFolder, '0', null, CancellationToken.None);

            var files = await ApplicationData.Current.TemporaryFolder.GetFilesAsync();
            foreach (var file in files)
            {
                Debug.WriteLine(file.Name);
            }
        }

        [TestMethod]
        public void TransformArchive_SingleDirectory()
        {
            string[] titles = new string[] 
            {
                "images/0.jpg",
                "images/1.jpg",
                "images/10.jpg",
                "images/11.jpg",
                "images/100.jpg",
                "images/999.jpg",
            };
            using (var srcArchive = ArchiveFactory.Create(SharpCompress.Common.ArchiveType.Zip))
            using (var memoryStream = new MemoryStream())
            {
                foreach (var title in titles)
                {
                    srcArchive.AddEntry(title, memoryStream, false);
                }

                var (result, destArchive) = TitleDigitCompletionTransform.TransformArchive(srcArchive, '0', null, CancellationToken.None);

                Assert.IsTrue(result);

                using (destArchive)
                {
                    foreach (var entry in destArchive.Entries)
                    {
                        Debug.WriteLine(entry.Key);
                    }
                }
            }
        }

        [TestMethod]
        public async Task TransformArchiveFileAsync()
        {
            string[] titles = new string[]
            {
                "images/0.jpg",
                "images/1.jpg",
                "images/10.jpg",
                "images/11.jpg",
                "images/100.jpg",
                "images/999.jpg",
            };

            var file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("archive.zip", CreationCollisionOption.ReplaceExisting);
            using (var srcArchive = ArchiveFactory.Create(SharpCompress.Common.ArchiveType.Zip))
            using (var memoryStream = new MemoryStream())
            {
                foreach (var title in titles)
                {
                    srcArchive.AddEntry(title, memoryStream, false);
                }


                using (var stream = await file.OpenStreamForWriteAsync())
                {
                    srcArchive.SaveTo(stream, new SharpCompress.Writers.WriterOptions(SharpCompress.Common.CompressionType.None));
                }
            }

            await TitleDigitCompletionTransform.TransformArchiveFileAsync(file, '0', SharpCompress.Common.CompressionType.None, null, CancellationToken.None);

            using (var fileStream = await file.OpenStreamForReadAsync())
            using (var archive = ArchiveFactory.Open(fileStream)) 
            {
                foreach (var entry in archive.Entries)
                {
                    Debug.WriteLine(entry.Key);
                }
            }
        }

        [TestMethod]
        public void TransformArchive_MultipleDirectory()
        {
            string[] titles = new string[]
            {
                "images1/cover.jpg",
                "images1/0.jpg",
                "images1/1.jpg",
                "images1/10.jpg",
                "images20/0.jpg",
                "images20/1.jpg",
                "images20/10.jpg",
                "images20/100.jpg",
                "omake1.jpg",
                "omake10.jpg",
            };
            using (var srcArchive = ArchiveFactory.Create(SharpCompress.Common.ArchiveType.Zip))
            using (var memoryStream = new MemoryStream())
            {
                foreach (var title in titles)
                {
                    srcArchive.AddEntry(title, memoryStream, false);
                }

                var (result, destArchive) = TitleDigitCompletionTransform.TransformArchive(srcArchive, '0', null, CancellationToken.None);

                Assert.IsTrue(result);

                using (destArchive)
                {
                    foreach (var entry in destArchive.Entries)
                    {
                        Debug.WriteLine(entry.Key);
                    }
                }
            }

        }

    }
}
