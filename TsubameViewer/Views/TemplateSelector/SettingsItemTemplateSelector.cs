﻿using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TsubameViewer.Views.TemplateSelector
{
    public sealed class SettingsItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ToggleSwitchSettingItem { get; set; } 
        public DataTemplate StoredFoldersSettingItem { get; set; }
        public DataTemplate UpdatableTextSettingItem { get; set; }
        public DataTemplate ButtonSettingItem { get; set; }
        public DataTemplate ThemeSelectSettingItem { get; set; }
        public DataTemplate LocaleSelectSettingItem { get; set; }
        public DataTemplate NumericTextBoxSettingItem { get; set; }


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
                NumberBoxSettingItemViewModel _ => NumericTextBoxSettingItem,
                _ => throw new NotSupportedException(),
            };
        }
    }
}
