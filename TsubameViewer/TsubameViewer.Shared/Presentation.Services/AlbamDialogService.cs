using I18NPortable;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Presentation.Services
{
    public sealed class AlbamDialogService
    {
        public async Task<(bool IsSuccess, string Name)> GetNewAlbamNameAsync()
        {
            var textInputDialog = new Views.Dialogs.TextInputDialog(
                "CreateAlbam".Translate(),
                "AlbamName_Placeholder".Translate(),
                "Create".Translate(),
                "AlbamName_Default".Translate()
                );

            await textInputDialog.ShowAsync();
            if (textInputDialog.GetInputText() is not null and var albamName && string.IsNullOrEmpty(albamName) is false)
            {
                return (true, albamName);
            }
            else
            {
                return (false, null);
            }
        }

        public async Task<(bool isEdited, string Rename)> EditAlbamAsync(string albamName)
        {
            var textInputDialog = new Views.Dialogs.TextInputDialog(
                "AlbamEdit".Translate(),
                "AlbamName_Placeholder".Translate(), 
                "Apply".Translate(),
                albamName
                );

            await textInputDialog.ShowAsync();
            if (textInputDialog.GetInputText() is not null and var newAlbamName && string.IsNullOrWhiteSpace(newAlbamName) is false)
            {
                return (true, newAlbamName);
            }
            else
            {
                return (false, null);
            }
        }
    }
}
