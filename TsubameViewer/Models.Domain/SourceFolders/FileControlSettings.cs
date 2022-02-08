using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.SourceFolders
{
    public sealed class FileControlSettings : FlagsRepositoryBase
    {
        public FileControlSettings()
        {
            _StorageItemDeleteDoNotDisplayNextTime = Read(false, nameof(StorageItemDeleteDoNotDisplayNextTime));
        }

        private bool _StorageItemDeleteDoNotDisplayNextTime;
        public bool StorageItemDeleteDoNotDisplayNextTime
        {
            get => _StorageItemDeleteDoNotDisplayNextTime;
            set => SetProperty(ref _StorageItemDeleteDoNotDisplayNextTime, value);
        }

    }
}
