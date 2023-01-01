using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Views.UINavigation;

namespace TsubameViewer.Views.Helpers
{
    public sealed class FocusHelper
    {
        private readonly ApplicationSettings _applicationSettings;

        public FocusHelper(ApplicationSettings applicationSettings)
        {
            _applicationSettings = applicationSettings;
        }

        public bool IsRequireSetFocus()
        {
            return (Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.DeviceFamily == "Windows.Xbox"
                || _applicationSettings.IsUINavigationFocusAssistanceEnabled)
                && UINavigationManager.NowControllerConnected
                ;
        }
    }
}
