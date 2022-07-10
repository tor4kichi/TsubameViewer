using I18NPortable;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TsubameViewer.Presentation.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class FolderOrArchiveRestructurePage : Page
    {
        public FolderOrArchiveRestructurePage()
        {
            this.InitializeComponent();

            DataContext = _vm = Ioc.Default.GetRequiredService<FolderOrArchiveRestructurePageViewModel>();
        }

        private readonly FolderOrArchiveRestructurePageViewModel _vm;

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_nowSelectAllWithSearch) { return; }

            var dataGrid = sender as DataGrid;

            foreach (var removed in e.RemovedItems.Cast<IPathRestructure>())
            {
                _vm.SelectedItems.Remove(removed);
            }
            foreach (var added in e.AddedItems.Cast<IPathRestructure>())
            {
                _vm.SelectedItems.Add(added);
            }

            if (e.AddedItems.Any())
            {
                dataGrid.ScrollIntoView(e.AddedItems.First(), DataGridColumn_RelativePath);
            }
        }

        private void ToggleSelectAll()
        {
            if (_vm.SelectedItems.Any())
            {
                PathsDataGrid.SelectedItems.Clear();
                _vm.SelectedItems.Clear();
            }
            else
            {
                foreach (var item in _vm.Items)
                {
                    PathsDataGrid.SelectedItems.Add(item);
                    _vm.SelectedItems.Add(item as IPathRestructure);
                }
            }
        }


        bool _nowSelectAllWithSearch = false;
        private void SelectAllWithSearch()
        {
            _nowSelectAllWithSearch = true;

            if (string.IsNullOrWhiteSpace(_vm.SearchText))
            {
                ToggleSelectAll();
                return;
            }

            try
            {
                var searchItems = _vm.SearchAll(_vm.SearchText);
                PathsDataGrid.SelectedItems.Clear();
                _vm.SelectedItems.Clear();
                foreach (var item in searchItems)
                {
                    PathsDataGrid.SelectedItems.Add(item);
                    _vm.SelectedItems.Add(item);
                }

                if (searchItems.Any())
                {
                    PathsDataGrid.ScrollIntoView(searchItems.LastOrDefault(), DataGridColumn_RelativePath);
                }
            }
            finally
            {
                _nowSelectAllWithSearch = false;
            }
        }


        private async void SaveOverwrite()
        {
            var dialog = new MessageDialog("RestructurePage_OverwriteSave_Confirm".Translate(_vm.SourceStorageItem.Name));

            dialog.Commands.Add(new UICommand("RestructurePage_OverwriteSave".Translate(), (s) => { _vm.OverwriteSaveCommand.Execute(null); }));
            dialog.Commands.Add(new UICommand("Cancel".Translate(), (s) => { }));
            dialog.DefaultCommandIndex = 1;
            dialog.CancelCommandIndex = 1;
            var result = await dialog.ShowAsync();

        }
    }

    public sealed class EnumValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return Enum.Parse(targetType, value as string);
        }

    }
}
