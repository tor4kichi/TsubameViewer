using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    public sealed class ImageViewerSettings : FlagsRepositoryBase
    {
        private readonly SettingsPerPathRepository _settingsPerPathRepository;
        public ImageViewerSettings(ILiteDatabase liteDatabase)
        {
            _settingsPerPathRepository = new SettingsPerPathRepository(liteDatabase);

            _IsReverseImageFliping_MouseWheel = Read(false, nameof(IsReverseImageFliping_MouseWheel));
            _IsRightBindingView = Read(false, nameof(IsRightBindingView));
            _IsEnableDoubleView = Read(true, nameof(IsEnableDoubleView));
            _IsEnablePrefetch = Read(true, nameof(IsEnablePrefetch));
        }

        private bool _IsReverseImageFliping_MouseWheel;
        public bool IsReverseImageFliping_MouseWheel
        {
            get => _IsReverseImageFliping_MouseWheel;
            set => SetProperty(ref _IsReverseImageFliping_MouseWheel, value);
        }

        // 見開き表示時に左綴じとしてページを並べる
        private bool _IsRightBindingView;
        public bool IsRightBindingView
        {
            get => _IsRightBindingView;
            set => SetProperty(ref _IsRightBindingView, value);
        }

        // 見開き表示
        private bool _IsEnableDoubleView;
        public bool IsEnableDoubleView
        {
            get { return _IsEnableDoubleView; }
            set { SetProperty(ref _IsEnableDoubleView, value); }
        }


        private bool _IsEnablePrefetch;
        public bool IsEnablePrefetch
        {
            get { return _IsEnablePrefetch; }
            set { SetProperty(ref _IsEnablePrefetch, value); }
        }


        public (bool IsDoubleView, bool IsRightBinding, double DefaultZoom) GetViewerSettingsPerPath(string path)
        {
            var entry = _settingsPerPathRepository.FindByPath(path);
            if (entry != null)
            {
                return (entry.IsEnableDoubleView ?? this.IsEnableDoubleView, 
                    entry.IsRightBindingView ?? IsRightBindingView, 
                    entry.DefaultZoom ?? 1.0
                    );
            }
            else
            {
                return (this.IsEnableDoubleView, this.IsRightBindingView, 1.0);
            }
        }

        public void SetViewerSettingsPerPath(string path, bool? isDoubleView, bool? isRightBinding, double? defaultZoom)
        {
            _settingsPerPathRepository.UpdateItem(new SettingsPerPathEntry() 
            {
                Path = path,
                DefaultZoom = defaultZoom,
                IsEnableDoubleView = isDoubleView,
                IsRightBindingView = isRightBinding,
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
            public bool? IsRightBindingView { get; init; }
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
}
