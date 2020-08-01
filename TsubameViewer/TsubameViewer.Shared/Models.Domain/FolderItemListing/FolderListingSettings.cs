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
            _FileDisplayMode = Read(FileDisplayMode.Midium, nameof(FileDisplayMode));
            _IsImageFileThumbnailEnabled = Read(true, nameof(IsImageFileThumbnailEnabled));
            _IsArchiveFileThumbnailEnabled = Read(true, nameof(IsArchiveFileThumbnailEnabled));
            _IsFolderThumbnailEnabled = Read(true, nameof(IsFolderThumbnailEnabled));

            _IsForceEnableXYNavigation = Read(false, nameof(IsForceEnableXYNavigation));
        }

        private FileDisplayMode _FileDisplayMode;
        public FileDisplayMode FileDisplayMode
        {
            get { return _FileDisplayMode; }
            set { SetProperty(ref _FileDisplayMode, value); }
        }

        private bool _IsImageFileThumbnailEnabled;
        public bool IsImageFileThumbnailEnabled
        {
            get { return _IsImageFileThumbnailEnabled; }
            set { SetProperty(ref _IsImageFileThumbnailEnabled, value); }
        }

        private bool _IsArchiveFileThumbnailEnabled;
        public bool IsArchiveFileThumbnailEnabled
        {
            get { return _IsArchiveFileThumbnailEnabled; }
            set { SetProperty(ref _IsArchiveFileThumbnailEnabled, value); }
        }

        private bool _IsFolderThumbnailEnabled;
        public bool IsFolderThumbnailEnabled
        {
            get { return _IsFolderThumbnailEnabled; }
            set { SetProperty(ref _IsFolderThumbnailEnabled, value); }
        }

        private bool _IsForceEnableXYNavigation;
        public bool IsForceEnableXYNavigation
        {
            get { return _IsForceEnableXYNavigation; }
            set { SetProperty(ref _IsForceEnableXYNavigation, value); }
        }
    }
}
