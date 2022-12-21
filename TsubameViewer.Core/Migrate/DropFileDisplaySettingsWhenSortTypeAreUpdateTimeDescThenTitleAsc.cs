﻿using LiteDB;
using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models.FolderItemListing;
using Windows.ApplicationModel;
using static TsubameViewer.Core.Models.FolderItemListing.DisplaySettingsByPathRepository;

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


        PackageVersion _targetVersion = new PackageVersion() { Major = 1, Minor = 5, Build = ushort.MaxValue };

        public bool IsRequireMigrate => SystemInformation.Instance.PreviousVersionInstalled.IsSmallerThen(_targetVersion)
            ;

        public void Migrate()
        {
            if (_liteDatabase.CollectionExists(nameof(FolderAndArchiveDisplaySettingEntry)))
            {
                var deleteCount = _liteDatabase.GetCollection<FolderAndArchiveDisplaySettingEntry>()
                    .DeleteMany("$.Sort = 'UpdateTimeDescThenTitleAsc'");
            }

            if (_liteDatabase.CollectionExists(nameof(FolderAndArchiveChildFileDisplaySettingEntry)))
            {
                var deleteCount = _liteDatabase.GetCollection<FolderAndArchiveChildFileDisplaySettingEntry>()
                    .DeleteMany("$.ChildItemDefaultSort = 'UpdateTimeDescThenTitleAsc'");
            }
        }
    }
}
