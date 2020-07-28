using I18NPortable;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.Views.Converters;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class SettingsPageViewModel : ViewModelBase
    {
        private readonly FolderListingSettings _folderListingSettings;
        private readonly SourceFoldersRepository _SourceFoldersRepository;
        private readonly ImageViewerSettings _imageViewerPageSettings;
        private readonly ThumbnailManager _thumbnailManager;

        public SettingsGroupViewModel[] SettingGroups { get; }
        public SettingsGroupViewModel[] AdvancedSettingGroups { get; }

        public SettingsPageViewModel(
            FolderListingSettings folderListingSettings,
            SourceFoldersRepository SourceFoldersRepository,
            ImageViewerSettings imageViewerPageSettings,
            ThumbnailManager thumbnailManager
            )
        {
            _folderListingSettings = folderListingSettings;
            _SourceFoldersRepository = SourceFoldersRepository;
            _imageViewerPageSettings = imageViewerPageSettings;
            _thumbnailManager = thumbnailManager;

            _IsThumbnailDeleteButtonActive = new ReactiveProperty<bool>();
            _ThumbnailImagesCacheSizeText = new ReactivePropertySlim<string>();

            SettingGroups = new[]
            {
                new SettingsGroupViewModel
                {
                    Label = "SourceFoldersSettings".Translate(),
                    Items =
                    {
                        new StoredFoldersSettingItemViewModel(_SourceFoldersRepository),
                    }
                },
                new SettingsGroupViewModel
                {
                    Label = "ThumbnailImageSettings".Translate(),
                    Items =
                    {
                        new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsDisplayFolderThubnail".Translate(), _folderListingSettings, x => x.IsFolderThumbnailEnabled),
                        new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsDisplayArchiveFileThubnail".Translate(), _folderListingSettings, x => x.IsArchiveFileThumbnailEnabled),
                        new ToggleSwitchSettingItemViewModel<FolderListingSettings>("IsDisplayImageFileThubnail".Translate(), _folderListingSettings, x => x.IsImageFileThumbnailEnabled),
                        new UpdatableTextSettingItemViewModel("ThumbnailCacheSize".Translate(), _ThumbnailImagesCacheSizeText),
                        new ButtonSettingItemViewModel("DeleteThumbnailCache".Translate(), () => _ = DeleteThumnnailsAsync()),
                    }
                },
                new SettingsGroupViewModel
                {
                    Label = "ImageViewerSettings".Translate(),
                    Items =
                    {
                        new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsReverseImageFliping_MouseWheel".Translate(), _imageViewerPageSettings, x => x.IsReverseImageFliping_MouseWheel),
                        new ToggleSwitchSettingItemViewModel<ImageViewerSettings>("IsReverseImageFliping_Button".Translate(), _imageViewerPageSettings, x => x.IsReverseImageFliping_Button),
                    }
                },
            };

            AdvancedSettingGroups = new[]
            {
                new SettingsGroupViewModel
                {
                    /*
                    Label = "ThumbnailImageSettings".Translate(),
                    Items =
                    {
                        new ButtonSettingItemViewModel("DeleteThumbnailCache".Translate(), () => _ = DeleteThumnnailsAsync()),
                        new UpdatableTextSettingItemViewModel("ThumbnailCacheSize".Translate(), _ThumbnailImagesCacheSizeText)
                    }
                    */
                },
            };
        }

        private async Task DeleteThumnnailsAsync()
        {
            _ThumbnailImagesCacheSizeText.Value = string.Empty;

            _IsThumbnailDeleteButtonActive.Value = false;
            await _thumbnailManager.DeleteAllThumnnailsAsync();

            var folder = await ThumbnailManager.GetTempFolderAsync();
            var files = await folder.GetFilesAsync();
            ulong size = 0;
            foreach (var file in files)
            {
                var prop = await file.GetBasicPropertiesAsync();
                size += prop.Size;
            }

            _ThumbnailImagesCacheSizeText.Value = ToUserFiendlyFileSizeText(size) + "B";
        }

        ReactiveProperty<bool> _IsThumbnailDeleteButtonActive;
        ReactivePropertySlim<string> _ThumbnailImagesCacheSizeText;


        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _IsThumbnailDeleteButtonActive.Value = true;

            var folder = await ThumbnailManager.GetTempFolderAsync();
            var files = await folder.GetFilesAsync();
            ulong size = 0;
            foreach (var file in files)
            {
                var prop = await file.GetBasicPropertiesAsync();
                size += prop.Size;
            }
            _ThumbnailImagesCacheSizeText.Value = ToUserFiendlyFileSizeText(size) + "B";

            // base.OnNavigatedToAsync(parameters);
        }

        private static string ToUserFiendlyFileSizeText(ulong size)
        {
            var conv = new ToKMGTPEZYConverter();
            return (string)conv.Convert(size, typeof(string), null, CultureInfo.CurrentCulture.Name);
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
        private readonly SourceFoldersRepository _SourceFoldersRepository;

        public ObservableCollection<StoredFolderViewModel> Folders { get; }

        public StoredFoldersSettingItemViewModel(SourceFoldersRepository SourceFoldersRepository)
        {
            _SourceFoldersRepository = SourceFoldersRepository;
            Folders = new ObservableCollection<StoredFolderViewModel>();

            Init();
        }

        async void Init()
        {
            await foreach (var item in _SourceFoldersRepository.GetSourceFolders())
            {
                Folders.Add(new StoredFolderViewModel(_SourceFoldersRepository, this)
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
        private readonly SourceFoldersRepository _SourceFoldersRepository;
        private readonly StoredFoldersSettingItemViewModel _parentVM;

        public StoredFolderViewModel(SourceFoldersRepository SourceFoldersRepository, StoredFoldersSettingItemViewModel parentVM)
        {
            _SourceFoldersRepository = SourceFoldersRepository;
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
                    "ConfirmRemoveSourceFolderFromAppDescription".Translate(),
                    $"ConfirmRemoveSourceFolderFromAppWithFolderName".Translate(FolderName)
                    );

                dialog.Commands.Add(new UICommand("RemoveSourceFolderFromApp".Translate(), _ => 
                {
                    _SourceFoldersRepository.RemoveFolder(Token);
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

    public class UpdatableTextSettingItemViewModel : SettingItemViewModelBase
    {
        public string Label { get; }
        public IReadOnlyReactiveProperty<string> Text { get; }
        public UpdatableTextSettingItemViewModel(string label, IObservable<string> textObservable)
        {
            Label = label;
            Text = textObservable.ToReadOnlyReactivePropertySlim();
        }
    }

    public class ButtonSettingItemViewModel : SettingItemViewModelBase
    {
        private readonly Action _buttonAction;

        public ButtonSettingItemViewModel(string label, Action buttonAction)
        {
            Label = label;
            _buttonAction = buttonAction;
            ActionCommand = new ReactiveCommand();
            ActionCommand.Subscribe(_ => _buttonAction());
        }

        public ButtonSettingItemViewModel(string label, Action buttonAction, IObservable<bool> canExecuteObservable)
        {
            Label = label;
            _buttonAction = buttonAction;
            ActionCommand = canExecuteObservable.ToReactiveCommand();
            ActionCommand.Subscribe(_ => _buttonAction());
        }

        public string Label { get; }
        public ReactiveCommand ActionCommand { get; }
    }
}
