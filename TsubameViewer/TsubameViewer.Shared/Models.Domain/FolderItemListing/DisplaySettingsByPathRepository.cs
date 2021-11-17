using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.FolderItemListing
{
    public record FolderAndArchiveDisplaySettingEntry
    {
        [BsonId]
        public string Path { get; init; }

        public FileSortType Sort { get; init; }

        public bool IsTitleDigitInterpolation { get; init; }
    }

    public record FolderAndArchiveChildFileDisplaySettingEntry
    {
        [BsonId]
        public string Path { get; init; }

        public FileSortType? ChildItemDefaultSort { get; init; }
    }

    public record FileDisplaySettingEntry
    {
        [BsonId]
        public string Path { get; init; }


        public FileSortType Sort { get; init; }

        public bool IsTitleDigitInterpolation { get; init; }
    }

    public sealed class DisplaySettingsByPathRepository
    {
        public sealed class InternalFolderAndArchiveDisplaySettingsByPathRepository : LiteDBServiceBase<FolderAndArchiveDisplaySettingEntry>
        {
            public InternalFolderAndArchiveDisplaySettingsByPathRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
            }

            public FolderAndArchiveDisplaySettingEntry FindById(string path)
            {
                return _collection.FindById(path);
            }

            public int DeleteUnderPath(string path)
            {
                return _collection.DeleteMany(x => x.Path.StartsWith(path));
            }
        }

        public sealed class InternalFileDisplaySettingsByPathRepository : LiteDBServiceBase<FileDisplaySettingEntry>
        {
            public InternalFileDisplaySettingsByPathRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
            }

            public FileDisplaySettingEntry FindById(string path)
            {
                return _collection.FindById(path);
            }

            public int DeleteUnderPath(string path)
            {
                return _collection.DeleteMany(x => x.Path.StartsWith(path));
            }
        }

        public sealed class InternalFolderAndArchiveChildFileDisplaySettingsByPathRepository : LiteDBServiceBase<FolderAndArchiveChildFileDisplaySettingEntry>
        {
            public InternalFolderAndArchiveChildFileDisplaySettingsByPathRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
            }

            public FolderAndArchiveChildFileDisplaySettingEntry FindById(string path)
            {
                return _collection.FindById(path);
            }

            public int DeleteUnderPath(string path)
            {
                return _collection.DeleteMany(x => x.Path.StartsWith(path));
            }
        }


        private readonly InternalFolderAndArchiveDisplaySettingsByPathRepository _internalFolderAndArchiveRepository;
        private readonly InternalFileDisplaySettingsByPathRepository _internalFileRepository;
        private readonly InternalFolderAndArchiveChildFileDisplaySettingsByPathRepository _internalChildFileRepository;

        public DisplaySettingsByPathRepository(
            InternalFolderAndArchiveDisplaySettingsByPathRepository folderAndArchiveRepository,
            InternalFileDisplaySettingsByPathRepository fileRepository,
            InternalFolderAndArchiveChildFileDisplaySettingsByPathRepository childFileRepository


            )
        {
            _internalFolderAndArchiveRepository = folderAndArchiveRepository;
            _internalFileRepository = fileRepository;
            _internalChildFileRepository = childFileRepository;
        }

        public FolderAndArchiveDisplaySettingEntry GetFolderAndArchiveSettings(string path)
        {
            return _internalFolderAndArchiveRepository.FindById(path);
        }

        public void SetFolderAndArchiveSettings(string path, FileSortType sortType, bool withTitleDigitInterpolation)
        {
            _internalFolderAndArchiveRepository.UpdateItem(new FolderAndArchiveDisplaySettingEntry() 
            {
                Path =  path,
                Sort = sortType,
                IsTitleDigitInterpolation = withTitleDigitInterpolation
            });
        }



        public FileDisplaySettingEntry GetFileSettings(string path)
        {
            return _internalFileRepository.FindById(path);
        }


        public void SetFileSettings(string path, FileSortType sortType, bool withTitleDigitInterpolation)
        {
            _internalFileRepository.UpdateItem(new FileDisplaySettingEntry()
            {
                Path = path,
                Sort = sortType,
                IsTitleDigitInterpolation = withTitleDigitInterpolation
            });
        }

        public void ClearFileSettings(string path)
        {
            _internalFileRepository.DeleteUnderPath(path);
        }

        public FileSortType? GetFileParentSettings(string path)
        {
            while (!string.IsNullOrEmpty(path))
            {
                if (_internalChildFileRepository.FindById(path) is not null and var entry && entry.ChildItemDefaultSort != null)
                {
                    return entry.ChildItemDefaultSort;
                }

                path = Path.GetDirectoryName(path);
            }

            return default(FileSortType?);
        }

        public void SetFileParentSettings(string path, FileSortType? sort)
        {
            _internalChildFileRepository.UpdateItem(new FolderAndArchiveChildFileDisplaySettingEntry()
            {
                Path = path,
                ChildItemDefaultSort = sort,
            });
        }


        public void DeleteUnderPath(string path)
        {
            _internalFolderAndArchiveRepository.DeleteUnderPath(path);
            _internalFileRepository.DeleteUnderPath(path);
            _internalChildFileRepository.DeleteUnderPath(path);
        }
    }
}
