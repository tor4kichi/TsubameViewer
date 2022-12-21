using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Contracts.Maintenance;
using TsubameViewer.Core.Models.Albam;

namespace TsubameViewer.Core.UseCases.Maintenance;

public sealed class EnsureFavoriteAlbam : ILaunchTimeMaintenance
{
    public static string FavoriteAlbamTitle { get; set; }

    private readonly AlbamRepository _albamRepository;

    public EnsureFavoriteAlbam(AlbamRepository albamRepository)
    {
        _albamRepository = albamRepository;
    }

    public void Maintenance()
    {
        FavoriteAlbam.EnsureFavoriteAlbam(_albamRepository, FavoriteAlbamTitle ?? "Favorite");
    }
}
