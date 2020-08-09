using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    public sealed class ImageViewerSettings : FlagsRepositoryBase
    {
        public ImageViewerSettings()
        {
            _IsReverseImageFliping_MouseWheel = Read(false, nameof(IsReverseImageFliping_MouseWheel));
            _IsReverseImageFliping_Button = Read(false, nameof(IsReverseImageFliping_Button));
            _IsLeftBindingView = Read(false, nameof(IsLeftBindingView));
        }

        private bool _IsReverseImageFliping_MouseWheel;
        public bool IsReverseImageFliping_MouseWheel
        {
            get => _IsReverseImageFliping_MouseWheel;
            set => SetProperty(ref _IsReverseImageFliping_MouseWheel, value);
        }

        private bool _IsReverseImageFliping_Button;
        public bool IsReverseImageFliping_Button
        {
            get => _IsReverseImageFliping_Button;
            set => SetProperty(ref _IsReverseImageFliping_Button, value);
        }

        private bool _IsLeftBindingView;
        public bool IsLeftBindingView
        {
            get => _IsLeftBindingView;
            set => SetProperty(ref _IsLeftBindingView, value);
        }
    }
}
