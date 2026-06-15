using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.ImageViewer;

namespace TsubameViewer.Core.Models;

public sealed class FavoriteAlbam 
{
    public FavoriteAlbam(AlbamRepository albamRepository)
    {
        _albamRepository = albamRepository;
    }

    public static void EnsureFavoriteAlbam(AlbamRepository albamRepository, string favoriteAlbamTitle)
    {
        if (albamRepository.IsExistAlbam(FavoriteAlbamId) is false)
        {
            albamRepository.CreateAlbam(FavoriteAlbamId, favoriteAlbamTitle);
        }
        else
        {
            var albam = albamRepository.GetAlbam(FavoriteAlbamId);
            albamRepository.UpdateAlbam(albam with { Name = favoriteAlbamTitle });
        }
    }

    public readonly static Guid FavoriteAlbamId = new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
    private readonly AlbamRepository _albamRepository;

    public bool IsFavorite(string path)
    {
        return _albamRepository.IsExistAlbamItem(path);
    }

    public AlbamItemEntry AddFavoriteItem(string path, AlbamItemType itemType)
    {
        if (_albamRepository.IsExistAlbamItem(path, itemType))
        {
            return null;
        }

        return _albamRepository.AddAlbamItem(FavoriteAlbamId, path, System.IO.Path.GetFileName(path), itemType);
    }

    public bool DeleteFavoriteItem(string path, AlbamItemType itemType)
    {
        return _albamRepository.DeleteAlbamItem(FavoriteAlbamId, path);
    }

    public AlbamItemEntry AddFavoriteItem(IImageSource imageSource)
    {
        var itemType = imageSource.GetAlbamItemType();
        if (_albamRepository.IsExistAlbamItem(imageSource.Path, itemType))
        {
            return null;
        }

        return _albamRepository.AddAlbamItem(FavoriteAlbamId, imageSource.Path, imageSource.Name, itemType);
    }

    public bool DeleteFavoriteItem(IImageSource imageSource)
    {
        return _albamRepository.DeleteAlbamItem(FavoriteAlbamId, imageSource.Path);
    }
}
