using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.ViewModels.PageNavigation;

namespace TsubameViewer.ViewModels.Albam.Commands
{
    public sealed class FavoriteToggleCommand : CommandBase
    {
        protected override bool CanExecute(object parameter)
        {
            if (parameter is IStorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            return parameter is IImageSource;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is IStorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            if (parameter is IImageSource imageSource)
            {

            }
        }
    }
}
