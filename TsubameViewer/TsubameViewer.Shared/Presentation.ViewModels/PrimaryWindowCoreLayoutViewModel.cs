using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.UseCase.PageNavigation.Commands;
using TsubameViewer.Models.UseCase.SourceManagement.Commands;
using Unity.Attributes;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class PrimaryWindowCoreLayoutViewModel : BindableBase
    {
        private INavigationService _navigationService => _navigationServiceLazy.Value;
        private readonly Lazy<INavigationService> _navigationServiceLazy;


        public List<object> MenuItems { get;  }
        public PrimaryWindowCoreLayoutViewModel(
            [Dependency("PrimaryWindowNavigationService")] Lazy<INavigationService> navigationServiceLazy,
            SourceChoiceCommand sourceChoiceCommand,
            RefreshNavigationCommand refreshNavigationCommand,
            OpenPageCommand openPageCommand
            )
        {
            MenuItems = new List<object>
            {
                new MenuItemViewModel() { PageType = nameof(Views.StoredFoldersManagementPage) },
                //new MenuItemViewModel() { PageType = nameof(Views.CollectionPage) },
            };
            _navigationServiceLazy = navigationServiceLazy;
            SourceChoiceCommand = sourceChoiceCommand;
            RefreshNavigationCommand = refreshNavigationCommand;
            OpenPageCommand = openPageCommand;
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

        public SourceChoiceCommand SourceChoiceCommand { get; }
        public RefreshNavigationCommand RefreshNavigationCommand { get; }
        public OpenPageCommand OpenPageCommand { get; }
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
