using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.FolderItemListing;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TsubameViewer.Presentation.Views.TemplateSelector
{
    public sealed class FileDisplayModeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Line { get; set; }
        public DataTemplate Small { get; set; }
        public DataTemplate Midium { get; set; }
        public DataTemplate Large { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return SelectTemplateCore(item, null);
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is FileDisplayMode fileDisplayMode)
            {
                return fileDisplayMode switch
                {
                    FileDisplayMode.Line => Line,
                    FileDisplayMode.Small => Small,
                    FileDisplayMode.Midium => Midium,
                    FileDisplayMode.Large => Large,
                    _ => throw new NotSupportedException(),
                };
            }
            return base.SelectTemplateCore(item, container);
        }
    }
}
