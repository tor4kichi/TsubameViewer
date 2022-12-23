using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Core.UseCases.Migrate
{
    public sealed class DropFileDisplaySettingsWhenSortTypeAreUpdateTimeDescThenTitleAsc : IMigrater
    {
        private readonly ILiteDatabase _liteDatabase;

        public DropFileDisplaySettingsWhenSortTypeAreUpdateTimeDescThenTitleAsc(
            ILiteDatabase liteDatabase
            )
        {
            _liteDatabase = liteDatabase;
        }


        public Version TargetVersion { get; } = new Version(1, 5, ushort.MaxValue);

        public void Migrate()
        {
            if (_liteDatabase.CollectionExists("FolderAndArchiveDisplaySettingEntry"))
            {
                var deleteCount = _liteDatabase.GetCollection("FolderAndArchiveDisplaySettingEntry")
                    .DeleteMany("$.Sort = 'UpdateTimeDescThenTitleAsc'");
            }

            if (_liteDatabase.CollectionExists("FolderAndArchiveChildFileDisplaySettingEntry"))
            {
                var deleteCount = _liteDatabase.GetCollection("FolderAndArchiveChildFileDisplaySettingEntry")
                    .DeleteMany("$.ChildItemDefaultSort = 'UpdateTimeDescThenTitleAsc'");
            }
        }
    }
}
