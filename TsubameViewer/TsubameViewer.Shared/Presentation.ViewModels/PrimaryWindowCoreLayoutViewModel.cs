using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Attributes;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class PrimaryWindowCoreLayoutViewModel : BindableBase
    {
        private INavigationService _navigationService => _navigationServiceLazy.Value;
        private readonly Lazy<INavigationService> _navigationServiceLazy;


        public List<object> MenuItems { get;  }
        public PrimaryWindowCoreLayoutViewModel(
            [Dependency("PrimaryWindowNavigationService")] Lazy<INavigationService> navigationServiceLazy
            )
        {
            MenuItems = new List<object>
            {
                new MenuItemViewModel() { PageType = nameof(Views.HomePage) },
                new MenuSeparatorViewModel(),
                new MenuItemViewModel() { PageType = nameof(Views.HomePage) }
            };
            _navigationServiceLazy = navigationServiceLazy;
        }

        private bool _IsDisplayMenu = true;
        public bool IsDisplayMenu
        {
            get { return _IsDisplayMenu; }
            set { SetProperty(ref _IsDisplayMenu, value); }
        }


        DelegateCommand<object> _OpenMenuItemCommand;
        public DelegateCommand<object> OpenMenuItemCommand =>
            _OpenMenuItemCommand ??= new DelegateCommand<object>(item => 
            {
                if (item is MenuItemViewModel menuItem)
                {
                    _navigationService.NavigateAsync(menuItem.PageType);
                }
            });
    }


    public class MenuSeparatorViewModel
    {

    }

    public class MenuItemViewModel
    {
        public string PageType { get; set; }
        public string Parameters { get; set; }
    }

}
