using I18NPortable;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Presentation.Services
{
    public sealed class AlbamDialogService
    {
        public async Task<string> GetAlbamTitleAsync()
        {
            var textInputDialog = new Views.Dialogs.TextInputDialog(
                "CreateAlbam".Translate(), 
                "CreateAlbam_Placeholder".Translate(), 
                "Create".Translate(), 
                "CreateAlbam_DefaultName".Translate()
                );

            await textInputDialog.ShowAsync();
            if (textInputDialog.GetInputText() is not null and var title && string.IsNullOrEmpty(title) is false)
            {
                return title;
            }
            else
            {
                return null;
            }
        }
    }
}
