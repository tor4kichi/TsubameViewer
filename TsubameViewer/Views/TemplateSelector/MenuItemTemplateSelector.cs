using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TsubameViewer.Views.TemplateSelector
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
