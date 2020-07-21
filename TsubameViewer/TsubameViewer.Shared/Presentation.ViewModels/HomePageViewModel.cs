using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Presentation.ViewModels.Commands;
using Windows.Storage;
#if WINDOWS_UWP
using Windows.Storage.AccessCache;
#endif

namespace TsubameViewer.Presentation.ViewModels
{
    // TODO: アクセス履歴対応

    public sealed class HomePageViewModel : ViewModelBase
    {
        public ObservableCollection<StorageItemViewModel> Folders { get; }

        bool _foldersInitialized = false;
        public HomePageViewModel(
            OpenFolderItemCommand openFolderItemCommand
            )
        {
            Folders = new ObservableCollection<StorageItemViewModel>();
            OpenFolderItemCommand = openFolderItemCommand;
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            if (!_foldersInitialized)
            {
                _foldersInitialized = true;

                await foreach (var item in GetStoredFolderItems())
                {
                    Folders.Add(new StorageItemViewModel(item.item, item.token));
                }
            }

            await base.OnNavigatedToAsync(parameters);
        }



        #region Commands

        private DelegateCommand _FolderChoiseCommand;
        public DelegateCommand FolderChoiseCommand =>
            _FolderChoiseCommand ??= new DelegateCommand(async () =>
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
                picker.CommitButtonText = "選択";
                picker.FileTypeFilter.Add("*");
                var seletedFolder = await picker.PickSingleFolderAsync();

                if (seletedFolder == null) { return; }

                var token = Guid.NewGuid().ToString();
                StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, seletedFolder);

                Folders.Add(new StorageItemViewModel(seletedFolder, token));
            });

        public OpenFolderItemCommand OpenFolderItemCommand { get; }




        #endregion


        private async IAsyncEnumerable<(IStorageItem item, string token)> GetStoredFolderItems([EnumeratorCancellation] CancellationToken ct = default)
        {
#if WINDOWS_UWP
            var myItems = StorageApplicationPermissions.FutureAccessList.Entries;
            foreach (var item in myItems)
            {
                ct.ThrowIfCancellationRequested();
                yield return (await StorageApplicationPermissions.FutureAccessList.GetItemAsync(item.Token), item.Token);
            }
#else
            // TODO: GetStoredFolderItems() UWP以外での対応
#endif
        }
    }
}
