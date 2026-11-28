using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using static TsubameViewer.Core.Models.ImageViewer.ArchiveFileInnerStructureCache;

namespace TsubameViewer.Core.Test
{
    [TestClass]
    public class ArchiveDirectoryNavigationTest
    {
        [TestMethod]
        public void DirectoryEntriesAndImplicitDirectoriesFormOneHierarchy()
        {
            var structure = new ArchiveFileInnerSturcture
            {
                Items = new[] { "cover.jpg", "book/chapter/page.jpg" },
                FileIndexies = new[] { 0, 1 },
                FileIndexiesSortWithDateTime = new[] { 0, 1 },
                FolderPathSeparator = '/',
                DirectoryEntryIndex = new Dictionary<string, int>(),
                FilesByFolder = new Dictionary<string, int[]>
                {
                    [""] = new[] { 0 },
                    ["book/"] = Array.Empty<int>(),
                    ["book"] = Array.Empty<int>(),
                    ["book/chapter/"] = Array.Empty<int>(),
                    ["book/chapter"] = new[] { 1 },
                },
            };
            var collection = new ArchiveImageCollection(null, null, structure, null);
            var root = collection.RootDirectoryToken;
            var book = collection.GetSubDirectories(root).Single();
            var chapter = collection.GetSubDirectories(book).Single();

            Assert.AreEqual("", root.Key);
            Assert.AreEqual("book", book.Key);
            Assert.AreEqual("book/chapter", chapter.Key);
            Assert.AreSame(root, book.ParentToken);
            Assert.AreSame(book, chapter.ParentToken);
            Assert.AreSame(chapter, collection.GetDirectoryTokenFromPath("book\\chapter\\"));
            Assert.AreEqual(1, root.FileIndexes.Length);
            Assert.AreEqual(1, chapter.FileIndexes.Length);
            Assert.AreEqual(0, chapter.Children.Count);
        }

        [TestMethod]
        public void SettingsParentFollowsArchiveHierarchy()
        {
            Assert.AreEqual(@"C:\library\book.zip?p=book/chapter", DisplaySettingsByPathRepository.GetParentPath(@"C:\library\book.zip?p=book/chapter/pages"));
            Assert.AreEqual(@"C:\library\book.zip?p=book", DisplaySettingsByPathRepository.GetParentPath(@"C:\library\book.zip?p=book/chapter"));
            Assert.AreEqual(@"C:\library\book.zip", DisplaySettingsByPathRepository.GetParentPath(@"C:\library\book.zip?p=book"));
            Assert.AreEqual(@"C:\library", DisplaySettingsByPathRepository.GetParentPath(@"C:\library\book.zip"));
        }

        [TestMethod]
        public void MatchingRootIsFlattenedWithoutHidingOtherItems()
        {
            var collection = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = new[] { 0 },
                ["Book"] = new[] { 1 },
                ["Book/chapter"] = Array.Empty<int>(),
                ["other"] = Array.Empty<int>(),
            });
            var book = collection.GetDirectoryTokenFromPath("Book");
            var chapter = collection.GetDirectoryTokenFromPath("Book/chapter");
            var other = collection.GetDirectoryTokenFromPath("other");

            Assert.IsTrue(collection.IsMatchingArchiveRootDirectory(book, "book.zip"));
            Assert.IsFalse(collection.IsMatchingArchiveRootDirectory(chapter, "chapter.zip"));
            CollectionAssert.AreEquivalent(new[] { chapter, other }, collection.GetVisibleRootDirectories("book.zip").ToArray());
            CollectionAssert.AreEqual(new[] { collection.RootDirectoryToken, book }, collection.GetVisibleRootImageDirectories("book.zip").ToArray());
            Assert.IsFalse(collection.HasOnlyMatchingRootDirectoryWithoutSubdirectories("book.zip"));
        }

        [TestMethod]
        public void OnlyMatchingRootRequiresNoOtherFilesOrSubdirectories()
        {
            var onlyBook = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = Array.Empty<int>(),
                ["book/"] = new[] { 1 },
            });
            Assert.IsTrue(onlyBook.HasOnlyMatchingRootDirectoryWithoutSubdirectories("Book.zip"));
            Assert.IsFalse(onlyBook.HasOnlyMatchingRootDirectoryWithoutSubdirectories("other.zip"));

            var withSubdirectory = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = Array.Empty<int>(),
                ["book"] = Array.Empty<int>(),
                ["book/chapter"] = new[] { 1 },
            });
            Assert.IsFalse(withSubdirectory.HasOnlyMatchingRootDirectoryWithoutSubdirectories("book.zip"));
            CollectionAssert.AreEqual(new[] { withSubdirectory.GetDirectoryTokenFromPath("book/chapter") },
                withSubdirectory.GetVisibleRootDirectories("book.zip").ToArray());

            var withImagesAndSubdirectory = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = Array.Empty<int>(),
                ["book"] = new[] { 1 },
                ["book/chapter"] = Array.Empty<int>(),
            });
            Assert.IsFalse(withImagesAndSubdirectory.HasOnlyMatchingRootDirectoryWithoutSubdirectories("book.zip"));

            var withOtherFile = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = new[] { 0 },
                ["book"] = new[] { 1 },
            });
            Assert.IsFalse(withOtherFile.HasOnlyMatchingRootDirectoryWithoutSubdirectories("book.zip"));
        }

        private static ArchiveImageCollection CreateCollection(Dictionary<string, int[]> filesByFolder)
        {
            var structure = new ArchiveFileInnerSturcture
            {
                Items = new[] { "readme.txt", "book/page.jpg" },
                FileIndexies = new[] { 0, 1 },
                FileIndexiesSortWithDateTime = new[] { 0, 1 },
                FolderPathSeparator = '/',
                DirectoryEntryIndex = new Dictionary<string, int>(),
                FilesByFolder = filesByFolder,
            };
            return new ArchiveImageCollection(null, null, structure, null);
        }
    }
}
