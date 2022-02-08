using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.Albam;

namespace TsubameViewer.Models.UseCase.Maintenance
{
    internal sealed class EnsureFavoriteAlbam : ILaunchTimeMaintenance
    {
        private readonly AlbamRepository _albamRepository;

        public EnsureFavoriteAlbam(AlbamRepository albamRepository)
        {
            _albamRepository = albamRepository;
        }

        public void Maintenance()
        {
            FavoriteAlbam.EnsureFavoriteAlbam(_albamRepository);
        }
    }
}
