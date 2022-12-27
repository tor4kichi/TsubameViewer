using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TsubameViewer.Views.TemplateSelector
{
    public sealed class FolderItemsGroupTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Folders { get; set; }
        public DataTemplate Files { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return SelectTemplateCore(item, null);
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return item switch
            {
                FolderFolderItemsGroup _ => Folders,
                FileFolderItemsGroup _ => Files,
                _ => throw new NotSupportedException(),
            };
        }
    }
}
