using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Presentation.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TsubameViewer.Presentation.Views.TemplateSelector
{
    public sealed class SettingsItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ToggleSwitchSettingItem { get; set; } 
        public DataTemplate StoredFoldersSettingItem { get; set; }
        public DataTemplate UpdatableTextSettingItem { get; set; }
        public DataTemplate ButtonSettingItem { get; set; }
        public DataTemplate ThemeSelectSettingItem { get; set; }
        public DataTemplate LocaleSelectSettingItem { get; set; }


        protected override DataTemplate SelectTemplateCore(object item)
        {
            return SelectTemplateCore(item, null);
        }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return item switch
            {
                StoredFoldersSettingItemViewModel _ => StoredFoldersSettingItem,
                IToggleSwitchSettingItemViewModel _ => ToggleSwitchSettingItem,
                UpdatableTextSettingItemViewModel _ => UpdatableTextSettingItem,
                ButtonSettingItemViewModel _ => ButtonSettingItem,
                ThemeSelectSettingItemViewModel _ => ThemeSelectSettingItem,
                LocaleSelectSettingItemViewModel _ => LocaleSelectSettingItem,
                _ => throw new NotSupportedException(),
            };
        }
    }
}
