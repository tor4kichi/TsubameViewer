using I18NPortable;
using Prism.Mvvm;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Text;
using TsubameViewer.Models.Domain.FolderItemListing;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class SettingsPageViewModel : ViewModelBase
    {
        private readonly FolderListingSettings _folderListingSettings;

        public SettingsGroupViewModel[] SettingGroups { get; }

        public SettingsPageViewModel(FolderListingSettings folderListingSettings)
        {
            _folderListingSettings = folderListingSettings;
            SettingGroups = new[]
            {
                new SettingsGroupViewModel
                {
                    Label = "ThumbnailImageSettings".Translate(),
                    Items =
                    {
                        new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsDisplayImageFileThubnail".Translate(), _folderListingSettings, x => x.IsImageFileThumbnailEnabled),
                        new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsDisplayArchiveFileThubnail".Translate(), _folderListingSettings, x => x.IsArchiveFileThumbnailEnabled),
                        new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsDisplayFolderThubnail".Translate(), _folderListingSettings, x => x.IsFolderThumbnailEnabled),
                    }
                }
            };
        }

    }

    public abstract class SettingItemViewModelBase
    {

    }
    public sealed class SettingsGroupViewModel 
    {
        public string Label { get; set; }
        public List<SettingItemViewModelBase> Items { get; set; } = new List<SettingItemViewModelBase>();
    }

    public interface IToggleSwitchSettingItemViewModel
    {
        string Label { get; }
        ReactiveProperty<bool> ValueContainer { get; }
    }

    public class ToggleSwitchSettingItemViewModel<T> : SettingItemViewModelBase, IToggleSwitchSettingItemViewModel 
        where T : INotifyPropertyChanged
    {
        public ToggleSwitchSettingItemViewModel(string label, T value, Expression<Func<T, bool>> expression)
        {
            ValueContainer = value.ToReactivePropertyAsSynchronized(expression);
            Label = label;
        }

        public ReactiveProperty<bool> ValueContainer { get; }
        public string Label { get; }
    }
}
