using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    public sealed class ThemeChangeRequestMessage : ValueChangedMessage<ApplicationTheme>
    {
        public ThemeChangeRequestMessage(ApplicationTheme value) : base(value)
        {
        }
    }
}
