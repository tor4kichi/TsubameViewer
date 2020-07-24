using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace TsubameViewer.Models.UseCase.PageNavigation
{
    public sealed class StorageItemViewModel : BindableBase
    {
        public static async ValueTask<INavigationParameters> CreatePageParameterAsync(StorageItemViewModel vm)
        {
            var item = await StorageApplicationPermissions.FutureAccessList.GetItemAsync(vm.Token);
            if (item is IStorageFolder folder)
            {
                var path = GetSubtractPath(folder, vm.Item);
                return new NavigationParameters(("token", vm.Token), ("path", Uri.EscapeDataString(path)));
            }
            else if (item is IStorageFile file)
            {
                return new NavigationParameters(("token", vm.Token), ("path", Uri.EscapeDataString(file.Name)));
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public static async Task<string> GetRawSubtractPath(StorageItemViewModel vm)
        {
            var item = await StorageApplicationPermissions.FutureAccessList.GetItemAsync(vm.Token);
            if (item is IStorageFolder folder)
            {
                return GetSubtractPath(folder, vm.Item);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private static string GetSubtractPath(IStorageFolder lt, IStorageItem rt)
        {
            if (!rt.Path.StartsWith(lt.Path))
            {
                throw new ArgumentException("差分パスの取得には親子関係にあるフォルダとアイテムが必要です。");
            }

            return rt.Path.Substring(lt.Path.Length);
        }



        public StorageItemViewModel() 
        {
#if WINDOWS_UWP
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                // Load design-time books.
                _Name = "テスト";
            }
#endif
        }

        public StorageItemViewModel(IStorageItem item, string token)
        {
            Item = item;
            Token = token;
            _Type = item switch
            {
                StorageFile _ => StorageItemTypes.File,
                StorageFolder _ => StorageItemTypes.Folder,
                _ => StorageItemTypes.None
            };

            _Name = Item.Name;
            _Path = Item.Path;
        }

        public IStorageItem Item { get; private set; }
        public string Token { get; private set; }

        private string _Name;
        public string Name
        {
            get { return _Name; }
            set { SetProperty(ref _Name, value); }
        }

        private string _Path;
        public string Path
        {
            get { return _Path; }
            set { SetProperty(ref _Path, value); }
        }


        private StorageItemTypes _Type;
        public StorageItemTypes Type
        {
            get { return _Type; }
            private set { SetProperty(ref _Type, value); }
        }



        public void Setup(IStorageItem item, string token)
        {
            Item = item;
            Token = token;
            Type = item switch
            {
                StorageFile _ => StorageItemTypes.File,
                StorageFolder _ => StorageItemTypes.Folder,
                _ => StorageItemTypes.None
            };

            Name = Item.Name;
            Path = Item.Path;
        }
    }
}
