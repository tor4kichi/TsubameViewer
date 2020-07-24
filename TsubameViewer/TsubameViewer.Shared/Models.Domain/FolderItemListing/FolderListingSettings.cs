using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.FolderItemListing
{
    public sealed class FolderListingSettings : FlagsRepositoryBase
    {
        public FolderListingSettings()
        {
            _FolderDisplayMode = Read(FolderDisplayMode.MangaCover, nameof(FolderDisplayMode));
        }

        private FolderDisplayMode _FolderDisplayMode;
        public FolderDisplayMode FolderDisplayMode
        {
            get { return _FolderDisplayMode; }
            set { SetProperty(ref _FolderDisplayMode, value); }
        }
    }
}
