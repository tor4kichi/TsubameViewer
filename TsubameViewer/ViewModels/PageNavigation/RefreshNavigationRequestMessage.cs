﻿using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.ViewModels.PageNavigation
{
    public sealed class RefreshNavigationRequestMessage : ValueChangedMessage<int>
    {
        public RefreshNavigationRequestMessage() : base(0)
        {
        }
    }
}
