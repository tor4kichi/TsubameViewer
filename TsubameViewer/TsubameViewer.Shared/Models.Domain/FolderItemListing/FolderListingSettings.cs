using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;
using Windows.Foundation;

namespace TsubameViewer.Models.Domain.FolderItemListing
{
    public sealed class FolderListingSettings : FlagsRepositoryBase
    {
        public const double DefaultFolderImageHeight = 262d;
        public const double DefaultFolderImageWidth = 200d;
        public const double DefaultFolderItemTitleHeight = 52d;

        public const string DefaultThumbnailPriorityTitleRegexString = "cover|Cover|COVER|カバー";
        public FolderListingSettings()
        {
            _FileDisplayMode = Read(FileDisplayMode.Midium, nameof(FileDisplayMode));
            _IsImageFileGenerateThumbnailEnabled = Read(true, nameof(IsImageFileGenerateThumbnailEnabled));
            _IsFolderGenerateThumbnailEnabled = Read(true, nameof(IsFolderGenerateThumbnailEnabled));
            _IsArchiveFileGenerateThumbnailEnabled = Read(true, nameof(IsArchiveFileGenerateThumbnailEnabled));
            _IsArchiveEntryGenerateThumbnailEnabled = Read(false, nameof(IsArchiveEntryGenerateThumbnailEnabled));

            _IsForceEnableXYNavigation = Read(false, nameof(IsForceEnableXYNavigation));
            
            _FolderItemThumbnailImageSize = Read(new Size(DefaultFolderImageWidth, DefaultFolderImageHeight), nameof(FolderItemThumbnailImageSize));
            _FolderItemTitleHeight = Read(DefaultFolderItemTitleHeight, nameof(FolderItemTitleHeight));
            
            _ThumbnailPriorityTitleRegexString = Read(DefaultThumbnailPriorityTitleRegexString, nameof(ThumbnailPriorityTitleRegexString));
        }

        private FileDisplayMode _FileDisplayMode;
        public FileDisplayMode FileDisplayMode
        {
            get { return _FileDisplayMode; }
            set { SetProperty(ref _FileDisplayMode, value); }
        }

        private bool _IsImageFileGenerateThumbnailEnabled;
        public bool IsImageFileGenerateThumbnailEnabled
        {
            get { return _IsImageFileGenerateThumbnailEnabled; }
            set { SetProperty(ref _IsImageFileGenerateThumbnailEnabled, value); }
        }

        private bool _IsFolderGenerateThumbnailEnabled;
        public bool IsFolderGenerateThumbnailEnabled
        {
            get { return _IsFolderGenerateThumbnailEnabled; }
            set { SetProperty(ref _IsFolderGenerateThumbnailEnabled, value); }
        }

        private bool _IsArchiveFileGenerateThumbnailEnabled;
        public bool IsArchiveFileGenerateThumbnailEnabled
        {
            get { return _IsArchiveFileGenerateThumbnailEnabled; }
            set { SetProperty(ref _IsArchiveFileGenerateThumbnailEnabled, value); }
        }

        private bool _IsArchiveEntryGenerateThumbnailEnabled;
        public bool IsArchiveEntryGenerateThumbnailEnabled
        {
            get { return _IsArchiveEntryGenerateThumbnailEnabled; }
            set { SetProperty(ref _IsArchiveEntryGenerateThumbnailEnabled, value); }
        }

        private bool _IsForceEnableXYNavigation;
        public bool IsForceEnableXYNavigation
        {
            get { return _IsForceEnableXYNavigation; }
            set { SetProperty(ref _IsForceEnableXYNavigation, value); }
        }

        private Size _FolderItemThumbnailImageSize;
        public Size FolderItemThumbnailImageSize
        {
            get { return _FolderItemThumbnailImageSize; }
            set { SetProperty(ref _FolderItemThumbnailImageSize, value); }
        }

        private double _FolderItemTitleHeight;
        public double FolderItemTitleHeight
        {
            get { return _FolderItemTitleHeight; }
            set { SetProperty(ref _FolderItemTitleHeight, value); }
        }


        private string _ThumbnailPriorityTitleRegexString;
        public string ThumbnailPriorityTitleRegexString
        {
            get { return _ThumbnailPriorityTitleRegexString; }
            set { SetProperty(ref _ThumbnailPriorityTitleRegexString, value); }
        }
    }
}
