using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using I18NPortable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.ViewModels.PageNavigation;
using Windows.UI.Xaml.Media;

namespace TsubameViewer.ViewModels.Albam.Commands;

public sealed class FavoriteToggleCommand : ImageSourceCommandBase
{
    private readonly FavoriteAlbam _favoriteAlbam;
    private readonly IMessenger _messenger;

    public FavoriteToggleCommand(
        FavoriteAlbam favoriteAlbam,
        IMessenger messenger)
    {
        _favoriteAlbam = favoriteAlbam;
        _messenger = messenger;
    }

    protected override bool CanExecute(IImageSource imageSource)
    {
        return base.CanExecute(imageSource);
    }
    protected override bool CanExecute(IEnumerable<IImageSource> imageSources)
    {
        return base.CanExecute(imageSources);
    }

    public override bool CanExecute(object parameter)
    {
        return base.CanExecute(parameter);
    }

    protected override void Execute(IImageSource imageSource)
    {
        if (_favoriteAlbam.IsFavorite(imageSource.Path))
        {
            _favoriteAlbam.DeleteFavoriteItem(imageSource);
            _messenger.SendShowTextNotificationMessage("Favorite_Removed".Translate(imageSource.Name));
            _messenger.Send(new ImageSourceFavoriteChanged(imageSource, false));
        }
        else
        {
            _favoriteAlbam.AddFavoriteItem(imageSource);
            _messenger.SendShowTextNotificationMessage("Favorite_Added".Translate(imageSource.Name));
            _messenger.Send(new ImageSourceFavoriteChanged(imageSource, true));
        }
    }


    protected override void Execute(IEnumerable<IImageSource> imageSources)
    {
        if (imageSources.All(x => _favoriteAlbam.IsFavorite(x.Path)))
        {
            foreach (var item in imageSources)
            {
                _favoriteAlbam.DeleteFavoriteItem(item);
                _messenger.Send(new ImageSourceFavoriteChanged(item, false));
            }
            _messenger.SendShowTextNotificationMessage("Favorite_Removed".Translate(imageSources.Count()));
            
        }
        else
        {
            foreach (var item in imageSources)
            {
                _favoriteAlbam.AddFavoriteItem(item);
                _messenger.Send(new ImageSourceFavoriteChanged(item, true));
            }
            _messenger.SendShowTextNotificationMessage("Favorite_Added".Translate(imageSources.Count()));            
        }
    }
}


public sealed class ImageSourceFavoriteChanged : ValueChangedMessage<(IImageSource imageSource, bool isFav)>
{
    public ImageSourceFavoriteChanged(IImageSource imageSource, bool isFav) : base((imageSource, isFav))
    {
    }
}