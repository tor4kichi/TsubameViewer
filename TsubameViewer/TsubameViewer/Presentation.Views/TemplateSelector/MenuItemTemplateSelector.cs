using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TsubameViewer.Presentation.Views.TemplateSelector
{
    public sealed class MenuItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate MenuSeparator { get; set; }
        public DataTemplate MenuItem { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return SelectTemplateCore(item, null);
        }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return item switch
            {
                MenuItemViewModel _ => MenuItem,
                MenuSeparatorViewModel _ => MenuSeparator,
                _ => throw new NotSupportedException(),
            };
        }
    }
}
