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
            _FileDisplayMode = Read(FileDisplayMode.Line, nameof(FileDisplayMode));
        }

        private FileDisplayMode _FileDisplayMode;
        public FileDisplayMode FileDisplayMode
        {
            get { return _FileDisplayMode; }
            set { SetProperty(ref _FileDisplayMode, value); }
        }
    }
}
