using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models;

namespace TsubameViewer.ViewModels.PageNavigation
{
    public sealed class ThemeChangeRequestMessage : ValueChangedMessage<ApplicationTheme>
    {
        public ThemeChangeRequestMessage(ApplicationTheme value) : base(value)
        {
        }
    }
}
