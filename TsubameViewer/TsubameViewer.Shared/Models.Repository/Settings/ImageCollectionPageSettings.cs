using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Models.Repository.Settings
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
