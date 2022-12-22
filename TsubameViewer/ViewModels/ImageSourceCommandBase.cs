using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.ViewModels.PageNavigation;

namespace TsubameViewer.ViewModels;

public abstract class ImageSourceCommandBase : CommandBase
{
    protected override bool CanExecute(object parameter)
    {
        if (parameter is IImageSource imageSource)
        {
            return CanExecute(imageSource);
        }
        else if (parameter is StorageItemViewModel itemVM)
        {
            return CanExecute(itemVM.Item);
        }
        else if (parameter is IEnumerable<IImageSource> imagesSources)
        {
            if (imagesSources.Any() is false) { return false; }
            if (imagesSources.Count() == 1) { return CanExecute(imagesSources.First()); }
            return CanExecute(imagesSources);
        }
        else if (parameter is IEnumerable<StorageItemViewModel> itemVMs)
        {
            if (itemVMs.Any() is false) { return false; }
            if (itemVMs.Count() == 1) { return CanExecute(itemVMs.First().Item); }
            return CanExecute(itemVMs.Select(x => x.Item));
        }
        else
        {
            return false;
        }
    }

    protected virtual bool CanExecute(IImageSource imageSource) => true;

    protected virtual bool CanExecute(IEnumerable<IImageSource> imageSources) => imageSources.All(CanExecute);

    protected override void Execute(object parameter)
    {
        if (parameter is IImageSource imageSource)
        {
            Execute(imageSource);
        }
        else if (parameter is StorageItemViewModel itemVM)
        {
            Execute(itemVM.Item);
        }
        else if (parameter is IEnumerable<IImageSource> imagesSources)
        {
            if (imagesSources.Count() == 1)
            {
                Execute(imagesSources.First());
            }
            else
            {
                Execute(imagesSources);
            }
        }
        else if (parameter is IEnumerable<StorageItemViewModel> itemVMs)
        {
            if (itemVMs.Count() == 1)
            {
                Execute(itemVMs.First().Item);
            }
            else
            {
                Execute(itemVMs.Select(x => x.Item));
            }                
        }
    }

    protected abstract void Execute(IImageSource imageSource);

    protected virtual void Execute(IEnumerable<IImageSource> imageSources)
    {
        foreach (var image in imageSources)
        {
            Execute(image);
        }
    }

    protected IImageSource FlattenAlbamItemInnerImageSource(IImageSource imageSource)
    {
        return imageSource.FlattenAlbamItemInnerImageSource();
    }

    protected IEnumerable<IImageSource> FlattenAlbamItemInnerImageSource(IEnumerable<IImageSource> imageSources)
    {
        return imageSources.Select(x => x.FlattenAlbamItemInnerImageSource());
    }
}
