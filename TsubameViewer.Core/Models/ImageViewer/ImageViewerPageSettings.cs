using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Infrastructure;

namespace TsubameViewer.Core.Models.ImageViewer;

public sealed class ImageViewerSettings : FlagsRepositoryBase
{
    private readonly SettingsPerPathRepository _settingsPerPathRepository;
    public ImageViewerSettings(ILiteDatabase liteDatabase)
    {
        _settingsPerPathRepository = new SettingsPerPathRepository(liteDatabase);

        _IsReverseImageFliping_MouseWheel = Read(false, nameof(IsReverseImageFliping_MouseWheel));
        _IsLeftBindingView = Read(false, nameof(IsLeftBindingView));
        _IsEnableDoubleView = Read(true, nameof(IsEnableDoubleView));
        _IsKeepSingleViewOnFirstPage = Read(true, nameof(IsKeepSingleViewOnFirstPage));
        _IsEnablePrefetch = Read(true, nameof(IsEnablePrefetch));
        _PdfImageThresholdWidth = Read(1200u, nameof(PdfImageThresholdWidth));
        _PdfImageAlternateWidth = Read(1600u, nameof(PdfImageAlternateWidth));
        _PdfImageThresholdHeight = Read(1200u, nameof(PdfImageThresholdHeight));
        _PdfImageAlternateHeight = Read(2400u, nameof(PdfImageAlternateHeight));            
    }

    private bool _IsReverseImageFliping_MouseWheel;
    public bool IsReverseImageFliping_MouseWheel
    {
        get => _IsReverseImageFliping_MouseWheel;
        set => SetProperty(ref _IsReverseImageFliping_MouseWheel, value);
    }

    // 見開き表示時に左綴じとしてページを並べる
    private bool _IsLeftBindingView;
    public bool IsLeftBindingView
    {
        get => _IsLeftBindingView;
        set => SetProperty(ref _IsLeftBindingView, value);
    }

    // 見開き表示
    private bool _IsEnableDoubleView;
    public bool IsEnableDoubleView
    {
        get { return _IsEnableDoubleView; }
        set { SetProperty(ref _IsEnableDoubleView, value); }
    }

    // 最初のページは常に見開き表示をOFFにする
    private bool _IsKeepSingleViewOnFirstPage;
    public bool IsKeepSingleViewOnFirstPage
    {
        get { return _IsKeepSingleViewOnFirstPage; }
        set { SetProperty(ref _IsKeepSingleViewOnFirstPage, value); }
    }

    private bool _IsEnablePrefetch;
    public bool IsEnablePrefetch
    {
        get { return _IsEnablePrefetch; }
        set { SetProperty(ref _IsEnablePrefetch, value); }
    }


    // 代替サイズ指定
    // PDFのメタデータ指定における高さが（800ptあるのに）350ptなどと設定されているケースに手動対応する
    
    private uint _PdfImageThresholdWidth;
    public uint PdfImageThresholdWidth
    {
        get => _PdfImageThresholdWidth;
        set => SetProperty(ref _PdfImageThresholdWidth, value);
    }

    private uint _PdfImageAlternateWidth;
    public uint PdfImageAlternateWidth
    {
        get => _PdfImageAlternateWidth;
        set => SetProperty(ref _PdfImageAlternateWidth, value);
    }


    private uint _PdfImageThresholdHeight;
    public uint PdfImageThresholdHeight
    {
        get => _PdfImageThresholdHeight;
        set => SetProperty(ref _PdfImageThresholdHeight, value);
    }

    private uint _PdfImageAlternateHeight;
    public uint PdfImageAlternateHeight
    {
        get => _PdfImageAlternateHeight;
        set => SetProperty(ref _PdfImageAlternateHeight, value);
    }


    public (bool IsDoubleView, bool IsLeftBinding, double DefaultZoom) GetViewerSettingsPerPath(string path)
    {
        var entry = _settingsPerPathRepository.FindByPath(path);
        if (entry != null)
        {
            return (entry.IsEnableDoubleView ?? this.IsEnableDoubleView, 
                entry.IsLeftBindingView ?? IsLeftBindingView, 
                entry.DefaultZoom ?? 1.0
                );
        }
        else
        {
            return (this.IsEnableDoubleView, this.IsLeftBindingView, 1.0);
        }
    }

    public void SetViewerSettingsPerPath(string path, bool? isDoubleView, bool? isLeftBinding, double? defaultZoom)
    {
        _settingsPerPathRepository.UpdateItem(new SettingsPerPathEntry() 
        {
            Path = path,
            DefaultZoom = defaultZoom,
            IsEnableDoubleView = isDoubleView,
            IsLeftBindingView = isLeftBinding,
        });
    }

    public void ClearViewerSettingsPerPath(string path)
    {
        _settingsPerPathRepository.DeleteItem(path);
    }

    private record SettingsPerPathEntry
    {
        [BsonId]
        public string Path { get; init; }

        public bool? IsEnableDoubleView { get; init; }
        public bool? IsLeftBindingView { get; init; }
        public double? DefaultZoom { get; init; }
    }

    private sealed class SettingsPerPathRepository : LiteDBServiceBase<SettingsPerPathEntry>
    {
        public SettingsPerPathRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
        {
        }

        public SettingsPerPathEntry FindByPath(string path)
        {
            return _collection.FindById(path);
        }
    }
}
