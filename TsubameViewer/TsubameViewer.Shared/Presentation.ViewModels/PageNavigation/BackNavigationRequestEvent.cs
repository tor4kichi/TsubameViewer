﻿using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    public sealed class BackNavigationRequestMessage : ValueChangedMessage<int>
    {
        public BackNavigationRequestMessage() : base(0) { }
    }
}
