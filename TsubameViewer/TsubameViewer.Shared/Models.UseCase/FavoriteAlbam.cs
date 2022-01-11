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
        }

        public readonly static Guid FavoriteAlbamId = new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        private readonly AlbamRepository _albamRepository;

        public bool IsFavorite(string path)
        {
            return _albamRepository.IsExistAlbamItem(FavoriteAlbamId, path);
        }

        public AlbamItemEntry AddFavoriteItem(string path)
        {
            return _albamRepository.AddAlbamItem(FavoriteAlbamId, path);
        }

        public bool DeleteFavoriteItem(string path)
        {
            return _albamRepository.DeleteAlbamItem(FavoriteAlbamId, path);
        }

        public IEnumerable<AlbamItemEntry> GetFavoriteItems(int skip = 0, int limit = int.MaxValue)
        {
            return _albamRepository.GetAlbamItems(FavoriteAlbamId, skip, limit);
        }
    }
}
