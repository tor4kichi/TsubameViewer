using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Services;
using TsubameViewer.Views.Dialogs;

namespace TsubameViewer.Services;

public sealed class SplitImageInputDialogService : ISplitImageInputDialogService
{
    public async Task<SplitImageInputDialogResult> GetSplitImageInputAsync()
    {
        var dialog = new SplitImageInputDialog();

        return await dialog.GetSplitImageInputAsync();
    }
}
