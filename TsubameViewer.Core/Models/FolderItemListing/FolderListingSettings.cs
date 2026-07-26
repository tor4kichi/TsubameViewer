using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Infrastructure;
using Windows.Foundation;

namespace TsubameViewer.Core.Models.FolderItemListing;

public sealed class FolderListingSettings : FlagsRepositoryBase
{
    public const double DefaultFolderImageHeight = 262d;
    public const double DefaultFolderImageWidth = 200d;
    public const double DefaultFolderItemTitleHeight = 52d;
    
    public FolderListingSettings()
    {
        _FileDisplayMode = Read(FileDisplayMode.Midium, nameof(FileDisplayMode));
        _IsGenerateThumbnailEnabled = Read(true, nameof(IsGenerateThumbnailEnabled));
        _FolderItemThumbnailImageSize = Read(new Size(DefaultFolderImageWidth, DefaultFolderImageHeight), nameof(FolderItemThumbnailImageSize));
        _FolderItemTitleHeight = Read(DefaultFolderItemTitleHeight, nameof(FolderItemTitleHeight));
        _isInPageSearchWithMigemo = Read(true, nameof(IsInPageSearchWithMigemo));
        _thumbnailDecodeType = Read(ThumbnailDecodeMethod.Skia, nameof(ThumbnailDecodeType));
        _FolderItemThumbnailQuality = Read(1f, nameof(FolderItemThumbnailQuality));
    }

    private FileDisplayMode _FileDisplayMode;
    public FileDisplayMode FileDisplayMode
    {
        get { return _FileDisplayMode; }
        set { SetProperty(ref _FileDisplayMode, value); }
    }

    private bool _IsGenerateThumbnailEnabled;
    public bool IsGenerateThumbnailEnabled
    {
        get { return _IsGenerateThumbnailEnabled; }
        set { SetProperty(ref _IsGenerateThumbnailEnabled, value); }
    }

    private float _FolderItemThumbnailQuality;
    public float FolderItemThumbnailQuality
    {
        get => _FolderItemThumbnailQuality;
        set => SetProperty(ref _FolderItemThumbnailQuality, Math.Clamp(value, 0.5f, 1.5f));
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

    private bool _isInPageSearchWithMigemo;
    public bool IsInPageSearchWithMigemo
    {
        get => _isInPageSearchWithMigemo;
        set => SetProperty(ref _isInPageSearchWithMigemo, value);
    }

    private ThumbnailDecodeMethod _thumbnailDecodeType;
    public ThumbnailDecodeMethod ThumbnailDecodeType
    {
        get => _thumbnailDecodeType;
        set => SetProperty(ref _thumbnailDecodeType, value);
    }
}

public enum ThumbnailDecodeMethod
{
    Skia,
    WindowsImageCodec,
    Win2D,
}