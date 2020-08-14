using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml.Controls;

namespace TsubameViewer.Presentation.Views.StyleSelector
{
    public sealed class SearchAutoSuggestBoxGroupStyleSelector : GroupStyleSelector
    {
        public GroupStyle ItemsGroupStyle { get; set; }
        public GroupStyle SearchIndexGroupStyle { get; set; }


        protected override GroupStyle SelectGroupStyleCore(object group, uint level)
        {
            if (group is ViewModels.AutoSuggestBoxSearchIndexGroup)
            {
                return SearchIndexGroupStyle;
            }
            else
            {
                return ItemsGroupStyle;
            }

            //return base.SelectGroupStyleCore(group, level);
        }
    }
}
