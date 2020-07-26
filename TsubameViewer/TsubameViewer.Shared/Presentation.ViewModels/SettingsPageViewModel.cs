using I18NPortable;
using Prism.Commands;
using Prism.Mvvm;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Text;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.SourceManagement;
using Windows.Storage;
using Windows.UI.Popups;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class SettingsPageViewModel : ViewModelBase
    {
        private readonly FolderListingSettings _folderListingSettings;
        private readonly StoredFoldersRepository _storedFoldersRepository;

        public SettingsGroupViewModel[] SettingGroups { get; }

        public SettingsPageViewModel(
            FolderListingSettings folderListingSettings,
            StoredFoldersRepository storedFoldersRepository
            )
        {
            _folderListingSettings = folderListingSettings;
            _storedFoldersRepository = storedFoldersRepository;
            SettingGroups = new[]
            {
                new SettingsGroupViewModel
                {
                    Label = "StoredFolderManagementSettings".Translate(),
                    Items =
                    {
                        new StoredFoldersSettingItemViewModel(_storedFoldersRepository),
                    }
                },
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


    public sealed class StoredFoldersSettingItemViewModel :  SettingItemViewModelBase
    {
        private readonly StoredFoldersRepository _storedFoldersRepository;

        public ObservableCollection<StoredFolderViewModel> Folders { get; }

        public StoredFoldersSettingItemViewModel(StoredFoldersRepository storedFoldersRepository)
        {
            _storedFoldersRepository = storedFoldersRepository;
            Folders = new ObservableCollection<StoredFolderViewModel>();

            Init();
        }

        async void Init()
        {
            await foreach (var item in _storedFoldersRepository.GetStoredFolderItems())
            {
                Folders.Add(new StoredFolderViewModel(_storedFoldersRepository, this)
                {
                    Item = item.item,
                    FolderName = item.item.Name,
                    Path = item.item.Path,
                    Token = item.token,
                });
            }
        }

        internal void RemoveItem(StoredFolderViewModel childVM)
        {
            Folders.Remove(childVM);
        }
    }

    public class StoredFolderViewModel
    {
        private readonly StoredFoldersRepository _storedFoldersRepository;
        private readonly StoredFoldersSettingItemViewModel _parentVM;

        public StoredFolderViewModel(StoredFoldersRepository storedFoldersRepository, StoredFoldersSettingItemViewModel parentVM)
        {
            _storedFoldersRepository = storedFoldersRepository;
            _parentVM = parentVM;
        }

        public string FolderName { get; set; }
        public string Path { get; set; }
        public string Token { get; set; }
        public IStorageItem Item { get; set; }


        private DelegateCommand _DeleteStoredFolderCommand;
        public DelegateCommand DeleteStoredFolderCommand =>
            _DeleteStoredFolderCommand ??= new DelegateCommand(async () => 
            {
                var dialog = new MessageDialog(
                    "ConfirmRemoveStoredFolderFromAppDescription".Translate(),
                    $"ConfirmRemoveStoredFolderFromAppWithFolderName".Translate(FolderName)
                    );

                dialog.Commands.Add(new UICommand("RemoveStoredFolderFromApp".Translate(), _ => 
                {
                    _storedFoldersRepository.RemoveFolder(Token);
                    _parentVM.RemoveItem(this);
                }));

                dialog.Commands.Add(new UICommand("Cancel".Translate()));
                dialog.CancelCommandIndex = 1;
                dialog.DefaultCommandIndex = 0;

                var result = await dialog.ShowAsync();
            });
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
