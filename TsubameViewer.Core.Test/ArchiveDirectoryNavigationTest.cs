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
        public void SingleLeafContainingAllFilesIsSelectedRegardlessOfNamesOrDepth()
        {
            var collection = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = Array.Empty<int>(),
                ["unrelated-name/"] = Array.Empty<int>(),
                ["unrelated-name/chapter"] = Array.Empty<int>(),
                ["unrelated-name/chapter/pages/"] = new[] { 0 },
                ["unrelated-name/chapter/pages"] = new[] { 1 },
            });
            Assert.AreSame(collection.GetDirectoryTokenFromPath("unrelated-name/chapter/pages"),
                collection.GetSingleLeafDirectoryContainingAllFiles());
            var context = new ArchiveImageCollectionContext(collection, collection.RootDirectoryToken, skipSingleLeafDirectory: true);
            Assert.IsFalse(context.IsExistFolderOrArchiveFileAsync(default).Result);
            Assert.IsTrue(context.IsExistImageFileAsync(default).Result);

            var unflattened = new ArchiveImageCollectionContext(collection, collection.RootDirectoryToken);
            Assert.IsTrue(unflattened.IsExistFolderOrArchiveFileAsync(default).Result);
            Assert.IsFalse(unflattened.IsExistImageFileAsync(default).Result);
        }

        [TestMethod]
        public void SingleLeafWithFilesOutsideItIsNotSelected()
        {
            var withRootFile = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = new[] { 0 },
                ["book"] = Array.Empty<int>(),
                ["book/chapter"] = new[] { 1 },
            });
            Assert.IsNull(withRootFile.GetSingleLeafDirectoryContainingAllFiles());

            var withIntermediateFile = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = Array.Empty<int>(),
                ["book"] = new[] { 0 },
                ["book/chapter"] = new[] { 1 },
            });
            Assert.IsNull(withIntermediateFile.GetSingleLeafDirectoryContainingAllFiles());
        }

        [TestMethod]
        public void MultipleLeavesOrNoLeafFilesAreNotSelected()
        {
            var multipleLeaves = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = Array.Empty<int>(),
                ["book"] = new[] { 1 },
                ["empty"] = Array.Empty<int>(),
            });
            Assert.IsNull(multipleLeaves.GetSingleLeafDirectoryContainingAllFiles());
            var context = new ArchiveImageCollectionContext(multipleLeaves, multipleLeaves.RootDirectoryToken, skipSingleLeafDirectory: true);
            Assert.IsTrue(context.IsExistFolderOrArchiveFileAsync(default).Result);
            Assert.IsFalse(context.IsExistImageFileAsync(default).Result);

            var emptyLeaf = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = Array.Empty<int>(),
                ["book"] = Array.Empty<int>(),
            });
            Assert.IsNull(emptyLeaf.GetSingleLeafDirectoryContainingAllFiles());

            var noDirectory = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = new[] { 1 },
            });
            Assert.IsNull(noDirectory.GetSingleLeafDirectoryContainingAllFiles());
        }

        [TestMethod]
        public void FolderListingSkipsEmptySingleChildAncestors()
        {
            var collection = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = Array.Empty<int>(),
                ["wrapper/"] = Array.Empty<int>(),
                ["wrapper/inner"] = Array.Empty<int>(),
                ["wrapper/inner/chapter1"] = new[] { 0 },
                ["wrapper/inner/chapter2"] = new[] { 1 },
            });
            var listingRoot = collection.GetFolderListingRoot();
            Assert.AreSame(collection.GetDirectoryTokenFromPath("wrapper/inner"), listingRoot);
            CollectionAssert.AreEquivalent(new[] { "wrapper/inner/chapter1", "wrapper/inner/chapter2" },
                collection.GetSubDirectories(listingRoot).Select(token => token.Key).ToArray());

            var context = new ArchiveImageCollectionContext(collection, collection.RootDirectoryToken, skipSingleLeafDirectory: true);
            Assert.IsTrue(context.IsExistFolderOrArchiveFileAsync(default).Result);
            Assert.IsFalse(context.IsExistImageFileAsync(default).Result);
        }

        [TestMethod]
        public void FolderListingPreservesDirectoriesWithFilesOrMultipleRootChildren()
        {
            var withRootFile = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = new[] { 0 },
                ["wrapper"] = Array.Empty<int>(),
                ["wrapper/chapter"] = new[] { 1 },
            });
            Assert.AreSame(withRootFile.RootDirectoryToken, withRootFile.GetFolderListingRoot());

            var withWrapperFile = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = Array.Empty<int>(),
                ["wrapper/"] = new[] { 0 },
                ["wrapper/chapter"] = new[] { 1 },
            });
            Assert.AreSame(withWrapperFile.RootDirectoryToken, withWrapperFile.GetFolderListingRoot());

            var withMultipleRootChildren = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = Array.Empty<int>(),
                ["wrapper"] = Array.Empty<int>(),
                ["wrapper/chapter"] = new[] { 1 },
                ["other"] = Array.Empty<int>(),
            });
            Assert.AreSame(withMultipleRootChildren.RootDirectoryToken, withMultipleRootChildren.GetFolderListingRoot());
        }

        [TestMethod]
        public void FolderListingStopsBeforeAChildContainingFiles()
        {
            var collection = CreateCollection(new Dictionary<string, int[]>
            {
                [""] = Array.Empty<int>(),
                ["wrapper"] = Array.Empty<int>(),
                ["wrapper/inner"] = new[] { 0 },
                ["wrapper/inner/chapter"] = new[] { 1 },
            });
            var listingRoot = collection.GetFolderListingRoot();
            Assert.AreSame(collection.GetDirectoryTokenFromPath("wrapper"), listingRoot);
            Assert.AreSame(collection.GetDirectoryTokenFromPath("wrapper/inner"), collection.GetSubDirectories(listingRoot).Single());
        }

        private static ArchiveImageCollection CreateCollection(Dictionary<string, int[]> filesByFolder)
        {
            var structure = new ArchiveFileInnerSturcture
            {
                Items = new[] { "readme.txt", "book/page.jpg" },
                FileIndexies = filesByFolder.Values.SelectMany(x => x).Distinct().ToArray(),
                FileIndexiesSortWithDateTime = filesByFolder.Values.SelectMany(x => x).Distinct().ToArray(),
                FolderPathSeparator = '/',
                DirectoryEntryIndex = new Dictionary<string, int>(),
                FilesByFolder = filesByFolder,
            };
            return new ArchiveImageCollection(null, null, structure, null);
        }
    }
}
