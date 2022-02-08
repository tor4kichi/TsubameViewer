using I18NPortable;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.Albam;

namespace TsubameViewer.Models.UseCase
{
    public sealed class FavoriteAlbam 
    {
        public FavoriteAlbam(AlbamRepository albamRepository)
        {
            _albamRepository = albamRepository;
        }

        public static void EnsureFavoriteAlbam(AlbamRepository albamRepository)
        {
            if (albamRepository.IsExistAlbam(FavoriteAlbamId) is false)
            {
                albamRepository.CreateAlbam(FavoriteAlbamId, "FavoriteAlbam".Translate());
            }
            else
            {
                var albam = albamRepository.GetAlbam(FavoriteAlbamId);
                albamRepository.UpdateAlbam(albam with { Name = "FavoriteAlbam".Translate() });
            }
        }

        public readonly static Guid FavoriteAlbamId = new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        private readonly AlbamRepository _albamRepository;

        public bool IsFavorite(string path)
        {
            return _albamRepository.IsExistAlbamItem(FavoriteAlbamId, path);
        }

        public AlbamItemEntry AddFavoriteItem(string path, string name)
        {
            if (_albamRepository.IsExistAlbamItem(FavoriteAlbamId, path))
            {
                return null;
            }

            return _albamRepository.AddAlbamItem(FavoriteAlbamId, path, name);
        }

        public bool DeleteFavoriteItem(string path)
        {
            return _albamRepository.DeleteAlbamItem(FavoriteAlbamId, path);
        }
    }
}
