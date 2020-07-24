using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.ImageView
{
    public sealed class ImageCollectionPageSettings : FlagsRepositoryBase
    {
        public ImageCollectionPageSettings()
        {
            _IsReverseMouseWheelBackForward = Read(false, nameof(IsReverseMouseWheelBackForward));
        }

        private bool _IsReverseMouseWheelBackForward;
        public bool IsReverseMouseWheelBackForward
        {
            get => _IsReverseMouseWheelBackForward;
            set => SetProperty(ref _IsReverseMouseWheelBackForward, value);
        }
    }
}
