﻿using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Presentation.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TsubameViewer.Presentation.Views.TemplateSelector
{
    public sealed class SettingsItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ToggleSwitchSettingItem { get; set; } 
        public DataTemplate StoredFoldersSettingItem { get; set; }

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
                _ => throw new NotSupportedException(),
            };
        }
    }
}