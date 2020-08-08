using Prism.Events;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    public sealed class ThemeChangeRequestEvent : PubSubEvent<ApplicationTheme>
    {

    }
}
