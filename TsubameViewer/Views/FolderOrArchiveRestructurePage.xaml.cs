using CommunityToolkit.Mvvm.DependencyInjection;
using I18NPortable;
using Microsoft.Toolkit.Uwp.UI.Controls;
using R3;
using System;
using System.Linq;
using System.Threading.Tasks;
using TsubameViewer.ViewModels;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

#nullable enable
namespace TsubameViewer.Views;

public sealed partial class FolderOrArchiveRestructurePage : Page, ITitlebarContentAware
{
    public DataTemplate? GetContent()
    {
        return TitlebarContent;
    }

    public R3.Observable<string> ObserveTitleChanged()
    {
        return R3.Observable.Return("FolderOrArchiveRestructure".Translate());
    }

    public FolderOrArchiveRestructurePage()
    {
        this.InitializeComponent();

        DataContext = _vm = Ioc.Default.GetRequiredService<FolderOrArchiveRestructurePageViewModel>();
    }

    readonly FolderOrArchiveRestructurePageViewModel _vm;

    void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_nowSelectAllWithSearch) { return; }
        if (sender is not  DataGrid dataGrid) { return; }

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

    void ToggleSelectAll()
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
    void SelectAllWithSearch()
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


    void SaveOverwrite()
    {
        d().FireAndForgetSafe();
        async Task d ()
        {
            var dialog = new MessageDialog("RestructurePage_OverwriteSave_Confirm".Translate(_vm.SourceStorageItem.Name));

            dialog.Commands.Add(new UICommand("RestructurePage_OverwriteSave".Translate(), (s) => { _vm.OverwriteSaveCommand.Execute(null); }));
            dialog.Commands.Add(new UICommand("Cancel".Translate(), (s) => { }));
            dialog.DefaultCommandIndex = 1;
            dialog.CancelCommandIndex = 1;
            var result = await dialog.ShowAsync();
        }
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
