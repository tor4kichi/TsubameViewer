using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace TsubameViewer.Presentation.ViewModels.SourceFolders
{
    public sealed class RequireInstallImageCodecExtensionMessage : ValueChangedMessage<string>
    {
        public RequireInstallImageCodecExtensionMessage(string fileType) : base(fileType)
        {
        }
    }
}
