using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class PrimaryWindowCoreLayoutViewModel : BindableBase
    {
        private readonly IEventAggregator _eventAggregator;

        public PrimaryWindowCoreLayoutViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }
    }
}
